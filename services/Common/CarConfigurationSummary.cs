namespace Common;

public class CarConfigurationSummary
{
    public Guid Id { get; set; }
    public DateTime LastUpdated { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public int ConfigurationSchemaVersion { get; init; }
}
