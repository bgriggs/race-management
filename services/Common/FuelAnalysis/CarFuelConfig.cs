using System.ComponentModel.DataAnnotations;

namespace Common.FuelAnalysis;

public class CarFuelConfig
{
    public bool IsEnabled { get; set; }

    [Range(1, 100)]
    public double TankCapacityGallons { get; set; }
    [Range(0.0001, 10)]
    public double DefaultConsumptionGalPerMin { get; set; }
    [Range(0, 1)]
    public double DefaultYellowConsumptionMultiplier { get; set; } = 0.5;
    [Range(0, 1)]
    public double DefaultCode35ConsumptionMultiplier { get; set; } = 0.3;

    public ThrottleConsumptionConfig ThrottleConsumption { get; set; } = new();
}
