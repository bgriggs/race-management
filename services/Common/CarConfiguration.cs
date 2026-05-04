using Channels;
using Channels.Alarms;
using Channels.Counters;
using Channels.Math;
using Channels.Tables;
using Channels.Timers;
using Channels.UserConditions;
using Common.CanBus;
using System.ComponentModel.DataAnnotations;

namespace Common;

/// <summary>
/// Configuration settings for the service running in the car, such as on a Raspberry Pi.
/// </summary>
public class CarConfiguration
{
    public Guid ConfigurationId { get; set; }
    public int ConfigurationSchemaVersion { get; } = 1;
    [Length(3, 32)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(1024)]
    public string Notes { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
    public DateTime? LastUpdatedOnCarTimestamp { get; set; }

    [Length(1, 6)]
    public required string Car { get; set; }

    public bool IsCloudConnectionEnabled { get; set; }
    [MaxLength(64)]
    public required string ClientId { get; set; }
    [MaxLength(32)]
    public required string ClientSecret { get; set; }

    public CanBusConfig CanConfig { get; set; } = new();
    public List<ChannelDefinition> ChannelDefinitions { get; set; } = [];
    public List<AlarmDefinition> AlarmDefinitions { get; set; } = [];
    public List<CounterDefinition> CounterDefinitions { get; set; } = [];
    public List<MathDefinition> MathDefinitions { get; set; } = [];
    public List<TableDefinition> TableMappings { get; set; } = [];
    public List<TimerDefinition> TimerDefinitions { get; set; } = [];
    public List<ConditionDefinition> UserConditions { get; set; } = [];

}