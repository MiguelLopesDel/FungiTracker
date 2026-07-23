using EcoMyceliumTracker.Application;
using EcoMyceliumTracker.Configuration;
using EcoMyceliumTracker.Contracts;
using EcoMyceliumTracker.Domain;
using EcoMyceliumTracker.Models;
using EcoMyceliumTracker.UnitTests.Fakes;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace EcoMyceliumTracker.UnitTests;

/// <summary>
/// These rules used to be reachable only through HTTP and PostgreSQL. They are
/// now exercised directly against the services.
/// </summary>
public sealed class ServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static FakeTimeProvider Clock() => new(Now);

    // ---------- networks ----------

    [Fact]
    public async Task CreateNetwork_WithFutureDiscoveryDate_IsRejectedAsValidation()
    {
        var service = new NetworkService(new FakeNetworkRepository(), Clock());

        var error = await Assert.ThrowsAsync<DomainException>(() => service.CreateAsync(
            new CreateMyceliumNetworkRequest("Armillaria", "Florestal", Now.AddDays(1))));

        Assert.Equal(DomainErrorKind.Validation, error.Kind);
        Assert.NotNull(error.Errors);
        Assert.Contains("discoveredAt", error.Errors);
    }

    [Fact]
    public async Task CreateNetwork_TrimsTextAndNormalisesTheDateToUtc()
    {
        var repository = new FakeNetworkRepository();
        var service = new NetworkService(repository, Clock());

        var created = await service.CreateAsync(new CreateMyceliumNetworkRequest(
            "  Armillaria ostoyae  ",
            "  Florestal  ",
            new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.FromHours(-3))));

        Assert.Equal("Armillaria ostoyae", created.ScientificName);
        Assert.Equal("Florestal", created.SoilType);
        Assert.Equal(TimeSpan.Zero, created.DiscoveredAt.Offset);
        Assert.Equal(12, created.DiscoveredAt.Hour);
    }

    [Fact]
    public async Task GetNetwork_WhenMissing_IsReportedAsNotFound()
    {
        var service = new NetworkService(new FakeNetworkRepository(), Clock());

        var error = await Assert.ThrowsAsync<DomainException>(() => service.GetByIdAsync(Guid.NewGuid()));

        Assert.Equal(DomainErrorKind.NotFound, error.Kind);
        Assert.Equal("network_not_found", error.Code);
    }

    [Fact]
    public async Task DeleteNetwork_WhenMissing_IsReportedAsNotFound()
    {
        var service = new NetworkService(new FakeNetworkRepository(), Clock());

        var error = await Assert.ThrowsAsync<DomainException>(() => service.DeleteAsync(Guid.NewGuid()));

        Assert.Equal(DomainErrorKind.NotFound, error.Kind);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task ListNetworks_WithInvalidPagination_IsRejected(int page, int pageSize)
    {
        var service = new NetworkService(new FakeNetworkRepository(), Clock());

        var error = await Assert.ThrowsAsync<DomainException>(
            () => service.GetPageAsync(page, pageSize, null, null));

        Assert.Equal(DomainErrorKind.Validation, error.Kind);
    }

    // ---------- sensors ----------

    [Fact]
    public async Task CreateSensor_ForUnknownNetwork_IsReportedAsNotFound()
    {
        var service = new SensorService(new FakeSensorRepository(), new FakeNetworkRepository());

        var error = await Assert.ThrowsAsync<DomainException>(() => service.CreateAsync(
            new CreateSensorNodeRequest(Guid.NewGuid(), "10,20", 50, true)));

        Assert.Equal(DomainErrorKind.NotFound, error.Kind);
        Assert.Equal("network_not_found", error.Code);
    }

    [Fact]
    public async Task CreateSensor_WithUnparsableLocation_IsRejectedBeforeReachingTheRepository()
    {
        var sensors = new FakeSensorRepository();
        var networks = new FakeNetworkRepository();
        var network = await networks.CreateAsync(new MyceliumNetwork { Id = Guid.NewGuid() });
        var service = new SensorService(sensors, networks);

        var error = await Assert.ThrowsAsync<DomainException>(() => service.CreateAsync(
            new CreateSensorNodeRequest(network.Id, "nao-e-coordenada", 50, true)));

        Assert.Equal(DomainErrorKind.Validation, error.Kind);
        Assert.Empty(sensors.Items);
    }

    [Fact]
    public async Task ListSensors_ForUnknownNetwork_IsReportedAsNotFound()
    {
        var service = new SensorService(new FakeSensorRepository(), new FakeNetworkRepository());

        var error = await Assert.ThrowsAsync<DomainException>(
            () => service.GetPageByNetworkIdAsync(Guid.NewGuid(), 1, 20, null));

        Assert.Equal(DomainErrorKind.NotFound, error.Kind);
    }

    [Fact]
    public async Task SetSensorStatus_WhenMissing_IsReportedAsNotFound()
    {
        var service = new SensorService(new FakeSensorRepository(), new FakeNetworkRepository());

        var error = await Assert.ThrowsAsync<DomainException>(
            () => service.SetActiveAsync(Guid.NewGuid(), false));

        Assert.Equal(DomainErrorKind.NotFound, error.Kind);
        Assert.Equal("sensor_not_found", error.Code);
    }

    [Fact]
    public async Task CreateSensor_TrimsTheLocationAndKeepsTheNetwork()
    {
        var sensors = new FakeSensorRepository();
        var networks = new FakeNetworkRepository();
        var network = await networks.CreateAsync(new MyceliumNetwork { Id = Guid.NewGuid() });
        var service = new SensorService(sensors, networks);

        var created = await service.CreateAsync(
            new CreateSensorNodeRequest(network.Id, "  (10.5, 20.25)  ", 60, true));

        Assert.Equal(new Coordinates(10.5, 20.25), created.Location);
        Assert.Equal(network.Id, created.NetworkId);
        Assert.Single(sensors.Items);
    }

    [Fact]
    public async Task UpdateSensor_WhenMissing_IsReportedAsNotFound()
    {
        var service = new SensorService(new FakeSensorRepository(), new FakeNetworkRepository());

        var error = await Assert.ThrowsAsync<DomainException>(() => service.UpdateAsync(
            Guid.NewGuid(),
            new UpdateSensorNodeRequest("10,20", 50, true)));

        Assert.Equal(DomainErrorKind.NotFound, error.Kind);
    }

    [Fact]
    public async Task UpdateSensor_WithMoistureOutOfRange_IsRejected()
    {
        var service = new SensorService(new FakeSensorRepository(), new FakeNetworkRepository());

        var error = await Assert.ThrowsAsync<DomainException>(() => service.UpdateAsync(
            Guid.NewGuid(),
            new UpdateSensorNodeRequest("10,20", 100.01m, true)));

        Assert.Equal(DomainErrorKind.Validation, error.Kind);
    }

    [Fact]
    public async Task UpdateSensor_AppliesTheNewValues()
    {
        var sensors = new FakeSensorRepository();
        var stored = await sensors.CreateAsync(new SensorNode { Id = Guid.NewGuid(), Location = new Coordinates(1, 2), MoistureLevel = 10 });
        var service = new SensorService(sensors, new FakeNetworkRepository());

        var updated = await service.UpdateAsync(
            stored.Id,
            new UpdateSensorNodeRequest("30,40", 77, false));

        Assert.Equal(new Coordinates(30, 40), updated.Location);
        Assert.Equal(77, updated.MoistureLevel);
        Assert.False(updated.IsActive);
    }

    [Fact]
    public async Task ListSensors_WithInvalidPagination_IsRejectedBeforeTouchingTheNetwork()
    {
        var networks = new FakeNetworkRepository();
        var service = new SensorService(new FakeSensorRepository(), networks);

        var error = await Assert.ThrowsAsync<DomainException>(
            () => service.GetPageByNetworkIdAsync(Guid.NewGuid(), 0, 20, null));

        Assert.Equal(DomainErrorKind.Validation, error.Kind);
    }

    [Fact]
    public async Task DeleteSensor_WhenMissing_IsReportedAsNotFound()
    {
        var service = new SensorService(new FakeSensorRepository(), new FakeNetworkRepository());

        var error = await Assert.ThrowsAsync<DomainException>(
            () => service.DeleteAsync(Guid.NewGuid()));

        Assert.Equal(DomainErrorKind.NotFound, error.Kind);
    }

    [Fact]
    public async Task GetSensor_WhenPresent_IsReturned()
    {
        var sensors = new FakeSensorRepository();
        var stored = await sensors.CreateAsync(new SensorNode { Id = Guid.NewGuid(), Location = new Coordinates(1, 2) });
        var service = new SensorService(sensors, new FakeNetworkRepository());

        Assert.Equal(stored.Id, (await service.GetByIdAsync(stored.Id)).Id);
    }

    [Fact]
    public async Task ListSensors_FiltersByActiveState()
    {
        var sensors = new FakeSensorRepository();
        var networks = new FakeNetworkRepository();
        var network = await networks.CreateAsync(new MyceliumNetwork { Id = Guid.NewGuid() });
        await sensors.CreateAsync(new SensorNode { Id = Guid.NewGuid(), NetworkId = network.Id, IsActive = true });
        await sensors.CreateAsync(new SensorNode { Id = Guid.NewGuid(), NetworkId = network.Id, IsActive = false });
        var service = new SensorService(sensors, networks);

        var page = await service.GetPageByNetworkIdAsync(network.Id, 1, 20, isActive: true);

        Assert.Single(page.Items);
        Assert.True(page.Items[0].IsActive);
    }

    [Fact]
    public async Task UpdateNetwork_WhenMissing_IsReportedAsNotFound()
    {
        var service = new NetworkService(new FakeNetworkRepository(), Clock());

        var error = await Assert.ThrowsAsync<DomainException>(() => service.UpdateAsync(
            Guid.NewGuid(),
            new UpdateMyceliumNetworkRequest("Armillaria", "Florestal", Now.AddDays(-1))));

        Assert.Equal(DomainErrorKind.NotFound, error.Kind);
    }

    [Fact]
    public async Task ListNetworks_WithValidPagination_ReachesTheRepository()
    {
        var repository = new FakeNetworkRepository();
        await repository.CreateAsync(new MyceliumNetwork { Id = Guid.NewGuid(), ScientificName = "Armillaria" });
        var service = new NetworkService(repository, Clock());

        var page = await service.GetPageAsync(1, 20, "armi", null);

        Assert.Single(page.Items);
        Assert.Equal(1, page.TotalPages);
    }

    // ---------- transfers ----------

    [Fact]
    public async Task CreateTransfer_WithoutTimestamp_UsesTheCurrentInstant()
    {
        var service = NewTransferService(out var repository);
        var (source, target) = SeedEligibleSensors(repository);

        var created = await service.CreateAsync(
            new CreateNutrientTransferRequest(source, target, 100));

        Assert.Equal(Now, created.TransferredAt);
    }

    [Fact]
    public async Task CreateTransfer_WithTimestamp_KeepsTheOneSupplied()
    {
        var service = NewTransferService(out var repository);
        var (source, target) = SeedEligibleSensors(repository);
        var supplied = Now.AddHours(-3);

        var created = await service.CreateAsync(
            new CreateNutrientTransferRequest(source, target, 100, supplied));

        Assert.Equal(supplied, created.TransferredAt);
    }

    [Fact]
    public async Task ListTransfers_WithZeroMinimumAndEqualDates_IsAccepted()
    {
        var service = NewTransferService(out _);

        // Zero is a valid floor and an empty interval is a valid range; only
        // negative amounts and inverted intervals are rejected.
        var page = await service.GetPageAsync(
            1,
            20,
            new TransferFilter { MinimumCarbonMg = 0, From = Now, To = Now });

        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task CreateTransfer_WithFutureTimestamp_IsRejected()
    {
        var service = NewTransferService(out var repository);

        var error = await Assert.ThrowsAsync<DomainException>(() => service.CreateAsync(
            new CreateNutrientTransferRequest(Guid.NewGuid(), Guid.NewGuid(), 100, Now.AddSeconds(1))));

        Assert.Equal(DomainErrorKind.Validation, error.Kind);
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task HighEnergyTransfers_UseTheConfiguredThreshold()
    {
        var service = NewTransferService(out var repository, thresholdMg: 750);

        await service.GetHighEnergyPageAsync(1, 20);

        Assert.Equal(750, repository.LastMinimumCarbonMg);
    }

    [Theory]
    [InlineData(-1, null)]
    [InlineData(null, "invertido")]
    public async Task ListTransfers_WithInvalidFilters_IsRejected(int? minimumCarbonMg, string? inverted)
    {
        var service = NewTransferService(out _);
        var from = inverted is null ? (DateTimeOffset?)null : Now;
        var to = inverted is null ? (DateTimeOffset?)null : Now.AddDays(-1);

        var error = await Assert.ThrowsAsync<DomainException>(() => service.GetPageAsync(
            1,
            20,
            new TransferFilter { MinimumCarbonMg = minimumCarbonMg, From = from, To = to }));

        Assert.Equal(DomainErrorKind.Validation, error.Kind);
    }

    [Fact]
    public async Task GetTransfer_WhenMissing_IsReportedAsNotFound()
    {
        var service = NewTransferService(out _);

        var error = await Assert.ThrowsAsync<DomainException>(() => service.GetByIdAsync(42));

        Assert.Equal(DomainErrorKind.NotFound, error.Kind);
    }

    /// <summary>
    /// Two active sensors on the same network, which is what the policy needs
    /// before a transfer between them is allowed.
    /// </summary>
    private static (Guid Source, Guid Target) SeedEligibleSensors(FakeTransferRepository repository)
    {
        var network = Guid.NewGuid();
        var source = new TransferSensor(Guid.NewGuid(), network, IsActive: true, new Coordinates(10, 20));
        var target = new TransferSensor(Guid.NewGuid(), network, IsActive: true, new Coordinates(30, 40));
        repository.Sensors.Add(source);
        repository.Sensors.Add(target);
        return (source.Id, target.Id);
    }

    private static TransferService NewTransferService(
        out FakeTransferRepository repository,
        int thresholdMg = 500)
    {
        repository = new FakeTransferRepository();
        return new TransferService(
            repository,
            new FakeUnitOfWork(),
            Options.Create(new TransferOptions { HighEnergyThresholdMg = thresholdMg }),
            Clock());
    }
}
