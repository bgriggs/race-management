using System.Text;

namespace Channels;

public class ChannelValue
{
    /// <summary>
    /// ID of the channel as specified with a channel definition.
    /// </summary>
    public int Id { get; set; }

    public string Value { get; set; } = string.Empty;

    public int GetValueInt()
    {
        return int.TryParse(Value, out var result) ? result : 0;
    }

    public double GetValueDouble()
    {
        return double.TryParse(Value, out var result) ? result : 0;
    }

    public void SetBaseValue(double value, ChannelDefinition definition)
    {
        var zeros = GetZeros(definition.BaseDecimalPlaces);
        Value = value.ToString("0." + zeros);
    }

    private static string GetZeros(int count)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < count; i++)
        {
            _ = sb.Append('0');
        }
        return sb.ToString();
    }
}
