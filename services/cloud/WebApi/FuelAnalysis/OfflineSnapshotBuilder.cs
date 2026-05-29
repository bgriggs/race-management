using Cloud.Shared.Database;
using Cloud.Shared.FuelAnalysis;
using Microsoft.EntityFrameworkCore;

namespace WebApi.FuelAnalysis;

/// <summary>
/// Computes a PitFill-only <see cref="FuelRangeSnapshot"/> directly from Postgres when
/// ChannelProcessor's live snapshot is missing or belongs to a different race. Covers the
/// engineer-managed race flow (manual stints, manually-entered fuel volumes) with no live
/// car telemetry. ChannelProcessor's <c>FuelReconciler</c> will overwrite this with a
/// richer telemetry-blended snapshot the moment stream data flows for the car.
/// </summary>
/// <remarks>
/// The PitFill formula is duplicated from
/// <c>ChannelProcessor.FuelAnalysis.Estimators.PitFillEstimator</c> rather than referenced
/// because WebApi does not (and should not) take a project ref on ChannelProcessor. If a
/// third consumer ever needs this calculation, lift it into <c>Cloud.Shared</c>.
/// </remarks>
public static class OfflineSnapshotBuilder
{
    /// <summary>Matches <c>FuelReconciler.DefaultHighConfidenceThreshold</c>.</summary>
    private const double DefaultHighConfidenceThreshold = 0.98;

    /// <summary>One-sided 98% z-score (inverse standard normal CDF at 0.98). Hardcoded to
    /// avoid taking a dependency on <c>InverseStandardNormalCdf</c> in ChannelProcessor.</summary>
    private const double ZAt98 = 2.0537489106318225;

    /// <summary>Mirrors <c>PitFillEstimator.SigmaNoEntryGallons</c> / <c>SigmaEnteredGallons</c>.</summary>
    private const double SigmaNoEntryGallons = 3.0;
    private const double SigmaEnteredGallons = 1.5;

    public static async Task<FuelRangeSnapshot?> BuildAsync(
        RaceManagementContext db,
        int teamId, string carNumber, int raceId,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var window = await db.FuelWindows
            .AsNoTracking()
            .Where(w => w.TeamId == teamId && w.CarNumber == carNumber && w.RaceId == raceId && w.ClosedAt == null)
            .OrderByDescending(w => w.OpenedAt)
            .FirstOrDefaultAsync(ct);
        if (window is null) return null;

        var startRefuel = await db.RefuelEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == window.StartRefuelEventId, ct);

        var fuelConfig = await CarFuelConfigLoader.LoadAsync(db, teamId, carNumber, ct);
        var tankCap = fuelConfig?.TankCapacityGallons ?? 0;
        var baseRate = fuelConfig?.DefaultConsumptionGalPerMin ?? 0;

        // Engineer-created windows (StintEditor.CreateAsync) store OpenedAt as race-TZ
        // wall-clock encoded as naive (timestamp WITHOUT time zone). Comparing that to
        // DateTime.UtcNow as raw ticks adds the TZ offset to elapsed (e.g. EDT race + UTC
        // server inflates elapsed by 4h → empties the tank artificially). Translate "now"
        // into race-TZ wall-clock so both sides of the subtraction are in the same frame.
        var race = await db.Races
            .AsNoTracking()
            .Where(r => r.Id == raceId && r.TeamId == teamId)
            .Select(r => new { r.TimeZone })
            .FirstOrDefaultAsync(ct);
        var nowInRaceTz = ConvertUtcToNaiveRaceTz(nowUtc, race?.TimeZone);
        var elapsedMin = Math.Max(0, (nowInRaceTz - window.OpenedAt).TotalMinutes);
        var entered = startRefuel?.EnteredFuelGallons;

        // Sum running-time of stints inside this window. EndAt on a stint is the
        // engineer's *expected* next-pit time, NOT a "complete" marker — so a stint with
        // EndAt in the future is still active. Active stints contribute (now - StartAt);
        // already-pitted stints (EndAt in the past) contribute (EndAt - StartAt). Stints
        // whose StartAt is still in the future contribute nothing.
        var windowStints = await db.Stints
            .AsNoTracking()
            .Where(s => s.FuelWindowId == window.Id)
            .Select(s => new { s.StartAt, s.EndAt })
            .ToListAsync(ct);
        var stintRunMin = 0.0;
        foreach (var s in windowStints)
        {
            if (s.StartAt >= nowInRaceTz) continue;
            var effectiveEnd = s.EndAt is DateTime end && end < nowInRaceTz ? end : nowInRaceTz;
            stintRunMin += (effectiveEnd - s.StartAt).TotalMinutes;
        }

        // PitFill — adapted from PitFillEstimator.Compute, but using stint-time elapsed
        // instead of wall-clock so it tracks the engineer's mental model of consumption.
        var pitfillAvailable = tankCap > 0 && baseRate > 0;
        double? pitfillGallons = null;
        double? pitfillSigma = null;
        double? pitfillMinutes = null;
        string? pitfillReason = null;
        if (!pitfillAvailable)
        {
            pitfillReason = tankCap <= 0
                ? "TankCapacityGallons not configured"
                : "DefaultConsumptionGalPerMin not configured";
        }
        else
        {
            var startingFuel = entered ?? tankCap;
            var consumed = baseRate * stintRunMin;
            pitfillGallons = Math.Max(0, startingFuel - consumed);
            pitfillSigma = entered is null ? SigmaNoEntryGallons : SigmaEnteredGallons;
            pitfillMinutes = pitfillGallons / baseRate;
        }

