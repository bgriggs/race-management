using System.Text.Json;
using System.Text.Json.Serialization;

namespace Racecar.FuelAnalysis;

/// <summary>
/// Atomic-rename JSON persistence for the throttle proxy calibration record.
/// Mirrors the pattern used by the on-car config writer: write to a sibling
/// temp file then <see cref="File.Move(string, string, bool)"/> over the target.
/// </summary>
public sealed class ThrottleProxyCalibrationStore
{
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    private readonly string _path;

    public ThrottleProxyCalibrationStore(string path)
    {
        _path = path;
    }

    public string Path => _path;

    public bool Exists => File.Exists(_path);

    /// <summary>
    /// Load the persisted calibration, or <c>null</c> if no file exists.
    /// Returns <c>null</c> if the file cannot be parsed (caller may seed empty).
    /// </summary>
    public ThrottleProxyCalibrationState? Load()
    {
        if (!File.Exists(_path)) return null;
        try
        {
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<ThrottleProxyCalibrationState>(json, s_jsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>Atomic write: serialize to a temp sibling then rename over the target.</summary>
    public void Save(ThrottleProxyCalibrationState state)
    {
        var dir = System.IO.Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var tempPath = _path + ".tmp";
        var json = JsonSerializer.Serialize(state, s_jsonOptions);
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _path, overwrite: true);
    }
}
