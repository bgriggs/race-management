using Common;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Racecar.Controllers;

[ApiController]
[Route("config")]
public class ConfigController : ControllerBase
{
    private static readonly JsonSerializerOptions s_jsonOptions =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly ILogger<ConfigController> _logger;
    private readonly IWebHostEnvironment _env;

    public ConfigController(ILogger<ConfigController> logger, IWebHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    private string ConfigPath => Path.Combine(_env.ContentRootPath, "config.json");
    private string VersionsDir => Path.Combine(_env.ContentRootPath, "config-versions");

    /// <summary>
    /// Returns the current active car configuration.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetConfigAsync()
    {
        if (!System.IO.File.Exists(ConfigPath))
            return NotFound();

        var json = await System.IO.File.ReadAllTextAsync(ConfigPath);
        var config = JsonSerializer.Deserialize<CarConfiguration>(json, s_jsonOptions);
        return Ok(config);
    }

    /// <summary>
    /// Receives a new configuration from the pit laptop.
    /// The configuration is written atomically and takes effect on next restart.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> PostConfigAsync([FromBody] CarConfiguration configuration)
    {
        await ArchiveCurrentConfigAsync();
        await WriteConfigAtomicAsync(ConfigPath, configuration);

        _logger.LogInformation("Configuration {ConfigurationId} written to {ConfigPath}", configuration.ConfigurationId, ConfigPath);
        return Ok();
    }

    /// <summary>
    /// Lists stored configuration versions available for rollback.
    /// </summary>
    [HttpGet("versions")]
    public async Task<IActionResult> GetVersionsAsync()
    {
        if (!Directory.Exists(VersionsDir))
            return Ok(Array.Empty<CarConfigurationSummary>());

        var summaries = new List<CarConfigurationSummary>();
        foreach (var file in Directory.EnumerateFiles(VersionsDir, "*.json"))
        {
            try
            {
                var json = await System.IO.File.ReadAllTextAsync(file);
                var config = JsonSerializer.Deserialize<CarConfiguration>(json, s_jsonOptions);
                if (config is not null)
                    summaries.Add(ToSummary(config));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not read version file {File}", file);
            }
        }

        return Ok(summaries.OrderByDescending(s => s.LastUpdated));
    }

    /// <summary>
    /// Rolls back to a previously stored configuration version.
    /// </summary>
    [HttpPost("rollback/{version}")]
    public async Task<IActionResult> PostRollbackAsync(string version)
    {
        if (!Guid.TryParse(version, out var versionId))
            return BadRequest("Version must be a valid configuration ID (GUID).");

        var versionPath = Path.Combine(VersionsDir, $"{versionId}.json");
        if (!System.IO.File.Exists(versionPath))
            return NotFound();

        var json = await System.IO.File.ReadAllTextAsync(versionPath);
        var config = JsonSerializer.Deserialize<CarConfiguration>(json, s_jsonOptions);
        if (config is null)
            return UnprocessableEntity("Version file could not be parsed.");

        await ArchiveCurrentConfigAsync();
        await WriteConfigAtomicAsync(ConfigPath, config);

        _logger.LogInformation("Rolled back to configuration {ConfigurationId}", versionId);
        return Ok();
    }

    private async Task ArchiveCurrentConfigAsync()
    {
        if (!System.IO.File.Exists(ConfigPath))
            return;

        Directory.CreateDirectory(VersionsDir);

        // Read the current config to get its ID for the archive filename.
        var json = await System.IO.File.ReadAllTextAsync(ConfigPath);
        var current = JsonSerializer.Deserialize<CarConfiguration>(json, s_jsonOptions);
        if (current is null)
            return;

        var archivePath = Path.Combine(VersionsDir, $"{current.ConfigurationId}.json");
        System.IO.File.Copy(ConfigPath, archivePath, overwrite: true);
    }

    private static async Task WriteConfigAtomicAsync(string configPath, CarConfiguration configuration)
    {
        var tempPath = configPath + ".tmp";
        var json = JsonSerializer.Serialize(configuration, s_jsonOptions);
        await System.IO.File.WriteAllTextAsync(tempPath, json);
        System.IO.File.Move(tempPath, configPath, overwrite: true);
    }

    private static CarConfigurationSummary ToSummary(CarConfiguration c) => new()
    {
        Id = c.ConfigurationId,
        Name = c.Name,
        Car = c.Car,
        Notes = c.Notes,
        LastUpdated = c.LastUpdated,
        LastUpdatedOnCarTimestamp = c.LastUpdatedOnCarTimestamp,
        ConfigurationSchemaVersion = c.ConfigurationSchemaVersion,
    };
}
