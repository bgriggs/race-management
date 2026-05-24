using ChannelProcessor.FuelAnalysis.State;
using Cloud.Shared.Database;
using Cloud.Shared.Database.Models.FuelAnalysis;

namespace ChannelProcessor.FuelAnalysis.Calibration;

/// <summary>
/// Updates the per-car FlowMeter calibration factor when a <see cref="FuelWindow"/> closes
/// with Reset-Confirmed ECU data (design.md §647). Computes
/// <c>observedFactor = ECU_used ÷ FlowMeter_used</c>, smooths with EMA
/// (α = <see cref="EmaAlpha"/>), and appends a row to <see cref="CalibrationFactor"/>.
/// Skipped silently when:
/// <list type="bullet">
///   <item>The closing window's ECU state is not Reset-Confirmed.</item>
///   <item>FlowMeter or ECU used is zero / negative / unavailable.</item>
///   <item>Observed factor is out of [<see cref="MinPlausibleFactor"/>,
///     <see cref="MaxPlausibleFactor"/>] (sensor glitch).</item>
///   <item>The most recent calibration row is a <see cref="CalibrationFactorSource.ManualOverride"/> —
///     engineer-blocked until "Resume learning".</item>
/// </list>
/// </summary>
public sealed class CalibrationFactorLearner(
    ICalibrationFactorReader reader,
    ILogger<CalibrationFactorLearner> logger)
{
    public const double EmaAlpha = 0.3;
    public const double MinPlausibleFactor = 0.5;
    public const double MaxPlausibleFactor = 2.0;

    public async Task LearnAsync(
        RaceManagementContext db,
        int teamId, string carNumber, int raceId,
        CarFuelState state,
        DateTime windowClosedAtUtc,
        CancellationToken ct)
    {
        if (state.CurrentWindowEcuResetState != EcuResetState.ResetConfirmed) return;

        // FlowMeter used in this window — needs both baseline and current value.
        if (state.CurrentWindowFlowMeterFuelUsedAtOpen is not double fmBaseline) return;
        if (state.LastFuelUsedValue is not double fmCurrent) return;
        var fmUsed = fmCurrent - fmBaseline;
        if (fmUsed <= 0.1) return; // too little flow to learn from

        // ECU used in this window — TripFuel started at ~0 after the reset, so the current
        // TripFuel value IS the ECU's measure of fuel used in this window.
        if (state.LastTripFuelValue is not double ecuUsed) return;
        if (ecuUsed <= 0.1) return;

        var observed = ecuUsed / fmUsed;
        if (observed < MinPlausibleFactor || observed > MaxPlausibleFactor)
        {
            logger.LogWarning(
                "Skipping calibration learn for team {TeamId} car {CarNumber}: observed factor {Observed:F3} out of [{Min}, {Max}] (ECU={Ecu:F2}, FM={Fm:F2})",
                teamId, carNumber, observed, MinPlausibleFactor, MaxPlausibleFactor, ecuUsed, fmUsed);
            return;
        }

        var current = await reader.GetLatestAsync(teamId, carNumber, ct);
        if (current?.Source == CalibrationFactorSource.ManualOverride)
        {
            logger.LogInformation(
                "Calibration learning blocked by ManualOverride for team {TeamId} car {CarNumber} — observed {Observed:F3} not applied",
                teamId, carNumber, observed);
            return;
        }

        var newFactor = current is null
            ? observed                                                  // bootstrap
            : (1 - EmaAlpha) * current.Value + EmaAlpha * observed;     // EMA smoothing

        db.CalibrationFactors.Add(new CalibrationFactor
        {
            TeamId = teamId,
            CarNumber = carNumber,
            Value = newFactor,
            Source = CalibrationFactorSource.Learned,
            EffectiveAt = windowClosedAtUtc,
            RaceId = raceId,
        });
        await db.SaveChangesAsync(ct);
        await reader.InvalidateAsync(teamId, carNumber, ct);

        logger.LogInformation(
            "Learned FlowMeter calibration for team {TeamId} car {CarNumber}: observed {Observed:F3}, prior {Prior}, new {New:F3} (ECU={Ecu:F2} gal, FM={Fm:F2} gal)",
            teamId, carNumber, observed, current?.Value.ToString("F3") ?? "—", newFactor, ecuUsed, fmUsed);
    }
}
