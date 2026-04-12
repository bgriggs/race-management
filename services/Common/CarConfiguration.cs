using Channels;
using Channels.Counters;
using Channels.Math;
using Channels.Tables;
using Channels.Timers;
using Channels.UserConditions;
using Common.CanBus;

namespace Common;

/// <summary>
/// Configuration settings for the service running in the car, such as on a Raspberry Pi.
/// </summary>
public class CarConfiguration
{
    public Guid ConfigurationId { get; set; }
    public int ConfigurationSchemaVersion { get; } = 1;
    public string Name { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }

    public required string Car { get; set; }
    public required string ClientId { get; set; }
    public required string ClientSecret { get; set; }

    public CanMessageConfig CanConfig { get; set; } = new();
    public List<ChannelDefinition> ChannelDefinitions { get; } = [];
    public List<CounterDefinition> CounterDefinitions { get; } = [];
    public List<MathDefinition> MathDefinitions { get; } = [];
    public List<TableDefinition> TableMappings { get; } = [];
    public List<TimerDefinition> TimerDefinitions { get; } = [];
    public List<ConditionDefinition> UserConditions { get; } = [];

}

