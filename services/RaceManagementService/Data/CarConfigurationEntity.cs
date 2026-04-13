namespace RaceManagementService.Data;

public class CarConfigurationEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
    public int ConfigurationSchemaVersion { get; set; }
    public string Data { get; set; } = string.Empty;
}
