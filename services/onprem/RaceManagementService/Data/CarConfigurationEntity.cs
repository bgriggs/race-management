using System.ComponentModel.DataAnnotations;

namespace RaceManagementService.Data;

public class CarConfigurationEntity
{
    public Guid Id { get; set; }
    [MaxLength(32)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(6)]
    public string Car { get; set; } = string.Empty;
    [MaxLength(1024)]
    public string Notes { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
    public DateTime? LastUpdatedOnCarTimestamp { get; set; }
    public int ConfigurationSchemaVersion { get; set; }
    public string Data { get; set; } = string.Empty;
}
