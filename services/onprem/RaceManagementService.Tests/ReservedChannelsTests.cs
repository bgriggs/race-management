using System.ComponentModel.DataAnnotations;
using Channels;
using Common;
using UnitsNet;

namespace RaceManagementService.Tests;

[TestClass]
public sealed class ReservedChannelsTests
{
    [TestMethod]
    public void AllIds_AreUnique()
    {
        var ids = ReservedChannels.Channels.Select(c => c.Id).ToList();
        var uniqueIds = ids.Distinct().ToList();
        CollectionAssert.AreEquivalent(uniqueIds, ids, "Duplicate channel Ids found.");
    }

    [TestMethod]
    public void AllNames_AreUnique()
    {
        var names = ReservedChannels.Channels.Select(c => c.Name).ToList();
        var uniqueNames = names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        CollectionAssert.AreEquivalent(uniqueNames, names, "Duplicate channel Names found.");
    }

    [TestMethod]
    public void AllAbbreviations_AreUnique()
    {
        var abbreviations = ReservedChannels.Channels.Select(c => c.Abbreviation).ToList();
        var uniqueAbbreviations = abbreviations.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        CollectionAssert.AreEquivalent(uniqueAbbreviations, abbreviations, "Duplicate channel Abbreviations found.");
    }

    [TestMethod]
    public void AllDataTypes_AreValidUnitsNetQuantities()
    {
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "String", "Unitless" };
        var invalidDataTypes = ReservedChannels.Channels
            .Where(c => !excluded.Contains(c.DataType))
            .Where(c => !Quantity.Infos.Any(q => q.Name.Equals(c.DataType, StringComparison.OrdinalIgnoreCase)))
            .Select(c => $"{c.Name}: {c.DataType}")
            .ToList();

