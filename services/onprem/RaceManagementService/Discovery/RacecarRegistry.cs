namespace RaceManagementService.Discovery;

/// <summary>
/// Thread-safe, in-memory registry of currently-visible racecars on the local network.
/// Updated periodically by <see cref="RacecarDiscoveryService"/>.
///
/// Auto-selection rule: when a scan result transitions from zero cars to one or more cars
/// and there is no active car, the service waits 1 second (to allow further cars to appear)
/// then selects the sole car if exactly one was found, or leaves the choice to the user
/// if multiple were found.
/// Eviction rule: if the active car disappears from the scan results it is cleared automatically.
/// </summary>
public sealed class RacecarRegistry
{
    private readonly Lock _lock = new();
    private IReadOnlyList<DiscoveredRacecar> _racecars = [];
    private DiscoveredRacecar? _activeRacecar;

    /// <summary>The most recent snapshot of discovered racecars.</summary>
    public IReadOnlyList<DiscoveredRacecar> Racecars
    {
        get { lock (_lock) return _racecars; }
    }

    /// <summary>The currently selected active racecar, or <c>null</c> if none is selected.</summary>
    public DiscoveredRacecar? ActiveRacecar
    {
        get { lock (_lock) return _activeRacecar; }
    }

    /// <summary>
    /// Explicitly selects an active racecar by name.
    /// Returns <c>false</c> if the named car is not in the current discovered list.
    /// </summary>
    public bool TrySetActive(string name)
    {
        lock (_lock)
        {
            var car = _racecars.FirstOrDefault(c => c.Name == name);
            if (car is null) return false;
            _activeRacecar = car;
            return true;
        }
    }

    /// <summary>Clears the active racecar selection.</summary>
    public void ClearActive()
    {
        lock (_lock) _activeRacecar = null;
    }

    /// <summary>
    /// Updates the discovered list and applies eviction + returns information needed for
    /// auto-selection by the background service.
    /// </summary>
    /// <returns>
    /// A tuple of (<c>wasEmpty</c>, <c>newList</c>, <c>evictedActiveName</c>) where
    /// <c>wasEmpty</c> is true when the previous snapshot had no cars (triggers the
    /// auto-selection delay window), and <c>evictedActiveName</c> is the name of the
    /// active car that was cleared because it disappeared, or <c>null</c> if no eviction
    /// occurred.
    /// </returns>
    internal (bool wasEmpty, IReadOnlyList<DiscoveredRacecar> newList, string? evictedActiveName) Update(
        IReadOnlyList<DiscoveredRacecar> racecars)
    {
        lock (_lock)
        {
            bool wasEmpty = _racecars.Count == 0;

            _racecars = racecars;

            // Evict the active car if it is no longer visible.
            string? evictedActiveName = null;
            if (_activeRacecar is not null &&
                !racecars.Any(c => c.Name == _activeRacecar.Name))
            {
                evictedActiveName = _activeRacecar.Name;
                _activeRacecar = null;
            }

            return (wasEmpty, racecars, evictedActiveName);
        }
    }

    /// <summary>
    /// Attempts to auto-select the active racecar.
    /// Called by the background service after the 1-second delay window.
    /// Only assigns if there is still no active car and exactly one car is present.
    /// </summary>
    internal void TryAutoSelect()
    {
        lock (_lock)
        {
            if (_activeRacecar is not null) return;
            if (_racecars.Count == 1)
                _activeRacecar = _racecars[0];
        }
    }
}
