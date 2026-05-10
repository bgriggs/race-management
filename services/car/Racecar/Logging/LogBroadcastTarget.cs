using NLog;
using NLog.Targets;
using Racecar.Services;

namespace Racecar.Logging;

/// <summary>
/// Custom NLog target that forwards rendered log lines to the <see cref="LogBroadcaster"/>
/// singleton. It is registered with NLog before host startup and wired to the DI-owned
/// broadcaster instance after the host is built.
/// </summary>
[Target("LogBroadcast")]
public sealed class LogBroadcastTarget : TargetWithLayout
{
    /// <summary>
    /// Set after the DI container is built. Thread-safe: volatile write in Program.cs
    /// happens-before any log write because the host starts logging after assignment.
    /// </summary>
    public LogBroadcaster? Broadcaster { get; set; }

    protected override void Write(LogEventInfo logEvent)
    {
        Broadcaster?.Publish(RenderLogEvent(Layout, logEvent));
    }
}
