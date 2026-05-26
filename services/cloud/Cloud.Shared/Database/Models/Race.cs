using System.ComponentModel.DataAnnotations;

namespace Cloud.Shared.Database.Models;

public class Race
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    [StringLength(100, MinimumLength = 1)]
    public required string Name { get; set; }
    public DateTime Start { get; set; }
    public double Duration { get; set; }
    /// <summary>
    /// IANA time-zone id (e.g. "America/Chicago") that <see cref="Start"/> is expressed in.
    /// Stored alongside the naive wall-clock so cloud-side consumers can convert UTC
    /// instants into the race's wall-clock for the "is the race active right now" check —
    /// the UI compares using JS local time, which only works for the user who entered
    /// it; the server has no implicit local TZ to fall back on.
    /// </summary>
    [StringLength(50, MinimumLength = 1)]
    public string TimeZone { get; set; } = "America/Chicago";
    [StringLength(500)]
    public string Notes { get; set; } = string.Empty;
    public int? RedMistEventId { get; set; }
    public int? RedMistOrganizationId { get; set; }
}