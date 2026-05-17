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
    public const string CAR_CHANNEL_STATE_KEY = "car-channels:{0}";
    public const string CAR_CHANNEL_CHANGES_CHANNEL = "car-channel-changes:{0}";
}
