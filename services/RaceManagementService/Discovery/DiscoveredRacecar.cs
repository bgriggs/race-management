namespace RaceManagementService.Discovery;

/// <summary>
/// A racecar management API endpoint discovered on the local network via DNS-SD.
/// </summary>
/// <param name="Name">The DNS-SD service name, which is the car's Pi hostname (e.g. "race-car-1").</param>
/// <param name="Host">The mDNS hostname or IP address to reach the car (e.g. "race-car-1.local" or "192.168.1.42").</param>
/// <param name="Port">The HTTP port the management REST API is listening on.</param>
public sealed record DiscoveredRacecar(string Name, string Host, int Port)
{
    /// <summary>Base URL for HTTP communication with the car's management REST API.</summary>
    public string BaseUrl => $"http://{Host}:{Port}";
}
