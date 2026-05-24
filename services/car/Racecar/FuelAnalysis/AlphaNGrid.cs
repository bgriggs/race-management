namespace Racecar.FuelAnalysis;

/// <summary>
/// 10×10 alpha-N (TPS, RPM) lookup grid for throttle-derived fuel rate.
///
/// Two surfaces:
/// <list type="bullet">
///   <item><b>Persistent</b> per-cell rate in gal/min and observation count.
///   Updated only on calibration window close, EMA-smoothed; backed by the
///   on-disk <see cref="ThrottleProxyCalibrationState"/>.</item>
///   <item><b>In-window</b> per-cell time and throttle-integral accumulators,
///   reset on every window open. Used at window close to attribute the
///   window's <c>Δfuel</c> proportionally across visited cells.</item>
/// </list>
///
/// Single-threaded by contract; the consumer's mailbox is the only writer.
/// </summary>
public sealed class AlphaNGrid
{
    public const int Dimension = 10;
    public const int CellCount = Dimension * Dimension;

    private readonly int _rpmMax;
    private readonly double[] _cellRates;
    private readonly int[] _cellSampleCounts;

    private readonly double[] _windowCellSeconds = new double[CellCount];
    private readonly double[] _windowCellIntegral = new double[CellCount];

    public AlphaNGrid(int rpmMax, ThrottleProxyCalibrationState? persisted = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rpmMax);
        _rpmMax = rpmMax;

        if (persisted is { Grid: { Cells.Length: CellCount, CellSampleCounts.Length: CellCount } g })
        {
            _cellRates = (double[])g.Cells.Clone();
            _cellSampleCounts = (int[])g.CellSampleCounts.Clone();
        }
        else
        {
            _cellRates = new double[CellCount];
            Array.Fill(_cellRates, double.NaN);
            _cellSampleCounts = new int[CellCount];
        }
    }

    public int RpmMax => _rpmMax;

    /// <summary>Compute a row-major cell index for a given (TPS, RPM) pair.</summary>
    public int CellIndex(double tps, double rpm)
    {
        var tpsBin = (int)Math.Floor(tps / 10.0);
        if (tpsBin < 0) tpsBin = 0;
        else if (tpsBin >= Dimension) tpsBin = Dimension - 1;

        var rpmBin = (int)Math.Floor(rpm * Dimension / _rpmMax);
        if (rpmBin < 0) rpmBin = 0;
        else if (rpmBin >= Dimension) rpmBin = Dimension - 1;

        return tpsBin * Dimension + rpmBin;
    }

    /// <summary>Persistent cell rate at <paramref name="cellIndex"/>, or NaN if unsampled.</summary>
    public double GetCellRate(int cellIndex) => _cellRates[cellIndex];

    /// <summary>Persistent observation count at <paramref name="cellIndex"/>.</summary>
    public int GetCellSampleCount(int cellIndex) => _cellSampleCounts[cellIndex];

    /// <summary>
    /// Lookup the current rate at <paramref name="tps"/>/<paramref name="rpm"/>.
    /// Returns NaN when the cell has not been calibrated.
    /// </summary>
    public double Lookup(double tps, double rpm) => _cellRates[CellIndex(tps, rpm)];

    /// <summary>
    /// Coverage = fraction of cells with at least <paramref name="minCellSamples"/>
    /// observations, expressed in [0..1].
    /// </summary>
    public double Coverage(int minCellSamples)
    {
        var covered = 0;
        for (var i = 0; i < CellCount; i++)
        {
            if (_cellSampleCounts[i] >= minCellSamples) covered++;
        }
        return covered / (double)CellCount;
    }

    /// <summary>Reset the in-window per-cell accumulators. Called on window open.</summary>
    public void ResetWindowObservations()
    {
        Array.Clear(_windowCellSeconds);
        Array.Clear(_windowCellIntegral);
    }

    /// <summary>
    /// Record an in-window observation for one sample. <paramref name="dtSeconds"/>
    /// is the elapsed time the previous (TPS, RPM) was held; the throttle integral
    /// contribution is <c>tps × dtSeconds</c>.
    /// </summary>
    public void RecordWindowSample(double tps, double rpm, double dtSeconds)
    {
        if (dtSeconds <= 0) return;
        var idx = CellIndex(tps, rpm);
        _windowCellSeconds[idx] += dtSeconds;
        _windowCellIntegral[idx] += tps * dtSeconds;
    }

    /// <summary>
    /// Apply a closed calibration window. Allocates the window's measured fuel
    /// across visited cells proportionally to each cell's share of the
    /// window's throttle integral, then EMAs each cell's rate.
    /// </summary>
    /// <param name="deltaFuelGallons">Ground-truth fuel burned over the window.</param>
    /// <param name="totalWindowIntegral">
    /// Σ TPS·Δt over the whole window (from
    /// <see cref="ThrottleIntegralTotalizer"/>). Must be positive.
    /// </param>
    /// <param name="emaAlpha">EMA weight applied to new observations.</param>
    /// <returns>True if at least one cell was updated.</returns>
    public bool ApplyWindowClose(double deltaFuelGallons, double totalWindowIntegral, double emaAlpha)
    {
        if (deltaFuelGallons <= 0 || totalWindowIntegral <= 0) return false;

        var anyUpdated = false;
        for (var i = 0; i < CellCount; i++)
        {
            var cellSeconds = _windowCellSeconds[i];
            var cellIntegral = _windowCellIntegral[i];
            if (cellSeconds <= 0 || cellIntegral <= 0) continue;

            // Cell share of the window's fuel, in gallons.
            var cellFuel = deltaFuelGallons * (cellIntegral / totalWindowIntegral);
            // Convert to gal/min over the time the cell was occupied.
            var cellRateGalPerMin = cellFuel / (cellSeconds / 60.0);

            var prev = _cellRates[i];
            _cellRates[i] = double.IsNaN(prev)
                ? cellRateGalPerMin
                : (1 - emaAlpha) * prev + emaAlpha * cellRateGalPerMin;
            _cellSampleCounts[i]++;
            anyUpdated = true;
        }
        return anyUpdated;
    }

    /// <summary>Snapshot persistent state into a <see cref="GridState"/> for serialization.</summary>
    public GridState Snapshot() => new()
    {
        TpsBinPct = 10.0,
        RpmBinMax = _rpmMax,
        RpmBinCount = Dimension,
        Cells = (double[])_cellRates.Clone(),
        CellSampleCounts = (int[])_cellSampleCounts.Clone(),
    };
}