        // List every estimator the live reconciler exposes so the UI's estimator table is
        // structurally identical between offline-computed and telemetry-blended snapshots.
        const string noTelemetry = "no telemetry";
        var estimators = new List<EstimatorReadout>
        {
            new() { Name = "ecu", Available = false, Reason = noTelemetry },
            new() { Name = "flowmeter.raw", Available = false, Reason = noTelemetry },
            new() { Name = "flowmeter.corrected", Available = false, Reason = noTelemetry },
            new()
            {
                Name = "pitfill",
                Available = pitfillAvailable,
                RangeGallons = pitfillGallons,
                RangeMinutes = pitfillMinutes,
                Sigma = pitfillSigma,
                Reason = pitfillReason,
            },
            new() { Name = "throttle.integral", Available = false, Reason = noTelemetry },
            new() { Name = "throttle.grid", Available = false, Reason = noTelemetry },
        };

        // Primary = PitFill (only available estimator in the offline build).
        double primaryConfidence = 0;
        if (pitfillGallons is double pg && pg > 0 && pitfillSigma is double ps)
        {
            primaryConfidence = Math.Clamp(1.0 - ps / pg, 0.0, 1.0);
        }

        // HighConfidence — matches FuelReconciler.ComputeHighConfidence.
        double? hcGallons = null;
        double? hcMinutes = null;
        if (pitfillGallons is double pgHc && pitfillSigma is double psHc)
        {
            hcGallons = Math.Max(0, pgHc - ZAt98 * psHc);
            if (baseRate > 0) hcMinutes = hcGallons / baseRate;
        }

        return new FuelRangeSnapshot
        {
            TeamId = teamId,
            CarNumber = carNumber,
            RaceId = raceId,
            AsOfUtc = nowUtc,
            Primary = new RangeReadout
            {
                Source = pitfillAvailable ? "pitfill" : "unavailable",
                RangeGallons = pitfillGallons,
                RangeMinutes = pitfillMinutes,
                RangeLaps = null,
                Confidence = primaryConfidence,
            },
            HighConfidence = new RangeReadout
            {
                Source = pitfillAvailable ? "pitfill" : "unavailable",
                RangeGallons = hcGallons,
                RangeMinutes = hcMinutes,
                RangeLaps = null,
                Confidence = DefaultHighConfidenceThreshold,
            },
            HighConfidenceThreshold = DefaultHighConfidenceThreshold,
            Estimators = estimators,
            Reconciler = new ReconcilerDetails
            {
                ConsensusRangeMinutes = pitfillMinutes,
                SpreadGallons = null,
                OutlierCount = 0,
                PaceMultiplier = 1.0,
                FlagMultiplier = 1.0,
                RecentAvgLapTimeSeconds = null,
                CurrentEffectiveRateGalPerMin = baseRate > 0 ? baseRate : null,
                FlowMeterCalibrationFactor = null,
                FlowMeterCalibrationFactorSource = null,
            },
            FuelWindow = new FuelWindowDetails
            {
                Id = window.Id,
                // Pass the naive race-TZ wall-clock through unchanged — the UI's panel
                // already treats it as wall-clock and the same naive-encoding convention
                // applies to stint times rendered alongside it. Tagging Kind=Unspecified
                // prevents JSON serialization from helpfully appending a 'Z' that would
                // mislead the client into a TZ shift.
                OpenedAtUtc = DateTime.SpecifyKind(window.OpenedAt, DateTimeKind.Unspecified),
                ElapsedMinutes = elapsedMin,
                EnteredFuelGallons = entered,
                GallonsUsedInWindow = null,
            },
        };
    }

    /// <summary>
    /// Converts <paramref name="utc"/> to the wall-clock value in <paramref name="ianaTimeZone"/>,
    /// returned as <c>Kind=Unspecified</c> so it can be subtracted directly from a naive
    /// (race-TZ wall-clock, no-Kind) DateTime read from Postgres without TZ math.
    /// Falls back to <paramref name="utc"/> unchanged when the timezone is null or unknown.
    /// </summary>
    public static DateTime ConvertUtcToNaiveRaceTz(DateTime utc, string? ianaTimeZone)
    {
        if (string.IsNullOrWhiteSpace(ianaTimeZone)) return DateTime.SpecifyKind(utc, DateTimeKind.Unspecified);
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(ianaTimeZone);
            var wall = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);
            return DateTime.SpecifyKind(wall, DateTimeKind.Unspecified);
        }
        catch (TimeZoneNotFoundException)
        {
            return DateTime.SpecifyKind(utc, DateTimeKind.Unspecified);
        }
        catch (InvalidTimeZoneException)
        {
            return DateTime.SpecifyKind(utc, DateTimeKind.Unspecified);
        }
    }

}
