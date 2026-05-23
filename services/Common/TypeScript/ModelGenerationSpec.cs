using Channels;
using Channels.Alarms;
using Channels.Counters;
using Channels.Enums;
using Channels.Logic;
using Channels.Logging;
using Channels.Math;
using Channels.Tables;
using Channels.Timers;
using Channels.UserConditions;
using Common.CanBus;
using TypeGen.Core.SpecGeneration;
using Common.FuelAnalysis;

namespace Common.TypeScript;

public class ModelGenerationSpec : GenerationSpec
{
    private const string OutputPath = "../../ui/shared-ui/src/models";

    private static readonly string[] FilesToOmit =
    [
        "value-tuple.ts",
        "i-value-tuple-internal.ts",
        "i-tuple.ts",
        "i-structural-equatable.ts",
        "i-structural-comparable.ts"
    ];

    private static readonly Type[] InterfaceTypes =
    [
        typeof(CarConfiguration),
        typeof(CanMessageConfig),
        typeof(CanBusConfig),
        typeof(CanBusInterfaceConfig),
        typeof(CanChannelAssignmentConfig),
        typeof(ChannelDefinition),
        typeof(CounterDefinition),
        typeof(MathDefinition),
        typeof(TableDefinition),
        typeof(TableMapping),
        typeof(TimerDefinition),
        typeof(ConditionDefinition),
        typeof(StatementDefinition),
        typeof(ComparisonDefinition),
        typeof(CarConfigurationSummary),
        typeof(AlarmDefinition),
        typeof(EnumDefinition),
        typeof(EnumValueDefinition),
        typeof(LoggingDefinition),
        typeof(CarFuelConfig),
        typeof(ThrottleConsumptionConfig)
    ];

    private static readonly Type[] EnumTypes =
    [
        typeof(MathType),
        typeof(SimpleOperationType),
        typeof(InterpolationType),
        typeof(LogicType),
        typeof(LoggingFrequency),
    ];

    public override void OnBeforeGeneration(OnBeforeGenerationArgs args)
    {
        foreach (var type in InterfaceTypes)
            AddInterface(type);

        foreach (var type in EnumTypes)
            AddEnum(type);

        MessagePackConverterGenerator.Generate(InterfaceTypes, OutputPath);
    }

    public override void OnAfterGeneration(OnAfterGenerationArgs args)
    {
        var generatedFiles = args.GeneratedFiles.ToList();

        foreach (var file in generatedFiles)
        {
            var name = Path.GetFileName(file);
            if (FilesToOmit.Contains(name, StringComparer.OrdinalIgnoreCase) && File.Exists(file))
                File.Delete(file);
        }

        XmlDocJsDocInjector.InjectJsDocs(InterfaceTypes.Concat(EnumTypes), OutputPath);

        var tableDefinitionPath = generatedFiles
            .FirstOrDefault(f => string.Equals(Path.GetFileName(f), "table-definition.ts", StringComparison.OrdinalIgnoreCase));

        if (tableDefinitionPath is null || !File.Exists(tableDefinitionPath))
            return;

        var content = File.ReadAllText(tableDefinitionPath);
        content = content.Replace("export class TableDefinition {", "export interface TableDefinition {");
        content = System.Text.RegularExpressions.Regex.Replace(content, @" = [^\n;]+;", ";");
        File.WriteAllText(tableDefinitionPath, content);
    }
}
