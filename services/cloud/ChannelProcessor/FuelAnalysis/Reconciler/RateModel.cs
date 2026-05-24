namespace ChannelProcessor.FuelAnalysis.Reconciler;

/// <summary>
/// Shared rate-model resolver per design.md §765:
/// <c>effectiveRate = baseRate × paceMultiplier × flagMultiplier</c>.
/// <para>
/// <see cref="Pace.DriverPaceCalculator"/> supplies the live pace multiplier from the
/// <c>LastLapTime</c> rolling window. <see cref="FlagMultiplier"/> remains stubbed at 1.0
/// until the <c>RaceFlagState</c> source ships — per design §804, when
/// <c>RaceFlagState ≠ Green</c> the caller is responsible for forcing pace to 1.0 too,
/// since flag and pace already capture the same slowdown and multiplying both
/// double-counts.
/// </para>
/// </summary>
public sealed class RateModel
{
    public RateModelResult Resolve(double baseRate, double paceMultiplier, double flagMultiplier) =>
        new(
            BaseRate: baseRate,
            PaceMultiplier: paceMultiplier,
            FlagMultiplier: flagMultiplier,
            EffectiveRate: baseRate * paceMultiplier * flagMultiplier);
}

public readonly record struct RateModelResult(
    double BaseRate,
    double PaceMultiplier,
    double FlagMultiplier,
    double EffectiveRate);
