using ChannelProcessor.Alarms;
using ChannelProcessor.Alarms.Config;
using ChannelProcessor.Alarms.Persistence;
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
using ChannelProcessor.RedMist;
using ChannelProcessor.StintTracker;
using ChannelProcessor.Telemetry;
using Cloud.Shared.Alarms;
using Cloud.Shared.Extensions;
using Cloud.Shared.FuelAnalysis;
using Cloud.Shared.RedMist;
using Cloud.Shared.Streaming;
using Microsoft.Extensions.Caching.Hybrid;
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

        builder.Services.AddSingleton(TimeProvider.System);
#pragma warning disable EXTEXP0018 // HybridCache is still tagged experimental in 10.0
        builder.Services.AddHybridCache(o => o.DefaultEntryOptions = new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(5), LocalCacheExpiration = TimeSpan.FromMinutes(5) });
#pragma warning restore EXTEXP0018

        // Shared streaming primitives used by both Fuel Analysis and Alarm Processor.
        builder.Services.AddSingleton<ICarChannelDefinitionResolver, CarChannelDefinitionResolver>();
        builder.Services.AddSingleton<ICarChannelPublisher, CarChannelPublisher>();
        builder.Services.AddSingleton<ITeamChannelPublisher, TeamChannelPublisher>();

        builder.Services.AddSingleton<ICarChannelStateRepository, CarChannelStateRepository>();
        builder.Services.AddHostedService<TelemetryStreamConsumer>();

        builder.Services.AddSingleton<ITeamChannelStateRepository, TeamChannelStateRepository>();
        builder.Services.AddHostedService<TeamChannelStreamConsumer>();

        // Fuel Analysis — the reconciler joins the existing two consumers as a fourth
        // hosted worker, using its own consumer group on the car stream.
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

        // Alarm Processor — see plan.md "Alarm Processor (ChannelProcessor / Alarms)".
        builder.Services.AddSingleton<IRedisAlarmStateGateway, RedisAlarmStateGateway>();
        builder.Services.AddSingleton<IAlarmDefinitionRepository, AlarmDefinitionRepository>();
        builder.Services.AddSingleton<ActiveAlarmStore>();
        builder.Services.AddHostedService<AlarmProcessorWorker>();
        builder.Services.AddHostedService<AlarmConfigChangeListener>();

        // RedMist integration — fourth hosted worker (ADR-0008). Per-team SETNX lease
        // coordinates the single hub subscription per team; initial sync via REST.
        builder.Services.AddRedMistClient(builder.Configuration);
        builder.Services.AddSingleton<RedMistLeaseManager>();
        builder.Services.AddSingleton<RedMistActivationEvaluator>();
        builder.Services.AddSingleton<RedMistStatusWriter>();
        builder.Services.AddSingleton<RedMistChannelPublisher>();
        builder.Services.AddSingleton<RedMistRaceStatePublisher>();
        builder.Services.AddHostedService<RedMistConsumerWorker>();

        // StintTracker — derives CurrentStintMinutes / StintCount from InPit transitions.
        // Decoupled from RedMist by design: it only consumes the channel pipeline (ADR-0008).
        builder.Services.AddHostedService<StintTrackerWorker>();

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
