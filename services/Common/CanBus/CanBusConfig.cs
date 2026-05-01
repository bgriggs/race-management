namespace Common.CanBus;

/// <summary>
/// Top level CAN bus configuration, which includes a list of CAN bus interfaces and their settings, as well as a list of booleans indicating whether each CAN bus is enabled.
/// </summary>
public class CanBusConfig
{
    public List<bool> CanBusEnabled { get; set; } = [];
    public List<CanBusInterfaceConfig> Interfaces { get; set; } = [];
}
