using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cloud.Shared.Database.Models;

[Table("CarConfiguration")]
public class CarConfigurationTable
{
    [Key]
    public Guid Id { get; set; }
    public int TeamId { get; set; }
    [Length(1, 6)]
    public required string Car { get; set; }
    public DateTime LastUpdated { get; set; }
    public DateTime? LastUpdatedOnCarTimestamp { get; set; }
    public int ConfigurationSchemaVersion { get; set; }
    [Length(3, 32)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(1024)]
    public string Notes { get; set; } = string.Empty;
    public string ConfigurationJson { get; set; } = string.Empty;
}
