namespace Channels.Logic;

/// <summary>
/// Abstracts persistence of when each comparison first became true, used to enforce
/// the ForMs duration requirement before a comparison is considered satisfied.
/// </summary>
public interface IComparisonDurationRepository
{
    /// <summary>Returns the time the comparison first became true, or null if it is not currently timing.</summary>
    Task<DateTimeOffset?> GetStartTimeAsync(Guid comparisonId);

    Task SetStartTimeAsync(Guid comparisonId, DateTimeOffset startTime);

    /// <summary>Clears the start time when the comparison becomes false, resetting the duration timer.</summary>
    Task RemoveStartTimeAsync(Guid comparisonId);
}
