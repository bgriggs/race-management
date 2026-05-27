using System.Collections.Concurrent;
using System.Text.Json;
using Cloud.Shared;
using Cloud.Shared.RedMist;
using RedMist.TimingCommon.Models;
using StackExchange.Redis;

namespace ChannelProcessor.RedMist;

/// <summary>
/// Maintains the per-team <see cref="RaceStateDto"/> that drives the Race-Monitor header
/// (race time, time-to-go, leader lap, flag). Updated from every RedMist snapshot, session
/// patch, and car patch; published to Redis pub/sub for WebApi to forward over <c>WebHub</c>;
/// cached in Redis so a fresh WebHub subscriber can seed itself without waiting for the next
/// tick.
///
/// The lease coordinator guarantees a single ChannelProcessor pod writes for any given team,
/// so per-team in-memory accumulators are race-safe without distributed locking.
/// </summary>
public sealed class RedMistRaceStatePublisher
{
    // Match the rest of the RedMist Redis state (RedMistStatusWriter etc.) — JSON for human
    // debuggability; the payload is small and infrequent enough that MessagePack isn't worth
    // the friction.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
    private static readonly TimeSpan StateTtl = TimeSpan.FromHours(1);

    private readonly IConnectionMultiplexer redis;
    private readonly TimeProvider time;
    private readonly ILogger<RedMistRaceStatePublisher> logger;
    private readonly ConcurrentDictionary<int, Accumulator> state = new();

    public RedMistRaceStatePublisher(
        IConnectionMultiplexer redis,
        TimeProvider time,
        ILogger<RedMistRaceStatePublisher> logger)
    {
        this.redis = redis;
        this.time = time;
        this.logger = logger;
    }

    /// <summary>Apply a full <see cref="SessionState"/> snapshot — resets every field.</summary>
    public Task ApplySnapshotAsync(int teamId, SessionState snapshot, CancellationToken ct)
    {
        var leaderLap = ComputeLeaderLap(snapshot.CarPositions);
        var acc = state.GetOrAdd(teamId, _ => new Accumulator());
        acc.SetAll(
            eventId: snapshot.EventId,
            localTimeOfDay: snapshot.LocalTimeOfDay,
            runningRaceTime: snapshot.RunningRaceTime,
            timeToGo: snapshot.TimeToGo,
            leaderLap: leaderLap,
            flag: RedMistFlagMapper.Map(snapshot.CurrentFlag));
        return FlushAsync(teamId, acc, ct);
    }

    /// <summary>
    /// Apply a sparse <see cref="SessionStatePatch"/> — only fields present on the patch
    /// overwrite the accumulator; null fields are left alone.
    /// </summary>
    public Task ApplySessionPatchAsync(int teamId, SessionStatePatch patch, CancellationToken ct)
    {
        var mappedFlag = patch.CurrentFlag is Flags f ? RedMistFlagMapper.Map(f) : null;
        var acc = state.GetOrAdd(teamId, _ => new Accumulator());
        var changed = acc.PatchSession(
            eventId: patch.EventId,
            localTimeOfDay: patch.LocalTimeOfDay,
            runningRaceTime: patch.RunningRaceTime,
            timeToGo: patch.TimeToGo,
            flag: mappedFlag);
        return changed ? FlushAsync(teamId, acc, ct) : Task.CompletedTask;
    }

    /// <summary>Bump leader-lap if any car in the batch has completed a higher lap.</summary>
    public Task ApplyCarPatchesAsync(int teamId, IReadOnlyList<CarPositionPatch> patches, CancellationToken ct)
    {
        var maxLap = 0;
        foreach (var p in patches)
        {
            if (p.LastLapCompleted is int lap && lap > maxLap) maxLap = lap;
        }
        if (maxLap == 0) return Task.CompletedTask;

        var acc = state.GetOrAdd(teamId, _ => new Accumulator());
        if (!acc.MaybeBumpLeaderLap(maxLap)) return Task.CompletedTask;
        return FlushAsync(teamId, acc, ct);
    }

