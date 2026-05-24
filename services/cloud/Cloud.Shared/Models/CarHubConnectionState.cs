using MessagePack;

namespace Cloud.Shared.Models;

[MessagePackObject]
public class CarHubConnectionState
{
    [Key(0)]
    public string ConnectionId { get; set; } = string.Empty;
    [Key(1)]
    public string ClientId { get; set; } = string.Empty;
    [Key(2)]
    public DateTime ConnectedTimestamp { get; set; }
    [Key(3)]
    public string CarKey { get; set; } = string.Empty;

    /// <summary>
    /// Team identifier set once <see cref="CarKey"/> is known. Used at disconnect
    /// time to remove the carKey from the per-team connected-cars set without
    /// re-parsing it out of the carKey string.
    /// </summary>
    [Key(4)]
    public int TeamId { get; set; }
}
