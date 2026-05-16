using BigMission.Shared.SignalR;
using Channels;
using Common;
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
