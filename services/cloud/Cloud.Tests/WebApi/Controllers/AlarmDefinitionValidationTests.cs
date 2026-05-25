using Channels.Logic;
using Cloud.Shared.Alarms;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using WebApi.Controllers;

namespace Cloud.Tests.WebApi.Controllers;

/// <summary>
/// Pure validation tests for <see cref="ConfigurationController.ValidateAlarmDefinition"/>.
/// Covers the rules surfaced in plan.md (Shape only — non-empty Name, hex color,
/// non-negative ack delay, ActivateComparisons present, comparison ids non-empty).
/// </summary>
[TestClass]
public class AlarmDefinitionValidationTests
{
    private static AlarmDefinitionDto Valid() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Coolant",
        Message = "Coolant high",
        DisplayChannelSourceColorHex = "#FF0000",
        TimeAfterAckToDisplaySecs = 60,
        Statement = new StatementDefinition
        {
            Id = Guid.NewGuid(),
            ActivateComparisons =
            [
                [new ComparisonDefinition { Id = Guid.NewGuid(), ChannelId = Guid.NewGuid(), Logic = LogicType.GreaterThan }],
            ],
        },
    };

    private static ModelStateDictionary State() => new();

    [TestMethod]
    public void Valid_ReturnsTrue_NoErrors()
    {
        var ms = State();
        Assert.IsTrue(ConfigurationController.ValidateAlarmDefinition(Valid(), ms));
        Assert.AreEqual(0, ms.ErrorCount);
    }

    [TestMethod]
    public void EmptyName_Fails()
    {
        var dto = Valid();
        dto.Name = "";
        var ms = State();

        Assert.IsFalse(ConfigurationController.ValidateAlarmDefinition(dto, ms));
        Assert.IsTrue(ms.ContainsKey(nameof(dto.Name)));
    }

    [TestMethod]
    public void NameTooLong_Fails()
    {
        var dto = Valid();
        dto.Name = new string('x', 21);
        var ms = State();

        Assert.IsFalse(ConfigurationController.ValidateAlarmDefinition(dto, ms));
        Assert.IsTrue(ms.ContainsKey(nameof(dto.Name)));
    }

    [TestMethod]
    public void BadHexColor_Fails()
    {
        var dto = Valid();
        dto.DisplayChannelSourceColorHex = "red";
        var ms = State();

        Assert.IsFalse(ConfigurationController.ValidateAlarmDefinition(dto, ms));
        Assert.IsTrue(ms.ContainsKey(nameof(dto.DisplayChannelSourceColorHex)));
    }

    [TestMethod]
    public void ValidShortHexColor_StillFails_OnlySixDigitsAllowed()
    {
        var dto = Valid();
        dto.DisplayChannelSourceColorHex = "#F00";
        var ms = State();

        Assert.IsFalse(ConfigurationController.ValidateAlarmDefinition(dto, ms));
    }

    [TestMethod]
    public void NegativeAckDelay_Fails()
    {
        var dto = Valid();
        dto.TimeAfterAckToDisplaySecs = -1;
        var ms = State();

        Assert.IsFalse(ConfigurationController.ValidateAlarmDefinition(dto, ms));
        Assert.IsTrue(ms.ContainsKey(nameof(dto.TimeAfterAckToDisplaySecs)));
    }

    [TestMethod]
    public void NoActivateComparisons_Fails()
    {
        var dto = Valid();
        dto.Statement.ActivateComparisons = [];
        var ms = State();

        Assert.IsFalse(ConfigurationController.ValidateAlarmDefinition(dto, ms));
    }

    [TestMethod]
    public void OnlyEmptyComparisonGroups_Fails()
    {
        var dto = Valid();
        dto.Statement.ActivateComparisons = [[]];
        var ms = State();

        Assert.IsFalse(ConfigurationController.ValidateAlarmDefinition(dto, ms));
    }

    [TestMethod]
    public void ComparisonWithEmptyId_Fails()
    {
        var dto = Valid();
        dto.Statement.ActivateComparisons[0][0].Id = Guid.Empty;
        var ms = State();

        Assert.IsFalse(ConfigurationController.ValidateAlarmDefinition(dto, ms));
    }

    [TestMethod]
    public void ComparisonWithEmptyChannelId_Fails()
    {
        var dto = Valid();
        dto.Statement.ActivateComparisons[0][0].ChannelId = Guid.Empty;
        var ms = State();

        Assert.IsFalse(ConfigurationController.ValidateAlarmDefinition(dto, ms));
    }
}
