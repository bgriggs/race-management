namespace Channels.Logic;

/// <summary>
/// Abstracts persistence of the last-seen channel value, used by Updated and ChangedBy
/// logic types to detect value changes between evaluations.
/// </summary>
public interface IPreviousChannelValueRepository
{
    /// <summary>Returns the previously recorded value for the channel, or null if none has been recorded yet.</summary>
    Task<string?> GetPreviousValueAsync(int channelId);

    Task SetPreviousValueAsync(int channelId, string value);
}
