using Cloud.Shared.Database.Models;
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
        typeof(Race),
        typeof(SiteSettings),
    ];

    private static readonly Type[] MessagePackTypes =
    [
        typeof(CarChannelSnapshot),
        typeof(ChannelValueSnapshot),
        typeof(ChannelChangeNotification),
    ];

    public override void OnBeforeGeneration(OnBeforeGenerationArgs args)
    {
        foreach (var type in InterfaceTypes)
            AddInterface(type);

        AddInterface<Team>().Member(nameof(Team.IsDeleted)).Ignore();
        AddInterface<Car>().Member(nameof(Car.IsDeleted)).Ignore();

        MessagePackConverterGenerator.Generate(MessagePackTypes, OutputPath);
    }
}
