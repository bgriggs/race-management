using Channels.Alarms;

namespace ChannelProcessor.Alarms.Config;

/// <summary>
/// Loads the effective alarm definition set for a car — team-level definitions plus the
/// car's own car-level definitions, both of which evaluate independently. Save is a real
/// EF write but has no caller in this slice; the WebApi follow-up wires CRUD endpoints
/// against it.
/// </summary>
public interface IAlarmDefinitionRepository
{
    /// <summary>
    /// Returns every <see cref="AlarmDefinition"/> applicable to the given car: rows where
    /// <c>CarNumber</c> is null (team-wide) plus rows whose <c>CarNumber</c> matches.
    /// Returns an empty list when no definitions exist.
    /// </summary>
    Task<IReadOnlyList<AlarmDefinition>> GetForCarAsync(int teamId, string carNumber, CancellationToken ct = default);

    /// <summary>
    /// Upserts a single alarm definition. <paramref name="carNumber"/> null marks the row
    /// as team-level. Implementation is exercised by the WebApi CRUD follow-up.
    /// </summary>
    Task SaveAsync(int teamId, string? carNumber, AlarmDefinition definition, CancellationToken ct = default);

    /// <summary>
    /// Evicts every cached alarm-definition set for the given team. Called by the
    /// <c>AlarmConfigChangeListener</c> on receipt of an <c>alarm-config-changed:{teamId}</c>
    /// pub/sub message from WebApi after a definition save or delete.
    /// </summary>
    Task InvalidateTeamAsync(int teamId, CancellationToken ct = default);
}
