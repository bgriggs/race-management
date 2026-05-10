using System.Runtime.InteropServices;

namespace Racecar.CanBus.PiCan;

/// <summary>
/// CAN support for Raspberry Pi's PiCAN Hat.  This uses the executables that
/// are provided with the hat by calling them in a command shell.
/// https://copperhilltech.com/pican-2-can-bus-interface-for-raspberry-pi/
/// </summary>
public class PiCanCanBus : ICanBus
{
    public event Action<CanMessage>? Received;
    private ILogger Logger { get; }
    private readonly ILoggerFactory loggerFactory;

    public bool IsOpen { get; private set; }

    public bool IsSilentOnCanBus { get; set; }

    private long _messagesRx;
    public long MessagesRx => Interlocked.Read(ref _messagesRx);

    private long _messagesTx;
    public long MessagesTx => Interlocked.Read(ref _messagesTx);

    private string? interfaceName;
    private int _socketFd = -1;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;


    public PiCanCanBus(ILoggerFactory loggerFactory)
    {
        Logger = loggerFactory.CreateLogger(GetType().Name);
        this.loggerFactory = loggerFactory;
    }


    public async Task<int> OpenAsync(string interfaceName, int speed)
    {
        this.interfaceName = interfaceName;
        await CloseAsync();
        await Task.Delay(1000);
        var result = await LinkUpAsync(speed);
        if (result != 0)
        {
            Logger.LogError($"Failed to start CAN link if {interfaceName} with speed {speed}");
            return result;
        }

        Logger.LogDebug($"CAN link started");

        var sockResult = OpenSocket();
        if (sockResult != 0)
            return sockResult;

        Logger.LogDebug($"CAN socket started");

        _receiveCts = new CancellationTokenSource();
        _receiveTask = Task.Run(() => DoReceive(_receiveCts.Token));
        IsOpen = true;
        Logger.LogDebug($"Completed PiCAN initialization.");
        return 0;
    }

    private int OpenSocket()
    {
        if (interfaceName == null)
        {
            Logger.LogError("Interface name is null when trying to open CAN socket");
            return -1;
        }

        _socketFd = SocketCan.socket(SocketCan.AF_CAN, SocketCan.SOCK_RAW, SocketCan.CAN_RAW);
        if (_socketFd < 0)
        {
            Logger.LogError("Failed to open CAN socket, errno: {Errno}", Marshal.GetLastWin32Error());
            return -1;
        }

        var ifr = new SocketCan.Ifreq { IfName = interfaceName };
        if (SocketCan.ioctl(_socketFd, SocketCan.SIOCGIFINDEX, ref ifr) < 0)
        {
            Logger.LogError("Failed to get interface index for {Interface}, errno: {Errno}", interfaceName, Marshal.GetLastWin32Error());
            SocketCan.close(_socketFd);
            _socketFd = -1;
            return -1;
        }

        var addr = new SocketCan.SockAddrCan
        {
            CanFamily = SocketCan.AF_CAN,
            CanIfIndex = ifr.IfIndex
        };

        if (SocketCan.bind(_socketFd, ref addr, Marshal.SizeOf<SocketCan.SockAddrCan>()) < 0)
        {
            Logger.LogError("Failed to bind CAN socket, errno: {Errno}", Marshal.GetLastWin32Error());
            SocketCan.close(_socketFd);
            _socketFd = -1;
            return -1;
        }

        Logger.LogDebug("CAN socket opened and bound to {Interface} (ifindex={IfIndex})", interfaceName, ifr.IfIndex);
        return 0;
    }

