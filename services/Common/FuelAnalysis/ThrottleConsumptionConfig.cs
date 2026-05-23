using System.ComponentModel.DataAnnotations;

namespace Common.FuelAnalysis;

public class ThrottleConsumptionConfig
{
    public bool IsEnabled { get; set; }
    [Range(2000, 12000)]
    public int MaxRpm { get; set; } = 7000;
}
