namespace Channels.Tables;

public class TableMapping
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsEnum { get; set; }
    public bool IgnoreCase { get; set; }
    public int InputChannel { get; set; }
    public int OutputChannel { get; set; }
    public InterpolationType InterpolationType { get; set; }

    public List<(string input, string output)> Mapping { get; } = [];


    private IEnumerable<double>? inputPoints = null;
    private IEnumerable<double>? outputValues = null;

    public IEnumerable<double> InputPoints
    {
        get
        {
            inputPoints ??= [.. Mapping.Select(m => double.Parse(m.input))];
            return inputPoints;
        }
    }

    public IEnumerable<double> OutputValues
    {
        get
        {
            outputValues ??= [.. Mapping.Select(m => double.Parse(m.output))];
            return outputValues;
        }
    }
}