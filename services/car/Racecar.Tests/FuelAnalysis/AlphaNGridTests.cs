using Racecar.FuelAnalysis;

namespace Racecar.Tests.FuelAnalysis;

[TestClass]
public sealed class AlphaNGridTests
{
    [TestMethod]
    public void Cell_index_clamps_to_grid_dimensions()
    {
        var g = new AlphaNGrid(rpmMax: 8000);

        Assert.AreEqual(0, g.CellIndex(0, 0));
        Assert.AreEqual(0, g.CellIndex(-10, -100));
        Assert.AreEqual(99, g.CellIndex(100, 8000));
        Assert.AreEqual(99, g.CellIndex(200, 16000));
    }

    [TestMethod]
    public void Cell_index_uses_floor_so_boundaries_belong_to_lower_bin()
    {
        var g = new AlphaNGrid(rpmMax: 10000);
        // tps=20 → bin 2, rpm=2000 → bin 2 → index 22.
        Assert.AreEqual(2 * 10 + 2, g.CellIndex(20, 2000));
    }

    [TestMethod]
    public void Lookup_returns_NaN_for_unsampled_cells()
    {
        var g = new AlphaNGrid(rpmMax: 8000);
        Assert.IsTrue(double.IsNaN(g.Lookup(50, 4000)));
    }

    [TestMethod]
    public void Window_close_attributes_fuel_by_integral_share()
    {
        var g = new AlphaNGrid(rpmMax: 8000);
        g.ResetWindowObservations();
        // Cell A: 50%, 4000rpm — 60 seconds at 50% → integral 3000
        g.RecordWindowSample(50, 4000, 60);
        // Cell B: 25%, 2000rpm — 60 seconds at 25% → integral 1500
        g.RecordWindowSample(25, 2000, 60);

        // Total integral 4500; ΔFuel 1.0 gal → A gets 2/3 gal, B gets 1/3 gal.
        var updated = g.ApplyWindowClose(deltaFuelGallons: 1.0, totalWindowIntegral: 4500, emaAlpha: 0.3);
        Assert.IsTrue(updated);

        var aIdx = g.CellIndex(50, 4000);
        var bIdx = g.CellIndex(25, 2000);

        // A: (2/3 gal) / (60s / 60s/min) = 2/3 gal/min
        Assert.AreEqual(2.0 / 3.0, g.GetCellRate(aIdx), 1e-9);
        // B: (1/3 gal) / 1 min = 1/3 gal/min
        Assert.AreEqual(1.0 / 3.0, g.GetCellRate(bIdx), 1e-9);
        Assert.AreEqual(1, g.GetCellSampleCount(aIdx));
        Assert.AreEqual(1, g.GetCellSampleCount(bIdx));
    }

    [TestMethod]
    public void Window_close_emas_existing_cell_values()
    {
        var g = new AlphaNGrid(rpmMax: 8000);
        // Seed: 60s at 50%, 0.5 gal → integral 3000, cell rate 0.5 gal/min.
        g.ResetWindowObservations();
        g.RecordWindowSample(50, 4000, 60);
        g.ApplyWindowClose(0.5, 3000, 0.3);

        var idx = g.CellIndex(50, 4000);
        var first = g.GetCellRate(idx);

        // Second window: 60s at 50%, 1.0 gal → cell rate observation 1.0 gal/min, EMA α=0.3.
        g.ResetWindowObservations();
        g.RecordWindowSample(50, 4000, 60);
        g.ApplyWindowClose(1.0, 3000, 0.3);

        var expected = 0.7 * first + 0.3 * 1.0;
        Assert.AreEqual(expected, g.GetCellRate(idx), 1e-9);
        Assert.AreEqual(2, g.GetCellSampleCount(idx));
    }

    [TestMethod]
    public void Coverage_counts_cells_at_or_above_min_samples()
    {
        var g = new AlphaNGrid(rpmMax: 8000);
        g.ResetWindowObservations();
        g.RecordWindowSample(50, 4000, 60);
        g.ApplyWindowClose(0.5, 3000, 0.3);

        Assert.AreEqual(0, g.Coverage(minCellSamples: 30));
        Assert.AreEqual(0.01, g.Coverage(minCellSamples: 1), 1e-9);
    }

    [TestMethod]
    public void Window_close_with_zero_integral_is_noop()
    {
        var g = new AlphaNGrid(rpmMax: 8000);
        var updated = g.ApplyWindowClose(1.0, 0, 0.3);
        Assert.IsFalse(updated);
    }

    [TestMethod]
    public void Constructor_restores_persisted_grid_state()
    {
        var rates = new double[100];
        Array.Fill(rates, double.NaN);
        rates[22] = 0.42;
        var counts = new int[100];
        counts[22] = 5;

        var persisted = new ThrottleProxyCalibrationState
        {
            Grid = new GridState { TpsBinPct = 10, RpmBinMax = 8000, RpmBinCount = 10, Cells = rates, CellSampleCounts = counts },
        };

        var g = new AlphaNGrid(8000, persisted);
        Assert.AreEqual(0.42, g.GetCellRate(22));
        Assert.AreEqual(5, g.GetCellSampleCount(22));
    }
}
