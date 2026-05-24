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
}