    private async Task<int> LinkUpAsync(int speed = 1000000)
    {
        var cmd = new ShellCommand(loggerFactory);
        try
        {
            Logger.LogDebug($"Startup CAN link: {interfaceName} at {speed}...");
            int exitCode = await cmd.RunInstAsync("sudo", $"/sbin/ip link set {interfaceName} up type can bitrate {speed}");
            if (exitCode != 0)
            {
                Logger.LogError("ip link set up exited with code {ExitCode}", exitCode);
                return exitCode;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error starting PiCAN link");
            return -1;
        }
        return 0;
    }

    public void Send(CanMessage message)
    {
        if (IsSilentOnCanBus) return;

        if (_socketFd < 0)
            throw new InvalidOperationException("CAN socket is not open.");

        int dlc = Math.Min(message.DataLength, 8);
        var frame = new SocketCan.CanFrame
        {
            CanId = message.IdLength == IdLength._29bit
                ? (message.CanId & SocketCan.CAN_EFF_MASK) | SocketCan.CAN_EFF_FLAG
                : message.CanId & SocketCan.CAN_SFF_MASK,
            Dlc = (byte)dlc,
            Data = new byte[8]
        };

        if (message.Data is { Length: > 0 })
            Buffer.BlockCopy(message.Data, 0, frame.Data, 0, dlc);

        int written = SocketCan.write(_socketFd, ref frame, SocketCan.CAN_FRAME_SIZE);
        if (written < 0)
        {
            int errno = Marshal.GetLastWin32Error();
            Logger.LogError("CAN write failed, errno: {Errno}", errno);
            throw new IOException($"CAN write failed with errno {errno}");
        }

        Interlocked.Increment(ref _messagesTx);
    }

    private void DoReceive(CancellationToken ct)
    {
        Logger.LogDebug("CAN receive loop started");
        var pfd = new SocketCan.PollFd
        {
            Fd = _socketFd,
            Events = SocketCan.POLLIN
        };

        while (!ct.IsCancellationRequested)
        {
            // Wait up to 200 ms so we can check for cancellation
            int ready = SocketCan.poll(ref pfd, 1, 200);
            if (ready < 0)
            {
                int errno = Marshal.GetLastWin32Error();
                if (errno == 4) continue; // EINTR — interrupted by signal, retry
                Logger.LogError("CAN poll failed, errno: {Errno}", errno);
                break;
            }

            if (ready == 0) continue; // timeout, loop to check cancellation

            if ((pfd.REvents & SocketCan.POLLERR) != 0)
            {
                Logger.LogWarning("CAN socket error event on poll");
                continue;
            }

            var frame = new SocketCan.CanFrame { Data = new byte[8] };
            int bytesRead = SocketCan.read(_socketFd, ref frame, SocketCan.CAN_FRAME_SIZE);
            if (bytesRead < 0)
            {
                Logger.LogError("CAN read failed, errno: {Errno}", Marshal.GetLastWin32Error());
                break;
            }

            bool isExtended = (frame.CanId & SocketCan.CAN_EFF_FLAG) != 0;
            var message = new CanMessage
            {
                CanId = isExtended
                    ? frame.CanId & SocketCan.CAN_EFF_MASK
                    : frame.CanId & SocketCan.CAN_SFF_MASK,
                IdLength = isExtended ? IdLength._29bit : IdLength._11bit,
                DataLength = frame.Dlc,
                Data = frame.Data[..frame.Dlc],
                Timestamp = DateTime.UtcNow
            };

            Interlocked.Increment(ref _messagesRx);

            try
            {
                Received?.Invoke(message);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Exception in CAN Received handler");
            }
        }

        Logger.LogDebug("CAN receive loop stopped");
    }

    public async Task<int> CloseAsync()
    {
        var cmd = new ShellCommand(loggerFactory);
        try
        {
            Logger.LogDebug("Turning off can link");

            if (_receiveCts != null)
            {
                await _receiveCts.CancelAsync();
                if (_receiveTask != null)
                    await _receiveTask.ConfigureAwait(false);
                _receiveCts.Dispose();
                _receiveCts = null;
                _receiveTask = null;
            }

            if (_socketFd >= 0)
            {
                SocketCan.close(_socketFd);
                _socketFd = -1;
            }

            int exitCode = await cmd.RunInstAsync("sudo", $"/sbin/ip link set {interfaceName} down");
            if (exitCode != 0)
                Logger.LogWarning("ip link set down exited with code {ExitCode}", exitCode);

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