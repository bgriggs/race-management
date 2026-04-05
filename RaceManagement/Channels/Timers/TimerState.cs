namespace Channels.Timers;

public class TimerState
{
    public int Id { get; set; }

    /// <summary>
    /// The time the timer was started, or null if the timer is not running.
    /// </summary>
    public DateTimeOffset? Started { get; set; }

    /// <summary>
    /// The timer value (in seconds) at the moment the timer was started.
    /// Used together with <see cref="Started"/> to calculate the current value.
    /// </summary>
    public double StartValue { get; set; }

    /// <summary>
    /// Previous evaluation result of the start condition, used for edge detection (false→true).
    /// Initialized to true per spec so the first false→true transition can be detected.
    /// </summary>
    public bool PreviousStartResult { get; set; } = true;

    /// <summary>
    /// Previous evaluation result of the stop condition, used for edge detection (false→true).
    /// Initialized to true per spec so the first false→true transition can be detected.
    /// </summary>
    public bool PreviousStopResult { get; set; } = true;
}
