using Cloud.Shared;
using Cloud.Shared.RedMist;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using RedMist.TimingCommon.Models;
using StackExchange.Redis;
using System.Collections.Concurrent;

namespace ChannelProcessor.RedMist;

/// <summary>
/// Fourth hosted worker in ChannelProcessor (ADR-0008). Per-team SETNX lease coordinator
/// that owns the RedMist hub subscription for each leased team.
///
/// On lease acquisition: loads the team's car-number set from <c>CarConfiguration</c>,
/// pulls the initial <see cref="SessionState"/> via REST, publishes the snapshot through
/// the channel pipeline, then opens the SignalR hub and subscribes to the event.
/// Inbound <see cref="CarPositionPatch"/> and <see cref="SessionStatePatch"/> messages are
/// filtered to team cars and translated to channel publishes by <see cref="RedMistChannelPublisher"/>.
/// On <c>ReceiveReset</c> or hub reconnect: re-runs the REST snapshot.
/// </summary>
public sealed class RedMistConsumerWorker : BackgroundService
{
    private readonly RedMistLeaseManager leases;
    private readonly RedMistActivationEvaluator activation;
    private readonly RedMistStatusWriter status;
    private readonly RedMistChannelPublisher channelPublisher;
    private readonly RedMistRaceStatePublisher raceState;
    private readonly IRedMistRestClient rest;
    private readonly IRedMistTokenProvider tokens;
    private readonly IConnectionMultiplexer redis;
    private readonly IOptions<RedMistOptions> options;
    private readonly ILoggerFactory loggerFactory;
    private readonly TimeProvider time;
    private readonly ILogger<RedMistConsumerWorker> logger;

    private readonly string podToken = $"{Environment.MachineName}-{Guid.NewGuid():N}";
    private readonly ConcurrentDictionary<int, TeamSession> held = new();
    private readonly ConcurrentDictionary<int, RedMistConnectionState> lastStatus = new();

    // Cancellation source that wraps the per-iteration tick delay. Replaced each iteration;
    // pub/sub callbacks cancel the current one to wake the loop early when a Race row or
    // team selection changes. Volatile because the pub/sub callback thread reads while
    // the worker thread writes.
    private volatile CancellationTokenSource? tickWaitCts;

    // Per-team live-events cache for the org→event resolution path. RedMist's live-event
    // set changes on the order of minutes (events go live/end), so a short TTL keeps us
    // close to "instant" without hammering the API across many teams. Holds the most recent
    // successful fetch — failed fetches don't overwrite, so transient outages don't
    // immediately blank the header.
    private static readonly TimeSpan LiveEventsCacheTtl = TimeSpan.FromSeconds(60);
    private readonly ConcurrentDictionary<int, (DateTime FetchedAtUtc, IReadOnlyList<EventListSummary> Events)> liveEventsCache = new();

