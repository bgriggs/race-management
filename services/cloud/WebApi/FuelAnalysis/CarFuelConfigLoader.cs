using System.Text.Json;
using Cloud.Shared.Database;
using Common;
using Common.FuelAnalysis;
using Microsoft.EntityFrameworkCore;

namespace WebApi.FuelAnalysis;

/// <summary>
/// Reads the active <see cref="CarFuelConfig"/> for a (team, car) pair out of the
/// JSON-blob CarConfigurations table. Centralized so callers — OfflineSnapshotBuilder,
/// StintEditor, FuelController — share a single deserialization path and stay aligned
/// on which configuration row is "active" (latest by LastUpdated).
/// </summary>
public static class CarFuelConfigLoader
{
    public static async Task<CarFuelConfig?> LoadAsync(
        RaceManagementContext db, int teamId, string carNumber, CancellationToken ct)
    {
        var json = await db.CarConfigurations
            .AsNoTracking()
            .Where(c => c.TeamId == teamId && c.Car == carNumber)
            .OrderByDescending(c => c.LastUpdated)
            .Select(c => c.ConfigurationJson)
            .FirstOrDefaultAsync(ct);
        if (json is null) return null;
        try
        {
            return JsonSerializer.Deserialize<CarConfiguration>(json)?.FuelConfig;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
