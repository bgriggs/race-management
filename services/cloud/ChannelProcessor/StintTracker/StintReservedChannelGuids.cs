namespace ChannelProcessor.StintTracker;

/// <summary>
/// Reserved-channel GUIDs the StintTracker reads from / writes to. Kept in sync with
/// <c>Common.ReservedChannels</c> by ID — GUIDs are the cross-tier stable identifiers.
/// </summary>
internal static class StintReservedChannelGuids
{
    public static readonly Guid InPit               = Guid.Parse("da12563a-1167-4899-9956-700b0b693005");
    public static readonly Guid CurrentStintMinutes = Guid.Parse("9a6b8f83-5fc4-4bde-6e5c-8b1c3d5f7e11");
    public static readonly Guid StintCount          = Guid.Parse("ab7c9094-60d5-4cef-7f6d-9c2d4e6a8f12");
}
