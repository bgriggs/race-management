namespace Racecar.CanBus.PiCan;

/// <summary>
/// CAN support for Raspberry Pi's PiCAN Hat.  This uses the executables that
/// are provided with the hat by calling them in a command shell.
/// </summary>
public class PiCanCanBus : ICanBus
{
    public event Action<CanMessage>? Received;
    private ILogger Logger { get; }
    private readonly ILoggerFactory loggerFactory;

    public bool IsOpen { get; private set; }

    public bool IsSilentOnCanBus { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    private readonly string interfaceName;


    public PiCanCanBus(ILoggerFactory loggerFactory, string interfaceName = "can0")
    {
        Logger = loggerFactory.CreateLogger(GetType().Name);
        this.loggerFactory = loggerFactory;
        this.interfaceName = interfaceName;
    }


    public async Task<int> OpenAsync(int speed)
    {
        await CloseAsync();
        await Task.Delay(1000);
        var result = await LinkUpAsync(speed);
        if (result != 0)
        {
            Logger.LogError($"Failed to start CAN link with speed {speed}");
            return result;
        }

        Logger.LogDebug($"CAN link started");

        //shell.ReceivedOutput += CanShell_ReceivedOutput;
        // receiveThread = new Thread(DoReceive) { IsBackground = true };
        // receiveThread.Start();
        IsOpen = true;
        Logger.LogDebug($"Completed PiCAN initialization.");
        return 0;
    }

    private async Task<int> LinkUpAsync(int speed = 1000000)
    {
        var cmd = new ShellCommand(loggerFactory);
        try
        {
            Logger.LogDebug($"Startup CAN link: {interfaceName} at {speed}...");
            await cmd.RunInstAsync("sudo", $"/sbin/ip link set {interfaceName} up type can bitrate {speed}");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error starting PiCAN link");
            return -1;
        }
        return 0;
    }

    public Task SendAsync(CanMessage message)
    {
        throw new NotImplementedException();
    }

    public async Task<int> CloseAsync()
    {
        var cmd = new ShellCommand(loggerFactory);
        try
        {
            Logger.LogDebug("Turning off can link");
            await cmd.RunInstAsync("sudo", $"/sbin/ip link set {interfaceName} down");

            // if (receiveThread != null)
            // {
            //     shell.Dispose();
            //     receiveThread.Abort();
            //     receiveThread = null;
            // }

            IsOpen = false;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error closing PiCAN link.");
            return -1;
        }
        return 0;
    }
}