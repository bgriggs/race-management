using System.Runtime.InteropServices;

namespace Racecar.CanBus;

public interface ICanBusFactory
{
    ICanBus CreateCanBus();
}

public class CanBusFactory(ILoggerFactory loggerFactory) : ICanBusFactory
{
    public ICanBus CreateCanBus()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new PiCan.PiCanCanBus(loggerFactory);
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new NotImplementedException("No CAN bus implementation for Windows yet");
        }
        throw new NotImplementedException("No CAN bus implementation for this OS yet");
    }
}