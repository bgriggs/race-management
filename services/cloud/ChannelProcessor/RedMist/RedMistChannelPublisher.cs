using System.Globalization;
using Channels;
using Cloud.Shared.Streaming;
using Microsoft.Extensions.Logging;
using RedMist.TimingCommon.Models;

namespace ChannelProcessor.RedMist;

/// <summary>
/// Translates RedMist's wire payloads (<see cref="CarPositionPatch"/>,
/// <see cref="SessionStatePatch"/>, <see cref="SessionState"/>) into channel-pipeline
/// publishes. Per-car values land on the <c>car-channel-values</c> stream via
/// <see cref="ICarChannelPublisher"/>; the per-team <c>RaceFlagState</c> lands on the
/// <c>team-channel-values</c> stream via <see cref="ITeamChannelPublisher"/>. Car-number
/// filtering against the team's <c>CarConfiguration</c> set is the caller's responsibility
/// — non-team cars must not reach this class.
/// </summary>
public sealed class RedMistChannelPublisher
{
    // Stable reserved-channel GUIDs from Common.ReservedChannels — kept inline because this
    // file is the single owner of RedMist-sourced publishes and there is no other consumer.
    private static readonly Guid PositionId      = Guid.Parse("4e70c2d0-d89c-4896-af7c-a286ceda9565");
    private static readonly Guid ClassPositionId = Guid.Parse("7e8153fd-7280-4bcf-a11b-2227b70daddb");
    private static readonly Guid InPitId         = Guid.Parse("da12563a-1167-4899-9956-700b0b693005");
    private static readonly Guid RaceFlagStateId = Guid.Parse("d5b2e9f4-3c1a-4e7d-9b8f-2e4f6c1d3b02");

    private readonly ICarChannelPublisher carPublisher;
    private readonly ITeamChannelPublisher teamPublisher;
    private readonly TimeProvider time;
    private readonly ILogger<RedMistChannelPublisher> logger;

    public RedMistChannelPublisher(
        ICarChannelPublisher carPublisher,
        ITeamChannelPublisher teamPublisher,
        TimeProvider time,
        ILogger<RedMistChannelPublisher> logger)
    {
        this.carPublisher = carPublisher;
        this.teamPublisher = teamPublisher;
        this.time = time;
        this.logger = logger;
    }

    /// <summary>
    /// Publishes per-car values for every patch whose <c>Number</c> is in <paramref name="teamCarNumbers"/>.
    /// Returns the number of patches that were forwarded.
    /// </summary>
    public async Task<int> PublishCarPatchesAsync(
        int teamId,
        IReadOnlySet<string> teamCarNumbers,
        IReadOnlyList<CarPositionPatch> patches,
        CancellationToken ct)
    {
        if (patches.Count == 0) return 0;
        var nowUtc = time.GetUtcNow().UtcDateTime;
        var published = 0;

        foreach (var patch in patches)
        {
            var number = patch.Number;
            if (string.IsNullOrEmpty(number) || !teamCarNumbers.Contains(number)) continue;

            var values = new List<PublishedChannelValue>(3);
            AddIfSet(values, PositionId, patch.OverallPosition, nowUtc);
            AddIfSet(values, ClassPositionId, patch.ClassPosition, nowUtc);
            AddIfSet(values, InPitId, patch.IsInPit, nowUtc);

            if (values.Count == 0) continue;

            try
            {
                await carPublisher.PublishAsync(teamId, number, values, ct);
                published++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "RedMistChannelPublisher: car publish failed for team {TeamId} car {CarNumber}", teamId, number);
            }
        }

        return published;
    }

    /// <summary>
    /// Publishes <c>RaceFlagState</c> when <paramref name="patch"/> carries a flag value
    /// recognized by <see cref="RedMistFlagMapper"/>. Returns <c>true</c> when something was
    /// published.
    /// </summary>
    public async Task<bool> PublishSessionPatchAsync(int teamId, SessionStatePatch patch, CancellationToken ct)
    {
        if (patch.CurrentFlag is not Flags flag) return false;
        var mapped = RedMistFlagMapper.Map(flag);
        if (mapped is null) return false;

        await PublishRaceFlagAsync(teamId, mapped, ct);
        return true;
    }

    /// <summary>
    /// Emits all current values for the snapshot to the channel pipeline:
    /// <c>RaceFlagState</c> (PerTeam) plus the per-car publishes for every car in
    /// <paramref name="teamCarNumbers"/> that appears in <see cref="SessionState.CarPositions"/>.
    /// Used by the consumer on every connect/reconnect (ADR-0008).
    /// </summary>
    public async Task PublishSnapshotAsync(
        int teamId,
        IReadOnlySet<string> teamCarNumbers,
        SessionState snapshot,
        CancellationToken ct)
    {
        var mapped = RedMistFlagMapper.Map(snapshot.CurrentFlag);
        if (mapped is not null)
            await PublishRaceFlagAsync(teamId, mapped, ct);

        if (snapshot.CarPositions is null || snapshot.CarPositions.Count == 0) return;

        var nowUtc = time.GetUtcNow().UtcDateTime;
        foreach (var car in snapshot.CarPositions)
        {
            var number = car.Number;
            if (string.IsNullOrEmpty(number) || !teamCarNumbers.Contains(number)) continue;

            var values = new List<PublishedChannelValue>(3);
            Add(values, PositionId, car.OverallPosition, nowUtc);
            Add(values, ClassPositionId, car.ClassPosition, nowUtc);
            Add(values, InPitId, car.IsInPit ? 1.0 : 0.0, nowUtc);

            if (values.Count == 0) continue;
            try
            {
                await carPublisher.PublishAsync(teamId, number, values, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "RedMistChannelPublisher: snapshot publish failed for team {TeamId} car {CarNumber}", teamId, number);
            }
        }
    }

    private async Task PublishRaceFlagAsync(int teamId, string mappedFlag, CancellationToken ct)
    {
        var teamValue = new TeamChannelValue
        {
            ChannelId = RaceFlagStateId,
            Value = mappedFlag,
            Timestamp = time.GetUtcNow().UtcDateTime,
        };
        try
        {
            await teamPublisher.PublishAsync(teamId, [teamValue], ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "RedMistChannelPublisher: RaceFlagState publish failed for team {TeamId}", teamId);
        }
    }

    private static void Add(List<PublishedChannelValue> sink, Guid channelId, double value, DateTime ts)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return;
        sink.Add(new PublishedChannelValue(channelId, value.ToString("R", CultureInfo.InvariantCulture), ts));
    }

    private static void Add(List<PublishedChannelValue> sink, Guid channelId, int value, DateTime ts) =>
        sink.Add(new PublishedChannelValue(channelId, value.ToString(CultureInfo.InvariantCulture), ts));

    private static void AddIfSet(List<PublishedChannelValue> sink, Guid channelId, int? value, DateTime ts)
    {
        if (value is int v) Add(sink, channelId, v, ts);
    }

    private static void AddIfSet(List<PublishedChannelValue> sink, Guid channelId, bool? value, DateTime ts)
    {
        if (value is bool v) sink.Add(new PublishedChannelValue(channelId, v ? "1" : "0", ts));
    }
}
