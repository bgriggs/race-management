namespace Cloud.Shared;

public static class Consts
{
    public const string CAR_CONNECTIONS = "car-conns";
    public const string CAR_CONNECTION = "car-connid-{0}";
    public const string CAR_CHANNEL_VALUES_STREAM_KEY = "car-channel-values";
    public const string CAR_STREAM_FIELD = "team-{0}-car-{1}";
    public const string CAR_CONNECTION_BY_CAR = "car-conn-by-car:{0}";

    // ChannelProcessor — latest channel value state
    public const string CHANNEL_PROC_CONSUMER_GROUP = "channelproc";

    // CarGateway — forwards CloudToCar channel values back to the car via CarHub
    public const string CARGW_CONSUMER_GROUP = "cargw";
    public const string CAR_CHANNEL_STATE_KEY = "car-channels:{0}";
    public const string CAR_CHANNEL_CHANGES_CHANNEL = "car-channel-changes:{0}";

    // Currently-active CarConfiguration ID for a connected car, keyed by carKey.
    // Written by CarHub on each SendChannelValuesAsync, deleted on disconnect.
    public const string CAR_ACTIVE_CONFIG_KEY = "car-active-config:{0}";

    // PerTeam (Scope = PerTeam) channel routing — cloud-origin values delivered to all
    // connected cars in a team. Stream payload is TeamChannelValue (Guid-keyed, not
    // SessionIndex-keyed) and ChannelProcessor stores per-team state separately from
    // the per-car hashes.
    public const string TEAM_CHANNEL_VALUES_STREAM_KEY = "team-channel-values";
    public const string TEAM_STREAM_FIELD = "team-{0}";
    public const string TEAM_CHANNEL_STATE_KEY = "team-channels:{0}";
    public const string TEAM_CHANNEL_CHANGES_CHANNEL = "team-channel-changes:{0}";

    // Set of currently-connected carKeys for a team. SADD on first SendChannelValuesAsync
    // per (connection, carKey), SREM on disconnect. Used by CarGateway to fan-out team
    // values to every active car in the team without scanning the global CAR_CONNECTIONS hash.
    public const string TEAM_CONNECTED_CARS = "team-connected-cars:{0}";

    // Fuel Analysis — independent consumer group on the car-channel-values stream so the
    // reconciler sees every message (it does not share work with the state-cache consumer
    // in CHANNEL_PROC_CONSUMER_GROUP).
    public const string CHANNEL_PROC_FUEL_CONSUMER_GROUP = "channelproc-fuel";
    // Per-car serialized FuelRangeSnapshot (latest only). Read by WebApi for the detail panel.
    public const string FUEL_SNAPSHOT_KEY = "fuel-snapshot:{0}";
    // Per-car serialized reconciler runtime state (open FuelWindow accumulators, debounce
    // state, recent-lap-time window, etc.). Rebuildable from Postgres on session start.
    public const string FUEL_STATE_KEY = "fuel-state:{0}";

    // Alarm Processor — independent consumer group on car-channel-values so it sees every
    // message regardless of what the latest-state cache consumer does.
    public const string CHANNEL_PROC_ALARM_CONSUMER_GROUP = "channelproc-alarm";
    // Per-(carKey, alarmId) AlarmState (MessagePack-serialized).
    public const string ALARM_STATE_KEY = "alarm-state:{0}:{1}";
    // Per-(carKey, statementId) bool? state.
    public const string ALARM_STATEMENT_STATE_KEY = "alarm-statement-state:{0}:{1}";
    // Per-(carKey, comparisonId) ForMs duration start time (ISO ticks).
    public const string ALARM_COMPARISON_DURATION_KEY = "alarm-comparison-duration:{0}:{1}";
    // Per-(carKey, channelId) previous channel value string (for Updated / ChangedBy logic).
    public const string ALARM_PREVIOUS_VALUE_KEY = "alarm-prev-value:{0}:{1}";

    // Per-team alarm state-change pub/sub. Published by ChannelProcessor on each
    // edge transition (Activated/Deactivated) and by WebApi on Acknowledge. Fanned
    // out to browsers by WebApi's ChannelPropagatorService.
    public const string ALARM_CHANGES_CHANNEL = "alarm-changes:{0}";

    // Per-team alarm-definition invalidation pub/sub. Published by WebApi after a
    // definition save/delete; consumed by ChannelProcessor's AlarmDefinitionRepository
    // to evict cached per-car alarm sets immediately rather than waiting on the TTL.
    public const string ALARM_CONFIG_CHANGED_CHANNEL = "alarm-config-changed:{0}";

    // Per-team current race-header state (race time, time-to-go, leader lap, flag).
    // Written by ChannelProcessor's RedmistConsumer on every session/car patch and
    // snapshot. JSON serialized. Read by WebHub on SubscribeToTeam to seed the client.
    // No persistence semantics — value reflects the most recent RedMist update only;
    // cleared on detach.
    public const string RACE_STATE_KEY = "race-state:{0}";
    public const string RACE_STATE_CHANGES_CHANNEL = "race-state-changes:{0}";

    // Per-team race configuration / selection change pub/sub. Published by WebApi after
    // SaveRace / DeleteRace / SelectRace; consumed by ChannelProcessor's RedmistConsumer
    // to break its poll delay and re-evaluate the team's activation candidate immediately
    // instead of waiting up to RenewalInterval (30s). Payload is unused — the channel name
    // carries the only required information (which team to re-tick).
    public const string TEAM_RACE_CHANGED_CHANNEL = "team-race-changed:{0}";
}
