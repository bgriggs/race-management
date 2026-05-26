using Channels;
using Common;
using WebApi.Controllers;

namespace Cloud.Tests.WebApi.Controllers;

/// <summary>
/// Pure validation tests for <see cref="ConfigurationController.ValidateChannelRouting"/>.
/// Covers ADR-0007 amendment (2026-05-25):
///   1. IsDistributionLocked reserved templates reject distribution changes.
///   2. Origin family (Car* vs Cloud*) is fixed at creation; edits cannot cross it.
/// </summary>
[TestClass]
public class ChannelRoutingValidationTests
{
    // ThrottleProxyFuelUsed — IsDistributionLocked=true, Distribution=CarToCloud, managed by throttle-consumption.
    private static readonly Guid LockedReservedChannelId = Guid.Parse("916d4b4f-5bcf-4d9c-2a1e-4d6e8b3c5a0d");

    private static CarConfiguration Config(params ChannelDefinition[] channels) => new()
    {
        ConfigurationId = Guid.NewGuid(),
        Car = "42",
        ClientId = "test-client",
        ClientSecret = "test-secret",
        Name = "Test",
        ChannelDefinitions = [.. channels],
    };

    private static ChannelDefinition Ch(Guid id, ChannelDistribution distribution, string name = "Test", bool isReserved = false) => new()
    {
        Id = id,
        IsReserved = isReserved,
        Name = name,
        Abbreviation = "TST",
        DataType = "Temperature",
        BaseUnitType = "DegreeFahrenheit",
        Distribution = distribution,
    };

    [TestMethod]
    public void NoPrior_NoTemplateLock_NoErrors()
    {
        var customChannelId = Guid.NewGuid();
        var incoming = Config(Ch(customChannelId, ChannelDistribution.CarToCloud));

        var errors = ConfigurationController.ValidateChannelRouting(incoming, prior: null);

        Assert.IsEmpty(errors);
    }

    [TestMethod]
    public void LockedReservedChannel_KeepingTemplateDistribution_NoErrors()
    {
        var incoming = Config(Ch(LockedReservedChannelId, ChannelDistribution.CarToCloud, "ThrottleProxyFuelUsed", isReserved: true));

        var errors = ConfigurationController.ValidateChannelRouting(incoming, prior: null);

        Assert.IsEmpty(errors);
    }

    [TestMethod]
    public void LockedReservedChannel_ChangingToDifferentDistribution_Rejected()
    {
        var incoming = Config(Ch(LockedReservedChannelId, ChannelDistribution.CarLocal, "ThrottleProxyFuelUsed", isReserved: true));

        var errors = ConfigurationController.ValidateChannelRouting(incoming, prior: null);

        Assert.HasCount(1, errors);
        StringAssert.Contains(errors[0], "locked");
        StringAssert.Contains(errors[0], "throttle-consumption");
    }

    [TestMethod]
    public void EditingExistingChannel_KeepingSameOriginFamily_NoErrors()
    {
        var channelId = Guid.NewGuid();
        var prior = Config(Ch(channelId, ChannelDistribution.CarToCloud));
        var incoming = Config(Ch(channelId, ChannelDistribution.CarLocal));

        var errors = ConfigurationController.ValidateChannelRouting(incoming, prior);

        Assert.IsEmpty(errors);
    }

    [TestMethod]
    public void EditingExistingChannel_CrossingOriginFamily_Rejected()
    {
        // Custom channel originally car-origin; client tries to flip it to cloud-origin.
        var channelId = Guid.NewGuid();
        var prior = Config(Ch(channelId, ChannelDistribution.CarToCloud));
        var incoming = Config(Ch(channelId, ChannelDistribution.CloudLocal));

        var errors = ConfigurationController.ValidateChannelRouting(incoming, prior);

        Assert.HasCount(1, errors);
        StringAssert.Contains(errors[0], "Origin is fixed");
    }

    [TestMethod]
    public void NewChannelOnExistingConfig_NoPriorEntry_NoOriginCheck()
    {
        // The user added a new custom channel; the existing config had other channels.
        // No prior entry by this Id → origin-family check skipped (any distribution OK at create time).
        var existingChannelId = Guid.NewGuid();
        var newChannelId = Guid.NewGuid();
        var prior = Config(Ch(existingChannelId, ChannelDistribution.CarToCloud));
        var incoming = Config(
            Ch(existingChannelId, ChannelDistribution.CarToCloud),
            Ch(newChannelId, ChannelDistribution.CloudLocal));

        var errors = ConfigurationController.ValidateChannelRouting(incoming, prior);

        Assert.IsEmpty(errors);
    }

    [TestMethod]
    public void BothViolations_BothReported()
    {
        var customId = Guid.NewGuid();
        var prior = Config(
            Ch(customId, ChannelDistribution.CarToCloud),
            Ch(LockedReservedChannelId, ChannelDistribution.CarToCloud, "ThrottleProxyFuelUsed", isReserved: true));
        var incoming = Config(
            Ch(customId, ChannelDistribution.CloudLocal),
            Ch(LockedReservedChannelId, ChannelDistribution.CarLocal, "ThrottleProxyFuelUsed", isReserved: true));

        var errors = ConfigurationController.ValidateChannelRouting(incoming, prior);

        Assert.HasCount(2, errors);
    }
}
