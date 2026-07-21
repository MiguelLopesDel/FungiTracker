using EcoMyceliumTracker.Domain;

namespace EcoMyceliumTracker.UnitTests;

/// <summary>
/// These rules used to live inside the repository, reachable only with a
/// database. They are decisions over sensor state, so they are exercised here
/// directly.
/// </summary>
public sealed class TransferPolicyTests
{
    private static readonly Guid Network = Guid.NewGuid();

    private static TransferSensor Sensor(bool isActive = true, Guid? network = null) =>
        new(Guid.NewGuid(), network ?? Network, isActive, "(1,2)");

    [Fact]
    public void TwoActiveSensorsOnTheSameNetwork_AreAllowed() =>
        Assert.Null(Record.Exception(() => TransferPolicy.EnsureAllowed(Sensor(), Sensor())));

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void AnInactiveSensor_IsRejected(bool sourceActive, bool targetActive)
    {
        var error = Assert.Throws<DomainException>(
            () => TransferPolicy.EnsureAllowed(Sensor(sourceActive), Sensor(targetActive)));

        Assert.Equal(DomainErrorKind.Validation, error.Kind);
        Assert.Equal("inactive_transfer_sensor", error.Code);
    }

    [Fact]
    public void SensorsOnDifferentNetworks_AreRejected()
    {
        var error = Assert.Throws<DomainException>(
            () => TransferPolicy.EnsureAllowed(Sensor(), Sensor(network: Guid.NewGuid())));

        Assert.Equal(DomainErrorKind.Validation, error.Kind);
        Assert.Equal("sensors_from_different_networks", error.Code);
    }

    [Fact]
    public void AMissingSensor_IsReportedAsNotFound()
    {
        var error = Assert.Throws<DomainException>(
            () => TransferPolicy.EnsureAllowed(Sensor(), null));

        Assert.Equal(DomainErrorKind.NotFound, error.Kind);
        Assert.Equal("transfer_sensor_not_found", error.Code);
    }

    // An inactive sensor on another network must report the inactive sensor,
    // because that is the condition the caller can act on first.
    [Fact]
    public void WhenSeveralRulesFail_TheInactiveSensorIsReportedFirst()
    {
        var error = Assert.Throws<DomainException>(() => TransferPolicy.EnsureAllowed(
            Sensor(isActive: false),
            Sensor(network: Guid.NewGuid())));

        Assert.Equal("inactive_transfer_sensor", error.Code);
    }
}
