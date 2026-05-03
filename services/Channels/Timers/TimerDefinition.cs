using Channels.Logic;
using System.ComponentModel.DataAnnotations;

namespace Channels.Timers;

public class TimerDefinition
{
    public Guid Id { get; set; }
    [MaxLength(20)]
    public required string Name { get; set; }
    public Guid OutputChId { get; set; }
    public required StatementDefinition Statement { get; set; }
    public bool CountDown { get; set; }
    public bool EnableRollover { get; set; }
    public int RolloverSeconds { get; set; }
    public bool EnableStartSeconds { get; set; }
    public int StartSeconds { get; set; }
    public bool EnableStopSeconds { get; set; }
    public int StopSeconds { get; set; }
}
