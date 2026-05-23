using Channels;
using Channels.Alarms;
using Channels.Counters;
using Channels.Enums;
using Channels.Logging;
using Channels.Math;
using Channels.Tables;
using Channels.Timers;
using Channels.UserConditions;
using Common.CanBus;
using Common.FuelAnalysis;
using System.ComponentModel.DataAnnotations;

namespace Common;

/// <summary>
/// Configuration settings for the service running in the car, such as on a Raspberry PI.
/// </summary>
public class CarConfiguration
{
    /// <summary>
    /// ID of the instance of the configuration.
    /// </summary>
    public Guid ConfigurationId { get; set; }
    /// <summary>
    /// Version of the configuration schema. This is used to handle breaking changes to the 
    /// configuration schema. If the version is different from what the service expects, it 
    /// may indicate that the configuration is incompatible with the current version of the 
    /// service, and the service can choose to reject the configuration and prompt the user 
    /// to update their configuration to a compatible version.
    /// </summary>
    public int ConfigurationSchemaVersion { get; } = 1;
    /// <summary>
    /// Name of the configuration such as car 777.
    /// </summary>
    [Length(3, 32)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Comments about the configuration.
    /// </summary>
    [MaxLength(1024)]
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// Last time the configuration was saved/persisted.
    /// </summary>
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// Last time the configuration was updated on the car, based on the timestamp from the car. 
    /// This is used to determine if the configuration has been updated on the car since it was 
    /// last loaded, which can help prevent overwriting changes made on the car when saving a 
    /// configuration from an external source like a phone or laptop.
    /// </summary>
    public DateTime? LastUpdatedOnCarTimestamp { get; set; }

    /// <summary>
    /// Car number this configuration runs on.
    /// </summary>
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
    public List<TableDefinition> TableDefinitions { get; set; } = [];
    public List<TimerDefinition> TimerDefinitions { get; set; } = [];
    public List<ConditionDefinition> UserConditions { get; set; } = [];
    public List<LoggingDefinition> LoggingDefinitions { get; set; } = [];
    public List<EnumDefinition> EnumDefinitions { get; set; } = [];
    public CarFuelConfig FuelConfig { get; set; } = new();
}