using System.Runtime.InteropServices;

namespace Racecar.CanBus.PiCan;

/// <summary>
/// P/Invoke declarations for the Linux SocketCAN interface (libc).
/// The PiCAN 2 board is driven entirely through the kernel SocketCAN
/// subsystem and appears as a standard network interface (e.g. can0).
/// </summary>
internal static class SocketCan
{
    // ── Socket address family / protocol ─────────────────────────────────────
    internal const int AF_CAN   = 29;
    internal const int SOCK_RAW = 3;
    internal const int CAN_RAW  = 1;

    // ── ioctl request ────────────────────────────────────────────────────────
    internal const uint SIOCGIFINDEX = 0x8933;

    // ── CAN ID flags ─────────────────────────────────────────────────────────
    internal const uint CAN_EFF_FLAG = 0x80000000u; // 29-bit extended frame
    internal const uint CAN_RTR_FLAG = 0x40000000u; // remote transmission request
    internal const uint CAN_EFF_MASK = 0x1FFFFFFFu; // valid bits for 29-bit IDs
    internal const uint CAN_SFF_MASK = 0x000007FFu; // valid bits for 11-bit IDs

    // ── struct can_frame size (fixed at 16 bytes) ────────────────────────────
    internal const int CAN_FRAME_SIZE = 16;

    // ── libc P/Invokes ───────────────────────────────────────────────────────

    [DllImport("libc", SetLastError = true)]
    internal static extern int socket(int domain, int type, int protocol);

    [DllImport("libc", SetLastError = true)]
    internal static extern int bind(int sockfd, ref SockAddrCan addr, int addrlen);

    [DllImport("libc", SetLastError = true)]
    internal static extern int ioctl(int fd, uint request, ref Ifreq ifr);

    [DllImport("libc", SetLastError = true)]
    internal static extern int write(int fd, ref CanFrame frame, int count);

    [DllImport("libc", SetLastError = true)]
    internal static extern int read(int fd, ref CanFrame frame, int count);

    [DllImport("libc", SetLastError = true)]
    internal static extern int poll(ref PollFd fds, uint nfds, int timeout);

    [DllImport("libc", SetLastError = true)]
    internal static extern int close(int fd);

    // ── poll() constants ──────────────────────────────────────────────────────
    internal const short POLLIN  = 0x0001; // data available to read
    internal const short POLLERR = 0x0008; // error condition

    // ── Structs ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Maps to Linux <c>struct pollfd</c>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct PollFd
    {
        public int   Fd;      // file descriptor
        public short Events;  // events to watch (e.g. POLLIN)
        public short REvents; // events that occurred (filled by kernel)
    }

    /// <summary>
    /// Maps to Linux <c>struct can_frame</c> (16 bytes).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct CanFrame
    {
        public uint CanId;  // CAN_EFF_FLAG | CAN_RTR_FLAG | 29-bit or 11-bit ID
        public byte Dlc;    // data length code (0–8)
        public byte Pad;
        public byte Res0;
        public byte Res1;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] Data;
    }

    /// <summary>
    /// Maps to Linux <c>struct sockaddr_can</c> (16 bytes).
    /// The explicit padding field accounts for natural alignment between
    /// the 2-byte <c>can_family</c> and the 4-byte <c>can_ifindex</c>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct SockAddrCan
    {
        public ushort CanFamily;   // AF_CAN
        private ushort _pad;       // natural alignment padding
        public int CanIfIndex;     // interface index from SIOCGIFINDEX
        public uint RxId;          // can_addr.tp.rx_id (unused for RAW)
        public uint TxId;          // can_addr.tp.tx_id (unused for RAW)
    }

    /// <summary>
    /// Minimal view of Linux <c>struct ifreq</c> (40 bytes on 64-bit).
    /// Only <c>ifr_name</c> (offset 0) and <c>ifr_ifindex</c> (offset 16) are used.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    internal struct Ifreq
    {
        [FieldOffset(0)]
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
        public string IfName;

        [FieldOffset(16)]
        public int IfIndex;
    }
}
