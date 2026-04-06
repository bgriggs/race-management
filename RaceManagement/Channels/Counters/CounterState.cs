namespace Channels.Counters;

public class CounterState
{
    public int Id { get; set; }

    /// <summary>
    /// Current counter value.
    /// </summary>
    public int Value { get; set; }

    /// <summary>
    /// Whether the counter has been initialized with the StartValue from parameters.
    /// </summary>
    public bool Initialized { get; set; }

    /// <summary>
    /// True when the up channel was zero on the previous evaluation, enabling rising-edge detection.
    /// Defaults to true so the first non-zero signal triggers the edge.
    /// </summary>
    public bool PreviousUpWasZero { get; set; } = true;

    /// <summary>
    /// True when the down channel was zero on the previous evaluation, enabling rising-edge detection.
    /// </summary>
    public bool PreviousDownWasZero { get; set; } = true;

    /// <summary>
    /// True when the reset channel was zero on the previous evaluation, enabling rising-edge detection.
    /// </summary>
    public bool PreviousResetWasZero { get; set; } = true;
}
