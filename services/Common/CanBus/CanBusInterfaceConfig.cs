namespace Common.CanBus;

/// <summary>
/// Represents settings for a single CAN network interface.
/// </summary>
public class CanBusInterfaceConfig
{
    /// <summary>
    /// Network interface name, such as "can0." The application will attempt to connect to this interface and read/write CAN messages according to the configuration.
    /// </summary>
    public string InterfaceName { get; set; } = string.Empty;
    public int BitRate { get; set; } = 1000000;
    public bool SilentOnCanBus { get; set; }

    public List<CanMessageConfig> Messages { get; set; } = [];
}
