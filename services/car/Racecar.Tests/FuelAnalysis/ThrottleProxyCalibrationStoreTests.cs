using Racecar.FuelAnalysis;

namespace Racecar.Tests.FuelAnalysis;

[TestClass]
public sealed class ThrottleProxyCalibrationStoreTests
{
    private static string NewTempPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "racecar-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "fuel-calibration.json");
    }

    [TestMethod]
    public void Load_returns_null_when_file_does_not_exist()
    {
        var store = new ThrottleProxyCalibrationStore(NewTempPath());
        Assert.IsNull(store.Load());
        Assert.IsFalse(store.Exists);
    }

    [TestMethod]
    public void Round_trip_save_and_load_preserves_state()
    {
        var path = NewTempPath();
        var store = new ThrottleProxyCalibrationStore(path);

        var state = ThrottleProxyCalibrationState.Empty(rpmMax: 8000);
        state.K = 0.000182;
        state.KSampleCount = 47;
        state.KLastUpdatedUtc = new DateTime(2026, 5, 24, 14, 30, 0, DateTimeKind.Utc);
        state.Grid.Cells[22] = 0.45;
        state.Grid.CellSampleCounts[22] = 7;

        store.Save(state);
        Assert.IsTrue(store.Exists);

        var loaded = store.Load();
        Assert.IsNotNull(loaded);
        Assert.AreEqual(state.K, loaded!.K);
        Assert.AreEqual(state.KSampleCount, loaded.KSampleCount);
        Assert.AreEqual(state.KLastUpdatedUtc, loaded.KLastUpdatedUtc);
        Assert.AreEqual(state.Grid.RpmBinMax, loaded.Grid.RpmBinMax);
        Assert.AreEqual(0.45, loaded.Grid.Cells[22]);
        Assert.AreEqual(7, loaded.Grid.CellSampleCounts[22]);
    }

    [TestMethod]
    public void Save_uses_atomic_rename_leaving_no_temp_behind()
    {
        var path = NewTempPath();
        var store = new ThrottleProxyCalibrationStore(path);
        store.Save(ThrottleProxyCalibrationState.Empty(8000));

        Assert.IsTrue(File.Exists(path));
        Assert.IsFalse(File.Exists(path + ".tmp"));
    }

    [TestMethod]
    public void Load_returns_null_on_corrupt_file()
    {
        var path = NewTempPath();
        File.WriteAllText(path, "{ not valid json");
        var store = new ThrottleProxyCalibrationStore(path);
        Assert.IsNull(store.Load());
    }

    [TestMethod]
    public void Manual_override_round_trips()
    {
        var path = NewTempPath();
        var store = new ThrottleProxyCalibrationStore(path);
        var state = ThrottleProxyCalibrationState.Empty(8000);
        state.ManualOverride = new ManualOverrideRecord
        {
            Value = 0.0002,
            Source = CalibrationSource.ManualOverride,
            ByUser = "engineer-1",
            AtUtc = new DateTime(2026, 5, 24, 12, 0, 0, DateTimeKind.Utc),
        };
        store.Save(state);

        var loaded = store.Load();
        Assert.IsNotNull(loaded);
        Assert.IsNotNull(loaded!.ManualOverride);
        Assert.AreEqual(0.0002, loaded.ManualOverride!.Value);
        Assert.AreEqual(CalibrationSource.ManualOverride, loaded.ManualOverride.Source);
        Assert.AreEqual("engineer-1", loaded.ManualOverride.ByUser);
    }
}
