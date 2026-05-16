using Common;
using Microsoft.Extensions.Options;
using NLog;
using NLog.Web;
using Racecar.CanBus;
using Racecar.Clients;
using Racecar.Logging;
using Racecar.Pipeline;
using Racecar.Services;

namespace Racecar;

public class Program
{
    public static void Main(string[] args)
    {
        // Register the custom target type before NLog loads its config from appsettings.
        LogManager.Setup().SetupExtensions(e => e.RegisterTarget<LogBroadcastTarget>("LogBroadcast"));

        var builder = WebApplication.CreateBuilder(args);

        // Load car configuration from config.json and watch for changes written by PostConfig.
        // IOptionsMonitor<CarConfiguration> is available to any service that needs the config.
        builder.Configuration.AddJsonFile("config.json", optional: true, reloadOnChange: true);
        builder.Services.Configure<CarConfiguration>(builder.Configuration);

        builder.Logging.ClearProviders();
        builder.Host.UseNLog();

        builder.Services.AddControllers();
        builder.Services.AddOpenApi();
        builder.Services.AddHealthChecks();

        builder.Services.AddSingleton<ICanBusFactory, CanBusFactory>();
        builder.Services.AddSingleton<LogBroadcaster>();
        builder.Services.AddHostedService<AvahiDiscoveryService>();

        // Pipeline infrastructure.
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<ChannelStatusState>();
        builder.Services.AddSingleton<ActiveConfigurationFactory>();
        builder.Services.AddSingleton<RacecarPipelineHost>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<RacecarPipelineHost>());
        builder.Services.AddHostedService<TestChannelValues>();

        // Pipeline startup: build initial ActiveConfiguration and attach CAN buses.
        builder.Services.AddHostedService<PipelineStartupService>();

        builder.Services.AddSingleton<CloudClient>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<CloudClient>());

        var app = builder.Build();

        // Wire the DI-owned broadcaster into the NLog target now that the container is built.
        if (LogManager.Configuration?.FindTargetByName<LogBroadcastTarget>("broadcast") is { } broadcastTarget)
            broadcastTarget.Broadcaster = app.Services.GetRequiredService<LogBroadcaster>();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.MapHealthChecks("/health/startup");
        app.MapHealthChecks("/health/live");

        app.MapControllers();

        app.Run();
    }
}

class TestChannelValues : BackgroundService
{
    private readonly ChannelStatusState channelStatus;
    private readonly ILogger<TestChannelValues> logger;

    public TestChannelValues(ChannelStatusState channelStatus, ILogger<TestChannelValues> logger)
    {
        this.channelStatus = channelStatus;
        this.logger = logger;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach(var ch in channelStatus.Snapshot())
            {
                logger.LogInformation("Channel {ChannelId} value: {Value}", ch.Key, ch.Value.Value);
            }
            await Task.Delay(1000, stoppingToken);
        }
    }
}


/// <summary>
/// Builds the initial <see cref="ActiveConfiguration"/> from the loaded
/// <see cref="CarConfiguration"/> and attaches each enabled CAN bus to the
/// pipeline. Also wires up configuration-change notifications so the pipeline
/// reloads atomically when <c>config.json</c> is updated on disk.
/// </summary>
internal sealed class PipelineStartupService : IHostedService
{
    private readonly IOptionsMonitor<CarConfiguration> _carConfig;
    private readonly RacecarPipelineHost _pipeline;
    private readonly ActiveConfigurationFactory _factory;
    private readonly ICanBusFactory _busFactory;
    private readonly ILogger<PipelineStartupService> _logger;
    private IDisposable? _changeListener;

    public PipelineStartupService(
        IOptionsMonitor<CarConfiguration> carConfig,
        RacecarPipelineHost pipeline,
        ActiveConfigurationFactory factory,
        ICanBusFactory busFactory,
        ILogger<PipelineStartupService> logger)
    {
        _carConfig = carConfig;
        _pipeline = pipeline;
        _factory = factory;
        _busFactory = busFactory;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        ApplyConfiguration(_carConfig.CurrentValue);
        AttachBuses(_carConfig.CurrentValue);

        _changeListener = _carConfig.OnChange(cfg =>
        {
            try
            {
                var next = _factory.Build(cfg);
                _pipeline.ReloadConfiguration(next);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reload pipeline configuration.");
            }
        });

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _changeListener?.Dispose();
        return Task.CompletedTask;
    }

    private void ApplyConfiguration(CarConfiguration cfg)
    {
        try
        {
            var active = _factory.Build(cfg);
            _pipeline.ReloadConfiguration(active);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build initial pipeline configuration.");
        }
    }

    private void AttachBuses(CarConfiguration cfg)
    {
        for (var i = 0; i < cfg.CanConfig.Interfaces.Count; i++)
        {
            if (i >= cfg.CanConfig.CanBusEnabled.Count || !cfg.CanConfig.CanBusEnabled[i]) continue;
            var iface = cfg.CanConfig.Interfaces[i];
            try
            {
                var bus = _busFactory.CreateCanBus();
                var openResult = bus.OpenAsync(iface.InterfaceName, iface.BitRate).GetAwaiter().GetResult();
                if (openResult != 0)
                {
                    _logger.LogWarning(
                        "CAN bus {Interface} failed to open (code {Code}); skipping.", iface.InterfaceName, openResult);
                    continue;
                }
                bus.IsSilentOnCanBus = iface.SilentOnCanBus;
                _pipeline.AttachBus(i, bus);
                _logger.LogInformation("CAN bus {Interface} attached (bus index {Index}).", iface.InterfaceName, i);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open CAN bus {Interface}.", iface.InterfaceName);
            }
        }
    }
}