        Assert.IsEmpty(invalidDataTypes,
            $"Channels with unrecognized DataType: {string.Join(", ", invalidDataTypes)}");
    }

    [TestMethod]
    public void AllBaseUnitTypes_AreValidUnitsNetUnits()
    {
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "String", "Unitless" };
        var invalidBaseUnits = ReservedChannels.Channels
            .Where(c => !excluded.Contains(c.DataType) && !string.IsNullOrEmpty(c.BaseUnitType))
            .Where(c =>
            {
                var quantityInfo = Quantity.Infos.FirstOrDefault(q => q.Name.Equals(c.DataType, StringComparison.OrdinalIgnoreCase));
                return quantityInfo is null ||
                       !quantityInfo.UnitInfos.Any(u => u.Name.Equals(c.BaseUnitType, StringComparison.OrdinalIgnoreCase));
            })
            .Select(c => $"{c.Name}: {c.DataType}/{c.BaseUnitType}")
            .ToList();

        Assert.IsEmpty(invalidBaseUnits,
            $"Channels with unrecognized BaseUnitType: {string.Join(", ", invalidBaseUnits)}");
    }

    [TestMethod]
    public void AllChannelDefinitions_PassDataAnnotationValidation()
    {
        var failures = new List<string>();

        foreach (var channel in ReservedChannels.Channels)
        {
            var context = new ValidationContext(channel);
            var results = new List<ValidationResult>();
            if (!Validator.TryValidateObject(channel, context, results, validateAllProperties: true))
            {
                foreach (var result in results)
                    failures.Add($"{channel.Name}: {result.ErrorMessage}");
            }
        }

        Assert.IsEmpty(failures,
            $"Channels failing validation:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }

    [TestMethod]
    public void AllDistributionValues_AreDefinedEnumMembers()
    {
        var invalid = ReservedChannels.Channels
            .Where(c => !Enum.IsDefined(typeof(ChannelDistribution), c.Distribution))
            .Select(c => $"{c.Name}: {c.Distribution}")
            .ToList();

        Assert.IsEmpty(invalid,
            $"Channels with undefined Distribution: {string.Join(", ", invalid)}");
    }

    [TestMethod]
    public void AllScopeValues_AreDefinedEnumMembers()
    {
        var invalid = ReservedChannels.Channels
            .Where(c => !Enum.IsDefined(typeof(ChannelScope), c.Scope))
            .Select(c => $"{c.Name}: {c.Scope}")
            .ToList();

        Assert.IsEmpty(invalid,
            $"Channels with undefined Scope: {string.Join(", ", invalid)}");
    }

    [TestMethod]
    public void ManagedByFeature_IsKebabCase_WhenSet()
    {
        var invalid = ReservedChannels.Channels
            .Where(c => !string.IsNullOrEmpty(c.ManagedByFeature))
            .Where(c => !System.Text.RegularExpressions.Regex.IsMatch(c.ManagedByFeature!, "^[a-z]+(-[a-z]+)*$"))
            .Select(c => $"{c.Name}: '{c.ManagedByFeature}'")
            .ToList();

        Assert.IsEmpty(invalid,
            $"Channels with non-kebab-case ManagedByFeature: {string.Join(", ", invalid)}");
    }

    [TestMethod]
    public void FuelAnalysisChannels_HaveExpectedRoutingFields()
    {
        // The set of reserved channels expected to be auto-injected when CarFuelConfig.IsEnabled flips on.
        // Each is identified by Name plus its expected Distribution. If any of these change in
        // ReservedChannels.cs, this test catches it — managed-channel routing is a contract.
        var expected = new[]
        {
            ("RaceFlagState",                  ChannelDistribution.CloudToCar,  ChannelScope.PerTeam),
            ("Position",                       ChannelDistribution.CloudLocal,  ChannelScope.PerCar),
            ("ClassPosition",                  ChannelDistribution.CloudLocal,  ChannelScope.PerCar),
            ("InPit",                          ChannelDistribution.CloudLocal,  ChannelScope.PerCar),
            ("CurrentStintMinutes",            ChannelDistribution.CloudLocal,  ChannelScope.PerCar),
            ("StintCount",                     ChannelDistribution.CloudLocal,  ChannelScope.PerCar),
            ("ManualFuelAddedGallons",         ChannelDistribution.CloudLocal,  ChannelScope.PerCar),
            ("FuelConsumption",                ChannelDistribution.CloudLocal,  ChannelScope.PerCar),
            ("FuelRangeMinutes",               ChannelDistribution.CloudLocal,  ChannelScope.PerCar),
            ("FuelRangeGallons",               ChannelDistribution.CloudLocal,  ChannelScope.PerCar),
            ("FuelRangeLaps",                  ChannelDistribution.CloudLocal,  ChannelScope.PerCar),
            ("FuelRangeMinutesHighConf", ChannelDistribution.CloudLocal,  ChannelScope.PerCar),
            ("FuelRangeGallonsHighConf", ChannelDistribution.CloudLocal,  ChannelScope.PerCar),
            ("FuelRangeLapsHighConf",    ChannelDistribution.CloudLocal,  ChannelScope.PerCar),
            ("FuelRangeConfidence",            ChannelDistribution.CloudLocal,  ChannelScope.PerCar),
            ("FuelConsumptionGalPerLap",       ChannelDistribution.CloudLocal,  ChannelScope.PerCar),
            ("FuelWindowElapsedMinutes",       ChannelDistribution.CloudLocal,  ChannelScope.PerCar),
        };

        var byName = ReservedChannels.Channels.ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);
        var failures = new List<string>();

        foreach (var (name, distribution, scope) in expected)
        {
            if (!byName.TryGetValue(name, out var channel))
            {
                failures.Add($"{name}: missing from ReservedChannels");
                continue;
            }
            if (channel.ManagedByFeature != "fuel-analysis")
                failures.Add($"{name}: ManagedByFeature='{channel.ManagedByFeature}' (expected 'fuel-analysis')");
            if (channel.Distribution != distribution)
                failures.Add($"{name}: Distribution={channel.Distribution} (expected {distribution})");
            if (channel.Scope != scope)
                failures.Add($"{name}: Scope={channel.Scope} (expected {scope})");
        }

        Assert.IsEmpty(failures,
            $"Fuel Analysis channel routing mismatches:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }

    [TestMethod]
    public void ThrottleConsumptionChannels_HaveExpectedRoutingFields()
    {
        var expected = new[]
        {
            // ThrottlePosition is the in-car input; only the in-car ThrottleProxyConsumer reads it.
            // Editable so teams can pick CarLocal to save bandwidth when cloud throttle traces aren't needed.
            ("ThrottlePosition",             ChannelDistribution.CarToCloud, ChannelScope.PerCar, /*locked*/ false),
            // ThrottleProxy* outputs are the cloud reconciler's estimator inputs; locking enforces correctness.
            ("ThrottleProxyFuelUsed",        ChannelDistribution.CarToCloud, ChannelScope.PerCar, /*locked*/ true),
            ("ThrottleProxyRate",            ChannelDistribution.CarToCloud, ChannelScope.PerCar, /*locked*/ true),
            ("ThrottleProxyConfidence",      ChannelDistribution.CarToCloud, ChannelScope.PerCar, /*locked*/ true),
            ("ThrottleProxyGridCoverage",    ChannelDistribution.CarToCloud, ChannelScope.PerCar, /*locked*/ true),
        };

        var byName = ReservedChannels.Channels.ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);
        var failures = new List<string>();

        foreach (var (name, distribution, scope, locked) in expected)
        {
            if (!byName.TryGetValue(name, out var channel))
            {
                failures.Add($"{name}: missing from ReservedChannels");
                continue;
            }
            if (channel.ManagedByFeature != "throttle-consumption")
                failures.Add($"{name}: ManagedByFeature='{channel.ManagedByFeature}' (expected 'throttle-consumption')");
            if (channel.Distribution != distribution)
                failures.Add($"{name}: Distribution={channel.Distribution} (expected {distribution})");
            if (channel.Scope != scope)
                failures.Add($"{name}: Scope={channel.Scope} (expected {scope})");
            if (channel.IsDistributionLocked != locked)
                failures.Add($"{name}: IsDistributionLocked={channel.IsDistributionLocked} (expected {locked})");
        }

        Assert.IsEmpty(failures,
            $"Throttle Consumption channel routing mismatches:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }

    [TestMethod]
    public void ProducedByFeature_MatchesExpectedSet()
    {
        // Channels written by an internal feature's code. The Channels-list "Used" column shows
        // these as "Output: {feature}". If this set changes, update Piece-1 (ProducedByFeature
        // for usage tracking) and the feature manifest documentation.
        var expected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // fuel-analysis — FuelReconciler outputs.
            { "FuelConsumption",            "fuel-analysis" },
            { "FuelRangeMinutes",           "fuel-analysis" },
            { "FuelRangeGallons",           "fuel-analysis" },
            { "FuelRangeLaps",              "fuel-analysis" },
            { "FuelRangeMinutesHighConf",   "fuel-analysis" },
            { "FuelRangeGallonsHighConf",   "fuel-analysis" },
            { "FuelRangeLapsHighConf",      "fuel-analysis" },
            { "FuelRangeConfidence",        "fuel-analysis" },
            { "FuelConsumptionGalPerLap",   "fuel-analysis" },
            { "FuelWindowElapsedMinutes",   "fuel-analysis" },
            // StintTracker outputs (decoupled from RedMist — derived from the InPit channel).
            { "CurrentStintMinutes",        "fuel-analysis" },
            { "StintCount",                 "fuel-analysis" },

            // throttle-consumption — in-car ThrottleProxyConsumer outputs.
            { "ThrottleProxyFuelUsed",      "throttle-consumption" },
            { "ThrottleProxyRate",          "throttle-consumption" },
            { "ThrottleProxyConfidence",    "throttle-consumption" },
            { "ThrottleProxyGridCoverage",  "throttle-consumption" },
        };

        var actual = ReservedChannels.Channels
            .Where(c => !string.IsNullOrEmpty(c.ProducedByFeature))
            .ToDictionary(c => c.Name, c => c.ProducedByFeature!, StringComparer.OrdinalIgnoreCase);

        var failures = new List<string>();
        foreach (var (name, feature) in expected)
        {
            if (!actual.TryGetValue(name, out var actualFeature))
                failures.Add($"{name}: missing ProducedByFeature (expected '{feature}')");
            else if (actualFeature != feature)
                failures.Add($"{name}: ProducedByFeature='{actualFeature}' (expected '{feature}')");
        }
        foreach (var (name, feature) in actual)
        {
            if (!expected.ContainsKey(name))
                failures.Add($"{name}: unexpected ProducedByFeature='{feature}' (not in expected set)");
        }

        Assert.IsEmpty(failures,
            $"ProducedByFeature drift:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }

    [TestMethod]
    public void OnlyThrottleProxyOutputs_HaveDistributionLocked()
    {
        // The set of distribution-locked reserved channels is a contract. Adding to it requires a deliberate
        // decision (and an ADR-0007 amendment update) — bouncing the test forces that conversation.
        var expectedLocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ThrottleProxyFuelUsed",
            "ThrottleProxyRate",
            "ThrottleProxyConfidence",
            "ThrottleProxyGridCoverage",
        };

        var actualLocked = ReservedChannels.Channels
            .Where(c => c.IsDistributionLocked)
            .Select(c => c.Name)
            .ToList();

        CollectionAssert.AreEquivalent(expectedLocked.ToList(), actualLocked,
            "Set of reserved channels with IsDistributionLocked changed — update ADR-0007 amendment if intentional.");
    }

    [TestMethod]
    public void ChannelsWithoutManagedByFeature_DefaultToPerCarCarToCloud()
    {
        // Unmanaged channels are general-purpose telemetry. They MUST use the safe defaults so
        // the runtime treats them like every other car-side channel — anything else implies an
        // editing mistake in ReservedChannels.cs.
        var nonDefault = ReservedChannels.Channels
            .Where(c => string.IsNullOrEmpty(c.ManagedByFeature))
            .Where(c => c.Distribution != ChannelDistribution.CarToCloud || c.Scope != ChannelScope.PerCar)
            .Select(c => $"{c.Name}: Distribution={c.Distribution}, Scope={c.Scope}")
            .ToList();

        Assert.IsEmpty(nonDefault,
            $"Unmanaged channels with non-default routing: {string.Join(", ", nonDefault)}");
    }

    [TestMethod]
    public void PerTeamChannels_AreFeatureManaged()
    {
        // In v1 there is no Team Channels editor — PerTeam channels can only exist as reserved,
        // feature-managed channels. If a PerTeam channel ever appears without a ManagedByFeature,
        // it would be unreachable from any UI flow.
        var orphans = ReservedChannels.Channels
            .Where(c => c.Scope == ChannelScope.PerTeam)
            .Where(c => string.IsNullOrEmpty(c.ManagedByFeature))
            .Select(c => c.Name)
            .ToList();

        Assert.IsEmpty(orphans,
            $"PerTeam channels missing ManagedByFeature: {string.Join(", ", orphans)}");
    }

    [TestMethod]
    public void CloudOriginChannels_AreFeatureManaged()
    {
        // Cloud-produced reserved channels (CloudLocal/CloudToCar) must be feature-managed in v1
        // because they're not user-configurable — they only enter a car's configuration via a
        // feature toggle. An unmanaged cloud-origin reserved channel would be unreachable.
        var orphans = ReservedChannels.Channels
            .Where(c => c.Distribution == ChannelDistribution.CloudLocal || c.Distribution == ChannelDistribution.CloudToCar)
            .Where(c => string.IsNullOrEmpty(c.ManagedByFeature))
            .Select(c => c.Name)
            .ToList();

        Assert.IsEmpty(orphans,
            $"Cloud-origin channels missing ManagedByFeature: {string.Join(", ", orphans)}");
    }
}
