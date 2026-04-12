namespace Common.CanBus;

/// <summary>
/// Represents settings for a single CAN network interface.
/// </summary>
public class CanBusConfig
{
    public string InterfaceName { get; set; } = string.Empty;
    public int BitRate { get; set; } = 1000000;
    public bool SilentOnCanBus { get; set; }

    public List<CanMessageConfig> Messages { get; } = [];
}
