using Channels.Logic;
using Cloud.Shared.Alarms;
using Cloud.Shared.Database.Models;
using Cloud.Shared.Database.Models.Alarms;
using Cloud.Shared.Database.Models.FuelAnalysis;
using Cloud.Shared.FuelAnalysis;
using Cloud.Shared.RedMist;
using Cloud.Shared.Telemetry;
using Common.TypeScript;
using TypeGen.Core.SpecGeneration;
using WebApi.Controllers;

namespace WebApi.TypeScript;

public class ModelGenerationSpec : GenerationSpec
{
    private const string OutputPath = "../../../ui/shared-ui/src/cloud-api";

    private static readonly Type[] InterfaceTypes =
    [
        typeof(Team),
        typeof(Car),
        typeof(TeamRequest),
        typeof(CarRequest),
        typeof(CarUpdate),
        typeof(SaveCarConfigurationResult),
        typeof(UserTeam),
        typeof(ChannelStatusTableConfiguration),
        typeof(ChannelStatusTableColumnConfiguration),
        typeof(CarChannelSnapshot),
        typeof(ChannelValueSnapshot),
        typeof(ChannelChangeNotification),
        typeof(CarConnectionChangeNotification),
        typeof(Race),
        typeof(SiteSettings),
        // Fuel Analysis — snapshot DTOs
        typeof(FuelRangeSnapshot),
        typeof(RangeReadout),
        typeof(EstimatorReadout),
        typeof(ReconcilerDetails),
        typeof(FuelWindowDetails),
        // Fuel Analysis — persisted entities (read by the Gantt + detail panel)
        typeof(RefuelEvent),
        typeof(FuelWindow),
        typeof(Stint),
        typeof(CalibrationFactor),
        // Fuel Analysis — request / response records on FuelController
        typeof(ManualRefuelRequest),
        typeof(EnterVolumeRequest),
        typeof(RefuelEventPublishResult),
        typeof(CalibrationOverrideRequest),
        // Alarm Processor
        typeof(AlarmDefinitionDto),
        typeof(ActiveAlarmDto),
        typeof(AlarmChangeNotification),
        typeof(StatementDefinition),
        typeof(ComparisonDefinition),
        // RedMist
        typeof(OrganizationSummary),
        typeof(RaceStateDto),
    ];

    private static readonly Type[] EnumTypes =
    [
        typeof(RefuelConfidenceTier),
        typeof(RefuelSource),
        typeof(EcuResetState),
        typeof(RefuelAnchor),
        typeof(CalibrationFactorSource),
        typeof(AlarmEventType),
        typeof(LogicType),
    ];

    private static readonly Type[] MessagePackTypes =
    [
        typeof(CarChannelSnapshot),
        typeof(ChannelValueSnapshot),
        typeof(ChannelChangeNotification),
        typeof(CarConnectionChangeNotification),
        typeof(AlarmChangeNotification),
        typeof(RaceStateDto),
    ];

    public override void OnBeforeGeneration(OnBeforeGenerationArgs args)
    {
        foreach (var type in InterfaceTypes)
            AddInterface(type);

        foreach (var type in EnumTypes)
            AddEnum(type);

        AddInterface<Team>().Member(nameof(Team.IsDeleted)).Ignore();
        AddInterface<Car>().Member(nameof(Car.IsDeleted)).Ignore();

        MessagePackConverterGenerator.Generate(MessagePackTypes, OutputPath);
    }
}