    /// <summary>
    /// Drop the team's state. Publishes an empty-string payload so subscribers can clear the UI
    /// immediately (StackExchange.Redis rejects truly null payloads on PUBLISH — empty is the
    /// "no state" sentinel; the propagator forwards a null DTO to clients), and deletes the
    /// Redis cache so a fresh subscribe sees nothing.
    /// </summary>
    public async Task ClearAsync(int teamId, CancellationToken ct)
    {
        state.TryRemove(teamId, out _);
        var db = redis.GetDatabase();
        try
        {
            await db.KeyDeleteAsync(StateKey(teamId));
            await db.PublishAsync(ChangesChannel(teamId), RedisValue.EmptyString);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clear race-state for team {TeamId}", teamId);
        }
    }

    private async Task FlushAsync(int teamId, Accumulator acc, CancellationToken ct)
    {
        var dto = acc.Snapshot(time.GetUtcNow().UtcDateTime);
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var db = redis.GetDatabase();
        try
        {
            await db.StringSetAsync(StateKey(teamId), json, StateTtl);
            await db.PublishAsync(ChangesChannel(teamId), json);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish race-state for team {TeamId}", teamId);
        }
    }

    private static int? ComputeLeaderLap(List<CarPosition>? positions)
    {
        if (positions is null || positions.Count == 0) return null;
        var max = 0;
        foreach (var car in positions)
        {
            if (car.LastLapCompleted > max) max = car.LastLapCompleted;
        }
        return max > 0 ? max : null;
    }

    private static RedisKey StateKey(int teamId) =>
        new(string.Format(Consts.RACE_STATE_KEY, teamId));

    private static RedisChannel ChangesChannel(int teamId) =>
        RedisChannel.Literal(string.Format(Consts.RACE_STATE_CHANGES_CHANNEL, teamId));

    private sealed class Accumulator
    {
        private readonly Lock gate = new();
        private int? eventId;
        private string? localTimeOfDay;
        private string? runningRaceTime;
        private string? timeToGo;
        private int? leaderLap;
        private string? flag;

        public void SetAll(int? eventId, string? localTimeOfDay, string? runningRaceTime, string? timeToGo, int? leaderLap, string? flag)
        {
            lock (gate)
            {
                this.eventId = eventId;
                this.localTimeOfDay = localTimeOfDay;
                this.runningRaceTime = runningRaceTime;
                this.timeToGo = timeToGo;
                this.leaderLap = leaderLap;
                this.flag = flag;
            }
        }

        public bool PatchSession(int? eventId, string? localTimeOfDay, string? runningRaceTime, string? timeToGo, string? flag)
        {
            lock (gate)
            {
                var changed = false;
                if (eventId is int newEvent && newEvent != this.eventId)
                {
                    this.eventId = newEvent;
                    changed = true;
                }
                if (localTimeOfDay is not null && localTimeOfDay != this.localTimeOfDay)
                {
                    this.localTimeOfDay = localTimeOfDay;
                    changed = true;
                }
                if (runningRaceTime is not null && runningRaceTime != this.runningRaceTime)
                {
                    this.runningRaceTime = runningRaceTime;
                    changed = true;
                }
                if (timeToGo is not null && timeToGo != this.timeToGo)
                {
                    this.timeToGo = timeToGo;
                    changed = true;
                }
                if (flag is not null && flag != this.flag)
                {
                    this.flag = flag;
                    changed = true;
                }
                return changed;
            }
        }

        public bool MaybeBumpLeaderLap(int candidate)
        {
            lock (gate)
            {
                if (leaderLap is int existing && existing >= candidate) return false;
                leaderLap = candidate;
                return true;
            }
        }

        public RaceStateDto Snapshot(DateTime nowUtc)
        {
            lock (gate)
            {
                return new RaceStateDto
                {
                    EventId = eventId,
                    LocalTimeOfDay = localTimeOfDay,
                    RunningRaceTime = runningRaceTime,
                    TimeToGo = timeToGo,
                    LeaderLap = leaderLap,
                    Flag = flag,
                    LastUpdatedUtc = nowUtc,
                };
            }
        }
    }
}
