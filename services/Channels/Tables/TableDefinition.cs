namespace Channels.Tables;

public class TableDefinition
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsEnum { get; set; }
    public bool IgnoreCase { get; set; }
    public Guid InputChannel { get; set; }
    public Guid OutputChannel { get; set; }
    public InterpolationType InterpolationType { get; set; }

    public List<TableMapping> Mappings { get; set; } = [];


    private IEnumerable<double>? inputPoints = null;
    private IEnumerable<double>? outputValues = null;

    public IEnumerable<double> GetInputPoints()
    {
        inputPoints ??= [.. Mappings.Select(m => double.Parse(m.Input))];
        return inputPoints;
    }

    public IEnumerable<double> GetOutputValues()
    {
        outputValues ??= [.. Mappings.Select(m => double.Parse(m.Output))];
        return outputValues;
    }
}

public record TableMapping(string Input, string Output);