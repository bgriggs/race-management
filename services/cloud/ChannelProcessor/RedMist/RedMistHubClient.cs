using Cloud.Shared.RedMist;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using RedMist.TimingCommon.Models;

namespace ChannelProcessor.RedMist;

/// <summary>
/// Holds a SignalR connection to RedMist's <c>StatusHub</c> for one (team, event) and
/// surfaces inbound messages as events. Mirrors the protocol the sample
/// <c>StatusSubscriptionClient</c> uses but is parameterised per-team so multiple teams
/// can share one process. Mirrors the sample's transport choices: WebSockets only with
/// <c>SkipNegotiation = true</c> so a reconnecting client doesn't get caught by a
/// negotiate/upgrade landing on different replicas of RedMist's hub.
/// </summary>
public sealed class RedMistHubClient : IAsyncDisposable
{
    private readonly int teamId;
    private readonly int eventId;
    private readonly IRedMistTokenProvider tokens;
    private readonly RedMistOptions options;
    private readonly ILogger logger;
    private HubConnection? hub;

    public RedMistHubClient(
        int teamId,
        int eventId,
        IRedMistTokenProvider tokens,
        RedMistOptions options,
        ILogger logger)
    {
        this.teamId = teamId;
        this.eventId = eventId;
        this.tokens = tokens;
        this.options = options;
        this.logger = logger;
    }

    /// <summary>Latest <see cref="CarPositionPatch"/> batch from the hub.</summary>
    public event Action<CarPositionPatch[]>? CarPatchesReceived;

    /// <summary>Latest <see cref="SessionStatePatch"/> from the hub.</summary>
    public event Action<SessionStatePatch>? SessionPatchReceived;

    /// <summary>RedMist signaled a session reset; consumer should re-run the REST snapshot.</summary>
    public event Action? ResetReceived;

    /// <summary>Connection state transitions (Connected / Reconnecting / Closed).</summary>
    public event Action<HubConnectionState, Exception?>? StateChanged;

    /// <summary>
    /// Opens the connection and subscribes to the event. Returns when the initial
    /// <c>SubscribeToEventV2</c> server invocation has completed.
    /// </summary>
    public async Task StartAsync(CancellationToken ct)
    {
        if (hub is not null)
            throw new InvalidOperationException("RedMistHubClient is already started.");

        hub = new HubConnectionBuilder()
            .WithUrl(options.HubUrl, ConfigureHttp)
            .WithAutomaticReconnect(new RedMistInfiniteRetryPolicy())
            .Build();

        hub.On<CarPositionPatch[]>("ReceiveCarPatches", patches =>
        {
            try { CarPatchesReceived?.Invoke(patches); }
            catch (Exception ex) { logger.LogError(ex, "ReceiveCarPatches handler threw"); }
        });
        hub.On<SessionStatePatch>("ReceiveSessionPatch", patch =>
        {
            try { SessionPatchReceived?.Invoke(patch); }
            catch (Exception ex) { logger.LogError(ex, "ReceiveSessionPatch handler threw"); }
        });
        hub.On("ReceiveReset", () =>
        {
            try { ResetReceived?.Invoke(); }
            catch (Exception ex) { logger.LogError(ex, "ReceiveReset handler threw"); }
        });

        hub.Reconnecting += ex =>
        {
            logger.LogInformation("RedMist hub reconnecting for team {TeamId}, event {EventId}: {Reason}",
                teamId, eventId, ex?.Message ?? "(no exception)");
            StateChanged?.Invoke(HubConnectionState.Reconnecting, ex);
            return Task.CompletedTask;
        };
        hub.Reconnected += async _ =>
        {
            logger.LogInformation("RedMist hub reconnected for team {TeamId}, event {EventId}; re-subscribing", teamId, eventId);
            StateChanged?.Invoke(HubConnectionState.Connected, null);
            try
            {
                await hub.InvokeAsync("SubscribeToEventV2", eventId, (string?)null);
                // After a reconnect there is no guarantee of state continuity — surface a reset so
                // the consumer re-runs the REST snapshot and resyncs derived state.
                ResetReceived?.Invoke();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Re-subscribe failed after RedMist hub reconnect for team {TeamId}, event {EventId}", teamId, eventId);
            }
        };
        hub.Closed += ex =>
        {
            if (ex is null)
                logger.LogInformation("RedMist hub closed cleanly for team {TeamId}, event {EventId}", teamId, eventId);
            else
                logger.LogWarning(ex, "RedMist hub closed with exception for team {TeamId}, event {EventId}", teamId, eventId);
            StateChanged?.Invoke(HubConnectionState.Disconnected, ex);
            return Task.CompletedTask;
        };

        await hub.StartAsync(ct);
        StateChanged?.Invoke(HubConnectionState.Connected, null);
        await hub.InvokeAsync("SubscribeToEventV2", eventId, (string?)null, ct);
        logger.LogInformation("Subscribed to RedMist event {EventId} for team {TeamId}", eventId, teamId);
    }

    private void ConfigureHttp(HttpConnectionOptions httpOptions)
    {
        // Sample StatusSubscriptionClient: SkipNegotiation=true, WebSockets only.
        httpOptions.SkipNegotiation = true;
        httpOptions.Transports = HttpTransportType.WebSockets;
        httpOptions.AccessTokenProvider = async () =>
        {
            try
            {
                return await tokens.GetAccessTokenAsync(teamId, CancellationToken.None);
            }
            catch (RedMistAuthException ex)
            {
                logger.LogWarning(ex, "RedMist token acquisition for hub failed for team {TeamId}", teamId);
                return null; // forces the SignalR connection to fail and bubble up to the consumer
            }
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (hub is null) return;
        try
        {
            // Best-effort unsubscribe; ignore failures on a connection that's already gone.
            if (hub.State == HubConnectionState.Connected)
            {
                try { await hub.InvokeAsync("UnsubscribeFromEventV2", eventId); }
                catch (Exception ex) { logger.LogDebug(ex, "UnsubscribeFromEventV2 failed during dispose"); }
            }
            await hub.DisposeAsync();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "RedMistHubClient.DisposeAsync swallowed exception");
        }
        finally
        {
            hub = null;
        }
    }
}
