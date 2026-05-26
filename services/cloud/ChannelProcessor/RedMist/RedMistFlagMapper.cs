using RedMist.TimingCommon.Models;

namespace ChannelProcessor.RedMist;

/// <summary>
/// Maps RedMist's <see cref="Flags"/> enum to the string values the local
/// <c>RaceFlagState</c> reserved channel carries (see design.md §949). The local
/// vocabulary is narrower than RedMist's — flags we don't model fall back to the
/// closest conservative value. Conservative = "treat as more restrictive than green"
/// so the Fuel Reconciler does not under-count consumption under unfamiliar flags.
/// </summary>
internal static class RedMistFlagMapper
{
    public const string Green = "Green";
    public const string Yellow = "Yellow";
    public const string Red = "Red";
    public const string Code60 = "Code60";
    public const string Code35 = "Code35";

    public static string? Map(Flags flag) => flag switch
    {
        Flags.Green => Green,
        Flags.Yellow => Yellow,
        Flags.Red => Red,
        Flags.Purple60 => Code60,
        Flags.Purple35 => Code35,
        // White (final lap), Checkered (finish), Black (penalty), Unknown — none of these
        // map cleanly to RaceFlagState. Treat as Green: the session is functionally racing or
        // post-session, the reconciler default is Green, and the fuel pace estimates do not
        // care about checkered flags.
        Flags.White or Flags.Checkered => Green,
        Flags.Black => Yellow,
        Flags.Unknown => Green,
        _ => null,
    };
}
