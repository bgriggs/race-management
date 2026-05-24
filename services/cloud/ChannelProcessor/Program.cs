using ChannelProcessor.FuelAnalysis;
using ChannelProcessor.FuelAnalysis.Calibration;
using ChannelProcessor.FuelAnalysis.Config;
using ChannelProcessor.FuelAnalysis.Estimators;
using ChannelProcessor.FuelAnalysis.Pace;
using ChannelProcessor.FuelAnalysis.Reconciler;
using ChannelProcessor.FuelAnalysis.Refuel;
using ChannelProcessor.FuelAnalysis.Session;
using ChannelProcessor.FuelAnalysis.Snapshot;
using ChannelProcessor.FuelAnalysis.State;
using ChannelProcessor.FuelAnalysis.Windows;
using ChannelProcessor.Telemetry;
using Cloud.Shared.Extensions;
using Cloud.Shared.FuelAnalysis;
using Cloud.Shared.Streaming;
using NLog.Extensions.Logging;

namespace ChannelProcessor;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.ClearProviders();
        builder.Logging.AddNLog("NLog");

        // Add services to the container.

        // Configure Redis with settings for SignalR backplane in multi-replica environment
        builder.Services.AddRedisConnectionMultiplexer(builder.Configuration);

        builder.Services.AddPostgres(builder.Configuration);

        builder.Services.AddSingleton<ICarChannelStateRepository, CarChannelStateRepository>();
        builder.Services.AddHostedService<TelemetryStreamConsumer>();

        builder.Services.AddSingleton<ITeamChannelStateRepository, TeamChannelStateRepository>();
        builder.Services.AddHostedService<TeamChannelStreamConsumer>();

        // Fuel Analysis — the reconciler joins the existing two consumers as a fourth
        // hosted worker, using its own consumer group on the car stream.
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddHybridCache();
        builder.Services.AddSingleton<ICarChannelDefinitionResolver, CarChannelDefinitionResolver>();
        builder.Services.AddSingleton<ICarChannelPublisher, CarChannelPublisher>();
        builder.Services.AddSingleton<IRaceSessionGate, RaceSessionGate>();
        builder.Services.AddSingleton<ICarFuelStateRepository, CarFuelStateRepository>();
        builder.Services.AddSingleton<ICarFuelConfigReader, CarFuelConfigReader>();
        builder.Services.AddSingleton<SessionLifecycleHandler>();
        builder.Services.AddSingleton<StintLifecycle>();
        builder.Services.AddSingleton<RefuelEventDetector>();
        builder.Services.AddSingleton<FuelWindowLifecycle>();
        builder.Services.AddSingleton<ManualEntryHandler>();
        builder.Services.AddSingleton<EcuResetClassifier>();
        // Slice 3 — Estimators + Reconciler + Snapshot emission
        builder.Services.AddSingleton<IFuelEstimator, EcuEstimator>();
        builder.Services.AddSingleton<IFuelEstimator, FlowMeterRawEstimator>();
        builder.Services.AddSingleton<IFuelEstimator, FlowMeterCorrectedEstimator>();
        builder.Services.AddSingleton<IFuelEstimator, PitFillEstimator>();
        builder.Services.AddSingleton<IFuelEstimator, ThrottleProxyIntegralEstimator>();
        builder.Services.AddSingleton<IFuelEstimator, ThrottleProxyGridEstimator>();
        builder.Services.AddSingleton<ThrottleProxyTracker>();
        builder.Services.AddSingleton<ICalibrationFactorReader, CalibrationFactorReader>();
        builder.Services.AddSingleton<CalibrationFactorLearner>();
        builder.Services.AddSingleton<RateModel>();
        builder.Services.AddSingleton<OutlierDebouncer>();
        builder.Services.AddSingleton<DriverPaceTracker>();
        builder.Services.AddSingleton<DriverPaceCalculator>();
        builder.Services.AddSingleton<FuelReconciler>();
        builder.Services.AddSingleton<IFuelSnapshotStore, FuelSnapshotStore>();
        builder.Services.AddSingleton<FuelSnapshotPublisher>();
        builder.Services.AddSingleton<SnapshotEmitter>();
        builder.Services.AddHostedService<FuelReconcilerWorker>();

        builder.Services.AddControllers();
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        var app = builder.Build();
        app.LogAssemblyInfo<Program>();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            Console.Title = "ChannelProcessor";
        }

        app.UseAuthorization();


        app.MapControllers();
        app.MapHealthCheckEndpoints();

        app.Run();
    }
}
