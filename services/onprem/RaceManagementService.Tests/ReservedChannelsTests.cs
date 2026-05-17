using System.ComponentModel.DataAnnotations;
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
}
