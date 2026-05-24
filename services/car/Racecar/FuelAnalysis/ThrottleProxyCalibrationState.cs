namespace Racecar.FuelAnalysis;

/// <summary>
/// Source of a calibration value, for the audit trail.
/// </summary>
public enum CalibrationSource
{
    Learned,
    ManualOverride,
    Reset,
}

/// <summary>
/// Engineer override of the integral scalar <c>k</c>. Halts EMA learning until
/// cleared.
/// </summary>
public sealed class ManualOverrideRecord
{
    public double Value { get; set; }
    public CalibrationSource Source { get; set; }
    public string? ByUser { get; set; }
    public DateTime AtUtc { get; set; }
}

/// <summary>
/// Alpha-N grid persisted state.
/// </summary>
public sealed class GridState
{
    public double TpsBinPct { get; set; } = 10.0;
    public int RpmBinMax { get; set; }
    public int RpmBinCount { get; set; } = 10;

    /// <summary>Row-major (TPS-bin, RPM-bin) rate per cell, in gal/min. NaN = unsampled.</summary>
    public double[] Cells { get; set; } = [];

    /// <summary>Row-major (TPS-bin, RPM-bin) observation counts (window-closes that touched the cell).</summary>
    public int[] CellSampleCounts { get; set; } = [];
}

/// <summary>
/// Serializable on-car calibration record for the throttle proxy module.
/// Persisted to <c>fuel-calibration.json</c> with atomic-rename writes.
/// </summary>
public sealed class ThrottleProxyCalibrationState
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>Integral scalar in gal per (TPS-percent · second). NaN until first window closes.</summary>
    public double K { get; set; } = double.NaN;

    public DateTime? KLastUpdatedUtc { get; set; }

    public int KSampleCount { get; set; }

    public GridState Grid { get; set; } = new();

    public ManualOverrideRecord? ManualOverride { get; set; }

    /// <summary>Returns an empty state sized for the given RPM-max.</summary>
    public static ThrottleProxyCalibrationState Empty(int rpmMax)
    {
        var cells = new double[100];
        Array.Fill(cells, double.NaN);
        return new ThrottleProxyCalibrationState
        {
            Grid = new GridState
            {
                TpsBinPct = 10.0,
                RpmBinMax = rpmMax,
                RpmBinCount = 10,
                Cells = cells,
                CellSampleCounts = new int[100],
            },
        };
    }
}
