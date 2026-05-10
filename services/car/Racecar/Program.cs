using Common;
using NLog;
using NLog.Targets;
using NLog.Web;
using Racecar.CanBus;
using Racecar.Logging;
using Racecar.Services;

namespace Racecar;

public class Program
{
    public static void Main(string[] args)
    {
        // Register the custom target type before NLog loads its config from appsettings.
        Target.Register<LogBroadcastTarget>("LogBroadcast");

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

        builder.Services.AddHostedService<TestCanBus>();

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

public class TestCanBus : BackgroundService
{
    private readonly ICanBusFactory canBusFactory;

    public TestCanBus(ICanBusFactory canBusFactory)
    {
        this.canBusFactory = canBusFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var canBus = canBusFactory.CreateCanBus();
        var result = await canBus.OpenAsync("can0", 1000000);
        if (result != 0)
        {
            Console.WriteLine($"Failed to open CAN bus, error code: {result}");
            return;
        }

        canBus.Received += message =>
        {
            Console.WriteLine($"Received CAN message: ID=0x{message.CanId:X}, Data={BitConverter.ToString(message.Data, 0, message.DataLength)}, Timestamp={message.Timestamp}");
        };
        while (!stoppingToken.IsCancellationRequested)
        {
            var message = new CanMessage
            {
                CanId = 0x123,
                IdLength = IdLength._11bit,
                Data = [0xDE, 0xAD, 0xBE, 0xEF],
                DataLength = 4,
                Timestamp = DateTime.UtcNow
            };
            canBus.Send(message);
            await Task.Delay(1000, stoppingToken);
        }
    }


}