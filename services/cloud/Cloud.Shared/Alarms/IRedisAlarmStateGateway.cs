using Channels.Alarms;

namespace Cloud.Shared.Alarms;

/// <summary>
/// Shared Redis access for <see cref="AlarmState"/>. Both ChannelProcessor (per-tick
/// reads/writes during evaluation) and WebApi (acknowledge endpoint) go through this
/// gateway so the JSON shape and key format cannot drift between the two sides.
/// </summary>
public interface IRedisAlarmStateGateway
{
    Task<AlarmState> GetAsync(string carKey, Guid alarmId, CancellationToken ct = default);

    Task SetAsync(string carKey, AlarmState state, CancellationToken ct = default);

    /// <summary>
    /// Read-modify-write that sets <see cref="AlarmState.IsAcknowledged"/> to true and
    /// <see cref="AlarmState.LastAcknowledgedTimestamp"/> to <paramref name="utcNow"/>.
    /// Returns the post-ack state. Idempotent: a re-ack on an already-acked alarm leaves
    /// the existing <c>LastAcknowledgedTimestamp</c> unchanged.
    /// </summary>
    Task<AlarmState> AcknowledgeAsync(string carKey, Guid alarmId, DateTime utcNow, CancellationToken ct = default);
}
