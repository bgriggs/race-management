using ChannelProcessor.FuelAnalysis.Estimators;
using ChannelProcessor.FuelAnalysis.Pace;
using ChannelProcessor.FuelAnalysis.State;
using Cloud.Shared.Database.Models.FuelAnalysis;
using Cloud.Shared.FuelAnalysis;
using Common.FuelAnalysis;

namespace ChannelProcessor.FuelAnalysis.Reconciler;

/// <summary>
/// Top-level reconciliation pass per design.md §850–887. Calls every registered
/// <see cref="IFuelEstimator"/>, projects fuel-remaining to time via <see cref="RateModel"/>
/// (or the estimator's own rate when <see cref="IFuelEstimator.BypassesRateModel"/> is set),
/// runs <see cref="OutlierDebouncer"/>, blends the non-outliers as an inverse-variance
/// weighted mean, derives the high-confidence range at the configured threshold, and
/// assembles a <see cref="FuelRangeSnapshot"/>.
/// </summary>
public sealed class FuelReconciler(
    IEnumerable<IFuelEstimator> estimators,
    OutlierDebouncer debouncer,
    RateModel rateModel,
    DriverPaceCalculator paceCalculator)
{
    private readonly IReadOnlyList<IFuelEstimator> _estimators = estimators.ToList();

    // Default HC threshold (design.md §860). Per-car and per-team overrides land alongside
    // the TeamFuelDefaults reader in a follow-up slice.
    private const double DefaultHighConfidenceThreshold = 0.98;

    // Flag multiplier remains stubbed at 1.0 until RaceFlagState integration ships.
    private const double FlagMultiplier = 1.0;

    public FuelRangeSnapshot Build(
        int teamId, string carNumber, int raceId,
        CarFuelState state,
        CarFuelConfig fuelConfig,
        CalibrationFactor? flowMeterCalibration,
        DateTime nowUtc)
    {
        var context = new EstimatorContext(state, fuelConfig, nowUtc, flowMeterCalibration);
        var pace = paceCalculator.Compute(state);

        var readings = new List<NamedReading>(_estimators.Count);
        var rateResults = new Dictionary<string, RateModelResult>(_estimators.Count);

        foreach (var est in _estimators)
        {
            var reading = est.Compute(context);
            readings.Add(new NamedReading(est.Name, reading));
            if (reading.Available && reading.BaseRateGalPerMin is double baseRate)
            {
                rateResults[est.Name] = est.BypassesRateModel
                    ? new RateModelResult(baseRate, 1.0, 1.0, baseRate)
                    : rateModel.Resolve(baseRate, pace.PaceMultiplier, FlagMultiplier);
            }
        }

        var outliers = debouncer.ApplyAndCommit(readings, state, nowUtc);

        var estimatorReadouts = BuildReadouts(readings, outliers, rateResults, pace.RecentAvgLapTimeSeconds);

        var contributors = readings
            .Where(r => r.Reading.Available
                        && r.Reading.RangeGallons is not null
                        && r.Reading.SigmaGallons is not null
                        && !(outliers.TryGetValue(r.Name, out var o) && o))
            .ToList();

        var primary = BlendPrimary(contributors, rateResults);
        var hc = ComputeHighConfidence(primary, DefaultHighConfidenceThreshold);
        var primaryLaps = MinutesToLaps(primary.RangeMinutes, pace.RecentAvgLapTimeSeconds);
        var hcLaps = MinutesToLaps(hc.RangeMinutes, pace.RecentAvgLapTimeSeconds);

        return new FuelRangeSnapshot
        {
            TeamId = teamId,
            CarNumber = carNumber,
            RaceId = raceId,
            AsOfUtc = nowUtc,
            Primary = new RangeReadout
            {
                Source = primary.Source,
                RangeGallons = primary.RangeGallons,
                RangeMinutes = primary.RangeMinutes,
                RangeLaps = primaryLaps,
                Confidence = primary.Confidence,
            },
            HighConfidence = new RangeReadout
            {
                Source = primary.Source,
                RangeGallons = hc.RangeGallons,
                RangeMinutes = hc.RangeMinutes,
                RangeLaps = hcLaps,
                Confidence = DefaultHighConfidenceThreshold,
            },
            HighConfidenceThreshold = DefaultHighConfidenceThreshold,
            Estimators = estimatorReadouts,
            Reconciler = new ReconcilerDetails
            {
                ConsensusRangeMinutes = primary.RangeMinutes,
                SpreadGallons = contributors.Count >= 2
                    ? contributors.Max(c => c.Reading.RangeGallons!.Value) - contributors.Min(c => c.Reading.RangeGallons!.Value)
                    : null,
                OutlierCount = outliers.Count(kv => kv.Value),
                PaceMultiplier = pace.PaceMultiplier,
                FlagMultiplier = FlagMultiplier,
                RecentAvgLapTimeSeconds = pace.RecentAvgLapTimeSeconds,
                CurrentEffectiveRateGalPerMin = primary.EffectiveRate > 0 ? primary.EffectiveRate : null,
                FlowMeterCalibrationFactor = flowMeterCalibration?.Value,
                FlowMeterCalibrationFactorSource = flowMeterCalibration is null
                    ? null
                    : CalibrationSourceLabel(flowMeterCalibration.Source),
            },
            FuelWindow = new FuelWindowDetails
            {
                Id = state.OpenFuelWindowId,
                OpenedAtUtc = state.OpenFuelWindowOpenedAt,
                ElapsedMinutes = state.OpenFuelWindowOpenedAt is DateTime openedAt
                    ? Math.Max(0, (nowUtc - openedAt).TotalMinutes)
                    : 0,
                EnteredFuelGallons = state.CurrentWindowEnteredFuelGallons,
                GallonsUsedInWindow = ComputeGallonsUsedInWindow(state),
            },
        };
    }

    private static List<EstimatorReadout> BuildReadouts(
        List<NamedReading> readings,
        Dictionary<string, bool> outliers,
        Dictionary<string, RateModelResult> rateResults,
        double? recentAvgLapTimeSeconds)
    {
        var output = new List<EstimatorReadout>(readings.Count);
        foreach (var r in readings)
        {
            double? rangeMin = null;
            if (r.Reading.Available
                && r.Reading.RangeGallons is double rg
                && rateResults.TryGetValue(r.Name, out var rr)
                && rr.EffectiveRate > 0)
            {
                rangeMin = rg / rr.EffectiveRate;
            }
            output.Add(new EstimatorReadout
            {
                Name = r.Name,
                Available = r.Reading.Available,
                RangeGallons = r.Reading.RangeGallons,
                RangeMinutes = rangeMin,
                RangeLaps = MinutesToLaps(rangeMin, recentAvgLapTimeSeconds),
                Sigma = r.Reading.SigmaGallons,
                IsOutlier = outliers.TryGetValue(r.Name, out var o) && o,
                Reason = r.Reading.UnavailableReason,
            });
        }
        return output;
    }

    private static PrimaryResult BlendPrimary(
        List<NamedReading> contributors,
        Dictionary<string, RateModelResult> rateResults)
    {
        if (contributors.Count == 0)
            return new PrimaryResult("unavailable", null, null, null, 0, 0);

        double sumWxV = 0, sumW = 0, sumWxRate = 0;
        foreach (var c in contributors)
        {
            var sigma = c.Reading.SigmaGallons!.Value;
            if (sigma <= 0) continue;
            var w = 1.0 / (sigma * sigma);
            sumWxV += w * c.Reading.RangeGallons!.Value;
            sumW += w;
            if (rateResults.TryGetValue(c.Name, out var rr))
                sumWxRate += w * rr.EffectiveRate;
        }
        if (sumW <= 0) return new PrimaryResult("unavailable", null, null, null, 0, 0);

        var primaryGallons = sumWxV / sumW;
        var primarySigma = Math.Sqrt(1.0 / sumW);
        var effectiveRate = sumWxRate / sumW;
        var rangeMinutes = effectiveRate > 0 ? primaryGallons / effectiveRate : (double?)null;

        var source = contributors.Count == 1
            ? StripSubName(contributors[0].Name)
            : "blended";

        var confidence = primaryGallons > 0
            ? Math.Clamp(1.0 - primarySigma / primaryGallons, 0.0, 1.0)
            : 0;

        return new PrimaryResult(source, primaryGallons, primarySigma, rangeMinutes, effectiveRate, confidence);
    }

    private static HcResult ComputeHighConfidence(PrimaryResult primary, double threshold)
    {
        if (primary.RangeGallons is null || primary.SigmaGallons is null)
            return new HcResult(null, null);
        var z = InverseStandardNormalCdf.Evaluate(threshold);
        var hcGallons = Math.Max(0, primary.RangeGallons.Value - z * primary.SigmaGallons.Value);
        double? hcMinutes = primary.EffectiveRate > 0 ? hcGallons / primary.EffectiveRate : null;
        return new HcResult(hcGallons, hcMinutes);
    }

    private static double? MinutesToLaps(double? rangeMinutes, double? recentAvgLapTimeSeconds)
    {
        if (rangeMinutes is not double m || recentAvgLapTimeSeconds is not double secs || secs <= 0) return null;
        return m * 60.0 / secs;
    }

    private static double? ComputeGallonsUsedInWindow(CarFuelState state) =>
        state.LastFuelUsedValue is double cur && state.CurrentWindowFlowMeterFuelUsedAtOpen is double open
            ? Math.Max(0, cur - open)
            : null;

    private static string StripSubName(string name)
    {
        var dot = name.IndexOf('.');
        return dot < 0 ? name : name[..dot];
    }

    // Match the design's UI labels (design.md §867): "learned" / "manual_override" / "reset".
    private static string CalibrationSourceLabel(CalibrationFactorSource source) => source switch
    {
        CalibrationFactorSource.Learned => "learned",
        CalibrationFactorSource.ManualOverride => "manual_override",
        CalibrationFactorSource.Reset => "reset",
        _ => source.ToString().ToLowerInvariant(),
    };

    private readonly record struct PrimaryResult(
        string Source,
        double? RangeGallons,
        double? SigmaGallons,
        double? RangeMinutes,
        double EffectiveRate,
        double Confidence);

    private readonly record struct HcResult(double? RangeGallons, double? RangeMinutes);
}
