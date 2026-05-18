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
    [StringLength(500)]
    public string Notes { get; set; } = string.Empty;
    public int? RedMistEventId { get; set; }
    public int? RedMistOrganizationId { get; set; }
}