    public RedMistConsumerWorker(
        RedMistLeaseManager leases,
        RedMistActivationEvaluator activation,
        RedMistStatusWriter status,
        RedMistChannelPublisher channelPublisher,
        RedMistRaceStatePublisher raceState,
        IRedMistRestClient rest,
        IRedMistTokenProvider tokens,
        IConnectionMultiplexer redis,
        IOptions<RedMistOptions> options,
        ILoggerFactory loggerFactory,
        TimeProvider time,
        ILogger<RedMistConsumerWorker> logger)
    {
        this.leases = leases;
        this.activation = activation;
        this.status = status;
        this.channelPublisher = channelPublisher;
        this.raceState = raceState;
        this.rest = rest;
        this.tokens = tokens;
        this.redis = redis;
        this.options = options;
        this.loggerFactory = loggerFactory;
        this.time = time;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("RedMistConsumerWorker started (pod token: {Token})", podToken);

        // WebApi publishes here after SaveRace / DeleteRace / SelectRace. We cancel the
        // current tick-delay so the next iteration evaluates immediately instead of
        // waiting up to RenewalInterval. The worker re-checks all teams on each tick;
        // we don't need to read the teamId off the pub/sub message.
        var sub = redis.GetSubscriber();
        var pattern = RedisChannel.Pattern(string.Format(Consts.TEAM_RACE_CHANGED_CHANNEL, "*"));
        await sub.SubscribeAsync(pattern, (channel, _) =>
        {
            try
            {
                tickWaitCts?.Cancel();
                logger.LogDebug("Team-race-changed received on '{Channel}'; waking worker for immediate tick", channel);
            }
            catch (ObjectDisposedException) { /* tickWaitCts was disposed between read and Cancel — benign */ }
        });

        await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(0, 5000)), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "RedMistConsumerWorker tick failed; will retry");
            }

            // Combine the worker's cancellation token with a per-iteration "wake me up early"
            // token. Either source cancellation breaks the delay; we differentiate via the
            // outer stoppingToken check.
            using var iterCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            tickWaitCts = iterCts;
            try { await Task.Delay(RedMistLeaseManager.RenewalInterval, iterCts.Token); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (OperationCanceledException) { /* woken early by pub/sub */ }
            finally { tickWaitCts = null; }
        }

        await sub.UnsubscribeAsync(pattern);
        await ReleaseAllAsync(CancellationToken.None);
        logger.LogInformation("RedMistConsumerWorker stopped");
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var nowUtc = time.GetUtcNow().UtcDateTime;
        var candidates = await activation.ListCandidateTeamsAsync(ct);
        var candidateSet = candidates.ToHashSet();

        // Teams that no longer appear as candidates lost their paired race entirely.
        foreach (var teamId in held.Keys.Where(t => !candidateSet.Contains(t)).ToList())
        {
            await DetachAsync(teamId, "no paired Race", ct);
        }

        foreach (var teamId in candidates)
        {
            var candidate = await activation.SelectCandidateAsync(teamId, nowUtc, ct);
            // Org-only Races carry the org id but no event id; resolve to a currently-live
            // event before the lease decision so Decide and AttachAsync can assume the
            // candidate has a concrete event id. Resolution failure (no live event for the
            // org) collapses the candidate to null — Decide then treats it as "no paired".
            candidate = await ResolveOrgToEventAsync(teamId, candidate, ct);
            held.TryGetValue(teamId, out var session);
            var action = RedMistLeaseDecision.Decide(
                currentlyHeld: session is not null,
                heldEventId: session?.RedMistEventId,
                candidate);

            switch (action)
            {
                case LeaseAction.PublishNoEventPaired:
                    await PublishStatusIfChangedAsync(teamId, RedMistConnectionState.NoEventPaired, eventId: null, detail: null, ct);
                    break;

                case LeaseAction.DetachNoCandidate:
                    await DetachAsync(teamId, "no paired Race", ct);
                    break;

                case LeaseAction.DetachOutOfWindow:
                    await DetachAsync(teamId, "race outside window", ct);
                    break;

                case LeaseAction.DetachEventChanged:
                    await DetachAsync(teamId, "paired event changed", ct);
                    break;

                case LeaseAction.Renew:
                    if (!await leases.TryRenewAsync(teamId, podToken, ct))
                    {
                        logger.LogInformation("Lost RedMist lease for team {TeamId} on renewal", teamId);
                        await session!.DisposeAsync();
                        held.TryRemove(teamId, out _);
                        await status.WriteAsync(teamId, RedMistConnectionState.Disconnected, candidate!.RedMistEventId, "lease lost", ct);
                    }
                    break;

                case LeaseAction.TryAcquire:
                    if (await leases.TryAcquireAsync(teamId, podToken, ct))
                        await AttachAsync(teamId, candidate!, ct);
                    break;
            }
        }
    }

    private async Task AttachAsync(int teamId, ActivationCandidate candidate, CancellationToken ct)
    {
        logger.LogInformation("Acquired RedMist lease for team {TeamId}, race {RaceId}, event {EventId}",
            teamId, candidate.RaceId, candidate.RedMistEventId);

        // The Race opted in to RedMist by carrying RedMistEventId, so missing credentials
        // here are a misconfiguration the engineer needs to see — surface as AuthFailed and
        // release the lease rather than spinning the hub against a null token.
        if (await tokens.GetAccessTokenAsync(teamId, ct) is null)
        {
            logger.LogWarning("RedMist credentials are not configured for team {TeamId}; releasing lease", teamId);
            await status.WriteAsync(teamId, RedMistConnectionState.AuthFailed, candidate.RedMistEventId,
                "RedMist credentials are not configured for this team.", ct);
            await leases.ReleaseAsync(teamId, podToken, ct);
            return;
        }

        await status.WriteAsync(teamId, RedMistConnectionState.Connecting, candidate.RedMistEventId, detail: null, ct);

        IReadOnlySet<string> carNumbers;
        try
        {
            carNumbers = await activation.LoadTeamCarNumbersAsync(teamId, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load CarConfiguration for team {TeamId}; releasing lease", teamId);
            await leases.ReleaseAsync(teamId, podToken, ct);
            await status.WriteAsync(teamId, RedMistConnectionState.Disconnected, candidate.RedMistEventId, ex.Message, ct);
            return;
        }

        // RedMistEventId is non-null here — ResolveOrgToEventAsync guarantees it before
        // any candidate reaches AttachAsync (org-only candidates that can't resolve are
        // collapsed to null in TickAsync).
        var session = new TeamSession(teamId, candidate.RedMistEventId!.Value, carNumbers);
        held[teamId] = session;

        if (!await ResyncSnapshotAsync(session, ct))
        {
            // Status was set inside ResyncSnapshotAsync; lease has already been released on auth-failure.
            // For transport failures we keep the lease so the next tick retries.
            return;
        }

        try
        {
            var hub = new RedMistHubClient(
                teamId,
                candidate.RedMistEventId!.Value,
                candidate.RedMistAccessCode,
                tokens,
                options.Value,
                loggerFactory.CreateLogger($"RedMistHubClient[team={teamId},event={candidate.RedMistEventId}]"));
            hub.CarPatchesReceived += patches => OnCarPatches(session, patches);
            hub.SessionPatchReceived += patch => OnSessionPatch(session, patch);
            hub.ResetReceived += () => OnReset(session);
            hub.StateChanged += (state, ex) => OnHubStateChanged(session, state, ex);

            await hub.StartAsync(ct);
            session.Hub = hub;
            await status.WriteAsync(teamId, RedMistConnectionState.Connected, candidate.RedMistEventId, detail: null, ct);
        }
        catch (RedMistAuthException ex)
        {
            logger.LogWarning(ex, "RedMist hub start failed (auth) for team {TeamId}; releasing lease", teamId);
            await status.WriteAsync(teamId, RedMistConnectionState.AuthFailed, candidate.RedMistEventId, ex.Message, ct);
            await leases.ReleaseAsync(teamId, podToken, ct);
            held.TryRemove(teamId, out _);
            await raceState.ClearAsync(teamId, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RedMist hub start failed for team {TeamId}, event {EventId}; will retry next tick",
                teamId, candidate.RedMistEventId);
            await status.WriteAsync(teamId, RedMistConnectionState.Reconnecting, candidate.RedMistEventId, ex.Message, ct);
            // Keep the lease — next tick re-evaluates and will try AttachAsync again.
            held.TryRemove(teamId, out _);
            await raceState.ClearAsync(teamId, ct);
        }
    }

    /// <summary>
    /// Calls <c>GetCurrentSessionState</c> and translates the snapshot into channel
    /// publishes. Returns <c>false</c> when auth failed (lease released, status written);
    /// <c>true</c> on success or any transient failure (caller decides whether to retry).
    /// </summary>
    private async Task<bool> ResyncSnapshotAsync(TeamSession session, CancellationToken ct)
    {
        try
        {
            var snapshot = await rest.GetCurrentSessionStateAsync(session.TeamId, session.RedMistEventId, ct);
            if (snapshot is null)
            {
                logger.LogInformation("RedMist returned no current session state for team {TeamId}, event {EventId}",
                    session.TeamId, session.RedMistEventId);
                return true;
            }

            if (session.LastSessionId is int prev && prev != snapshot.SessionId)
            {
                logger.LogInformation("RedMist active session changed for team {TeamId}: {Prev} → {Next} ('{Name}')",
                    session.TeamId, prev, snapshot.SessionId, snapshot.SessionName);
            }
            session.LastSessionId = snapshot.SessionId;

            await channelPublisher.PublishSnapshotAsync(session.TeamId, session.CarNumbers, snapshot, ct);
            await raceState.ApplySnapshotAsync(session.TeamId, snapshot, ct);
            logger.LogInformation("RedMist sync for team {TeamId}, event {EventId}: session {SessionId} '{SessionName}', {CarCount} cars",
                session.TeamId, session.RedMistEventId, snapshot.SessionId, snapshot.SessionName, snapshot.CarPositions?.Count ?? 0);
            return true;
        }
        catch (RedMistAuthException ex)
        {
            logger.LogWarning(ex, "RedMist sync auth-failed for team {TeamId}; releasing lease", session.TeamId);
            await status.WriteAsync(session.TeamId, RedMistConnectionState.AuthFailed, session.RedMistEventId, ex.Message, ct);
            await leases.ReleaseAsync(session.TeamId, podToken, ct);
            held.TryRemove(session.TeamId, out _);
            await session.DisposeAsync();
            await raceState.ClearAsync(session.TeamId, ct);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RedMist sync failed for team {TeamId}, event {EventId}", session.TeamId, session.RedMistEventId);
            await status.WriteAsync(session.TeamId, RedMistConnectionState.Reconnecting, session.RedMistEventId, ex.Message, ct);
            return true;
        }
    }

    private void OnCarPatches(TeamSession session, CarPositionPatch[] patches)
    {
        if (patches.Length == 0) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await channelPublisher.PublishCarPatchesAsync(session.TeamId, session.CarNumbers, patches, CancellationToken.None);
                // Race-state's leader-lap is computed across ALL cars in the event, not just
                // the team's cars — pass the unfiltered batch.
                await raceState.ApplyCarPatchesAsync(session.TeamId, patches, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "RedMist car-patch publish failed for team {TeamId}, event {EventId}",
                    session.TeamId, session.RedMistEventId);
            }
        });
    }

    private void OnSessionPatch(TeamSession session, SessionStatePatch patch)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                // Track session-change so the next snapshot fetch can reset downstream state.
                if (patch.SessionId is int newSession && session.LastSessionId is int prev && prev != newSession)
                {
                    logger.LogInformation("RedMist session change detected via patch for team {TeamId}: {Prev} → {Next}",
                        session.TeamId, prev, newSession);
                    session.LastSessionId = newSession;
                    // Re-run snapshot — gives downstream a fresh full state for the new session.
                    await ResyncSnapshotAsync(session, CancellationToken.None);
                }

                await channelPublisher.PublishSessionPatchAsync(session.TeamId, patch, CancellationToken.None);
                await raceState.ApplySessionPatchAsync(session.TeamId, patch, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "RedMist session-patch publish failed for team {TeamId}, event {EventId}",
                    session.TeamId, session.RedMistEventId);
            }
        });
    }

    private void OnReset(TeamSession session)
    {
        _ = Task.Run(async () =>
        {
            logger.LogInformation("RedMist ReceiveReset for team {TeamId}, event {EventId}; re-running snapshot",
                session.TeamId, session.RedMistEventId);
            await ResyncSnapshotAsync(session, CancellationToken.None);
        });
    }

    private void OnHubStateChanged(TeamSession session, HubConnectionState state, Exception? ex)
    {
        var mapped = state switch
        {
            HubConnectionState.Connected => RedMistConnectionState.Connected,
            HubConnectionState.Reconnecting => RedMistConnectionState.Reconnecting,
            HubConnectionState.Disconnected => RedMistConnectionState.Disconnected,
            _ => RedMistConnectionState.Reconnecting,
        };
        _ = Task.Run(async () =>
        {
            try { await status.WriteAsync(session.TeamId, mapped, session.RedMistEventId, ex?.Message, CancellationToken.None); }
            catch (Exception writeEx) { logger.LogWarning(writeEx, "Status write failed for team {TeamId}", session.TeamId); }
        });
    }

    private async Task DetachAsync(int teamId, string reason, CancellationToken ct)
    {
        if (!held.TryRemove(teamId, out var session))
            return;

        logger.LogInformation("Releasing RedMist lease for team {TeamId} ({Reason})", teamId, reason);
        await session.DisposeAsync();
        await leases.ReleaseAsync(teamId, podToken, ct);
        await status.WriteAsync(teamId, RedMistConnectionState.Disconnected, session.RedMistEventId, reason, ct);
        // Wipe the race-state cache so any client subscribing now sees blanks instead of
        // stale values from the prior session.
        await raceState.ClearAsync(teamId, ct);
    }

    private async Task ReleaseAllAsync(CancellationToken ct)
    {
        foreach (var (teamId, _) in held.ToArray())
        {
            try { await DetachAsync(teamId, "shutdown", ct); }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to release RedMist lease for team {TeamId} on shutdown", teamId); }
        }
    }

    private async Task PublishStatusIfChangedAsync(int teamId, RedMistConnectionState state, int? eventId, string? detail, CancellationToken ct)
    {
        if (lastStatus.TryGetValue(teamId, out var prev) && prev == state) return;
        lastStatus[teamId] = state;
        await status.WriteAsync(teamId, state, eventId, detail, ct);
    }

    /// <summary>
    /// For an org-only candidate (Race has <c>RedMistOrganizationId</c> but no
    /// <c>RedMistEventId</c>), looks up the org's currently-live event via RedMist and returns
    /// a copy of the candidate with <c>RedMistEventId</c> populated. Returns the candidate
    /// unchanged when an event id is already present, or <c>null</c> when no live event exists
    /// for the org (the org isn't running anything right now) or when the lookup itself
    /// failed — both cases collapse to "no candidate this tick", so the lease isn't acquired
    /// and the UI header stays blank for the team.
    /// </summary>
    private async Task<ActivationCandidate?> ResolveOrgToEventAsync(int teamId, ActivationCandidate? candidate, CancellationToken ct)
    {
        if (candidate is null) return null;
        if (candidate.RedMistEventId is not null) return candidate;
        if (candidate.RedMistOrganizationId is not int orgId) return null;

        try
        {
            var live = await GetCachedLiveEventsAsync(teamId, ct);
            // First match wins. Teams typically run with one org, but if the org happens to
            // have multiple live events at once (rare), we'd need a more specific rule from
            // the engineer — flagging the ambiguity in the log so it's not silent.
            var matches = live.Where(e => e.OrganizationId == orgId && e.IsLive).ToList();
            if (matches.Count == 0)
            {
                logger.LogDebug("No live RedMist event for team {TeamId} org {OrgId}; skipping attach", teamId, orgId);
                return null;
            }
            if (matches.Count > 1)
            {
                logger.LogWarning("Multiple live RedMist events for team {TeamId} org {OrgId}: {Ids}; picking first",
                    teamId, orgId, string.Join(",", matches.Select(m => m.Id)));
            }
            var resolved = matches[0];
            logger.LogInformation("Resolved team {TeamId} org {OrgId} → live event {EventId} ('{EventName}')",
                teamId, orgId, resolved.Id, resolved.EventName);
            return candidate with { RedMistEventId = resolved.Id };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to resolve org→event for team {TeamId} org {OrgId}", teamId, orgId);
            return null;
        }
    }

    private async Task<IReadOnlyList<EventListSummary>> GetCachedLiveEventsAsync(int teamId, CancellationToken ct)
    {
        var nowUtc = time.GetUtcNow().UtcDateTime;
        if (liveEventsCache.TryGetValue(teamId, out var cached) && nowUtc - cached.FetchedAtUtc < LiveEventsCacheTtl)
            return cached.Events;

        var fetched = await rest.LoadLiveEventsAsync(teamId, ct);
        liveEventsCache[teamId] = (nowUtc, fetched);
        return fetched;
    }

    private sealed class TeamSession : IAsyncDisposable
    {
        public int TeamId { get; }
        public int RedMistEventId { get; }
        public IReadOnlySet<string> CarNumbers { get; }
        public RedMistHubClient? Hub { get; set; }
        public int? LastSessionId { get; set; }

        public TeamSession(int teamId, int redMistEventId, IReadOnlySet<string> carNumbers)
        {
            TeamId = teamId;
            RedMistEventId = redMistEventId;
            CarNumbers = carNumbers;
        }

        public async ValueTask DisposeAsync()
        {
            if (Hub is not null)
            {
                await Hub.DisposeAsync();
                Hub = null;
            }
        }
    }
}
