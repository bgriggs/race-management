using CarUpdateAgent.Configuration;
using CarUpdateAgent.Models;
using CarUpdateAgent.Services;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;

namespace CarUpdateAgent.BackgroundServices;

/// <summary>
/// Maintains an outbound SignalR connection to the cloud hub and handles
/// <c>NewVersionAvailable</c> messages by forwarding them to the update orchestrator.
/// Reconnects automatically with exponential back-off on any connection failure.
/// </summary>
public class CloudHubClient : BackgroundService
{
    private readonly IUpdateOrchestrator _orchestrator;
    private readonly UpdateAgentOptions _options;
    private readonly ILogger<CloudHubClient> _logger;

    public CloudHubClient(
        IUpdateOrchestrator orchestrator,
        IOptions<UpdateAgentOptions> options,
        ILogger<CloudHubClient> logger)
    {
        _orchestrator = orchestrator;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.CloudHubUrl))
        {
            _logger.LogWarning("CloudHubUrl is not configured. Cloud OTA notifications are disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var connection = BuildConnection();

            connection.On<UpdateInfo>("NewVersionAvailable", async info =>
            {
                _logger.LogInformation(
                    "Cloud hub: NewVersionAvailable received for version {Version}.",
                    info.Version);

                try
                {
                    await _orchestrator.StartOtaUpdateAsync(info, stoppingToken);
                }
                catch (InvalidOperationException ex)
                {
                    // Update already in progress; cloud will be notified of current state
                    // when the team polls GET /status or a future status-push is added.
                    _logger.LogWarning("Could not start OTA update: {Reason}", ex.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error starting OTA update.");
                }
            });

            connection.Closed += async ex =>
            {
                if (ex is not null)
                    _logger.LogWarning("Cloud hub connection closed with error: {Message}", ex.Message);
                else
                    _logger.LogInformation("Cloud hub connection closed.");
            };

            try
            {
                _logger.LogInformation("Connecting to cloud hub at {Url}.", _options.CloudHubUrl);
                await connection.StartAsync(stoppingToken);
                _logger.LogInformation("Connected to cloud hub.");

                // Wait until the connection drops or the host is stopping.
                await WaitForConnectionLossAsync(connection, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cloud hub connection failed. Will retry.");
            }
            finally
            {
                await connection.DisposeAsync();
            }

            if (!stoppingToken.IsCancellationRequested)
            {
                var delay = TimeSpan.FromSeconds(15);
                _logger.LogInformation("Reconnecting to cloud hub in {Delay}s.", delay.TotalSeconds);
                await Task.Delay(delay, stoppingToken);
            }
        }
    }

    private HubConnection BuildConnection() =>
        new HubConnectionBuilder()
            .WithUrl(_options.CloudHubUrl)
            .WithAutomaticReconnect()
            .Build();

    /// <summary>
    /// Returns a task that completes when the connection is no longer in the Connected state.
    /// </summary>
    private static async Task WaitForConnectionLossAsync(
        HubConnection connection,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var _ = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));

        connection.Closed += _ =>
        {
            tcs.TrySetResult();
            return Task.CompletedTask;
        };

        // If already disconnected (race), complete immediately.
        if (connection.State != HubConnectionState.Connected)
        {
            tcs.TrySetResult();
        }

        await tcs.Task;
    }
}
