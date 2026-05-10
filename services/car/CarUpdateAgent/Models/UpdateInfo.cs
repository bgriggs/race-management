namespace CarUpdateAgent.Models;

/// <summary>
/// Describes a new core app version to be applied, as received from the cloud hub
/// or constructed by the laptop update path.
/// </summary>
public class UpdateInfo
{
    /// <summary>The version string for the new binary (e.g. "1.4.2").</summary>
    public required string Version { get; init; }

    /// <summary>
    /// Signed HTTPS URL to download the binary from cloud object storage.
    /// Null when the binary is supplied directly via the laptop REST endpoint.
    /// </summary>
    public string? DownloadUrl { get; init; }

    /// <summary>Expected SHA-256 hex digest (lowercase) of the binary.</summary>
    public required string ExpectedSha256 { get; init; }
}
