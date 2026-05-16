using BigMission.Shared.Auth;
using BigMission.Shared.SignalR;
using Channels;
using Common;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;

namespace Racecar.Clients;

public interface ICloudClient
{
    /// <summary>Raised when the hub connection state changes.</summary>
    event Action<HubConnectionState>? ConnectionStatusChanged;

    Task SendChannelValuesAsync(ChannelValue[] channelValues);
}

public class CloudClient : HubClientBase, ICloudClient
{
    private readonly ILogger logger;
    private HubConnection? hub;
    private CancellationToken stoppingToken;
    private DateTime? unhealthySince;
    private readonly SemaphoreSlim reconnectSemaphore = new(1, 1);
    private static readonly TimeSpan StuckConnectionThreshold = TimeSpan.FromSeconds(30);

    private CarConfiguration carConfiguration;
    private readonly IDisposable? configChangeListener;
    private readonly IConfiguration configuration;

    public CloudClient(ILoggerFactory loggerFactory, IConfiguration configuration, IOptionsMonitor<CarConfiguration> carConfig)
        : base(loggerFactory, configuration)
    {
        logger = loggerFactory.CreateLogger(GetType().Name);
        carConfiguration = carConfig.CurrentValue;

        configChangeListener = carConfig.OnChange(cfg =>
        {
            try
            {
                carConfiguration = cfg;
                _ = TryRestartConnectionAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to reload hub configuration.");
            }
        });
        this.configuration = configuration;
    }


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.stoppingToken = stoppingToken;
        hub = StartConnection(stoppingToken);
        hub.On("SendCarConfiguration", () => carConfiguration);

        while (!stoppingToken.IsCancellationRequested)
        {
            var h = hub;
            if (h != null)
            {
                FireStatusUpdate(h);
            }
            await CheckConnectionHealthAsync();
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    protected override string GetClientId() => carConfiguration.ClientId;
    protected override string GetClientSecret() => carConfiguration.ClientSecret;

    protected override HubConnection GetConnection()
    {
        var hubUrl = configuration["Server:StatusHub"] ?? throw new InvalidOperationException("Hub URL is not configured.");
        var authUrl = configuration["Keycloak:AuthServerUrl"] ?? throw new InvalidOperationException("Keycloak URL is not configured.");
        var realm = configuration["Keycloak:Realm"] ?? throw new InvalidOperationException("Keycloak realm is not configured.");

        var builder = new HubConnectionBuilder().WithUrl(hubUrl, delegate (HttpConnectionOptions options)
        {
            // Skip negotiate to connect directly via WebSocket without a connection ID.
            // This avoids 404 errors in multi-replica deployments where negotiate and
            // WebSocket upgrade may hit different pods.
            options.SkipNegotiation = true;
            options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
            options.AccessTokenProvider = async delegate
            {
                try
                {
                    var clientId = GetClientId();
                    var clientSecret = GetClientSecret();
                    return await KeycloakServiceToken.RequestClientToken(authUrl, realm, clientId, clientSecret);
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Failed to get server hub access token");
                    return null;
                }
            };
        })
        .WithAutomaticReconnect(new InfiniteRetryPolicy())
        .TryAddMessagePack();

        var hubConnection = builder.Build();

        InitializeStateLogging(hubConnection);
        return hubConnection;
    }

    /// <summary>
    /// Detects when the hub connection is stuck in Connecting or Reconnecting state
    /// beyond the threshold and forces a connection restart.
    /// </summary>
    private async Task CheckConnectionHealthAsync()
    {
        if (hub is null) return;

        var state = hub.State;
        if (state == HubConnectionState.Connected || state == HubConnectionState.Disconnected)
        {
            unhealthySince = null;
            return;
        }

        // State is Connecting or Reconnecting
        unhealthySince ??= DateTime.UtcNow;
        var elapsed = DateTime.UtcNow - unhealthySince.Value;

        if (elapsed >= StuckConnectionThreshold)
        {
            logger.LogWarning("Hub connection stuck in {State} state for {Elapsed:F0}s, forcing restart", state, elapsed.TotalSeconds);
            unhealthySince = null;
            await TryRestartConnectionAsync();
        }
    }

    /// <summary>
    /// Attempts to restart the hub connection when InvalidOperationException occurs.
    /// </summary>
    private async Task<bool> TryRestartConnectionAsync()
    {
        if (!await reconnectSemaphore.WaitAsync(100)) // Prevent concurrent reconnection attempts
        {
            return false;
        }

        try
        {
            logger.LogWarning("Attempting to restart hub connection");

            // Dispose current connection
            if (hub != null)
            {
                try
                {
                    await hub.DisposeAsync();
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Error disposing hub connection during restart");
                }
            }

            // Create new connection
            hub = StartConnection(stoppingToken);
            if (hub != null)
            {
                hub.On("SendCarConfiguration", () => carConfiguration);
                logger.LogInformation("Hub connection restarted successfully");
                return true;
            }

            logger.LogError("Failed to restart hub connection - StartConnection returned null");
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to restart hub connection");
            return false;
        }
        finally
        {
            reconnectSemaphore.Release();
        }
    }

    #region Cloud Calls

    public async Task SendChannelValuesAsync(ChannelValue[] channelValues)
    {
        var h = hub;
        if (h != null)
        {
            await h.InvokeAsync("SendChannelValuesAsync", channelValues);
        }
    }

    #endregion
}
