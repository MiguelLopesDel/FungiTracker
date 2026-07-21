using EcoMyceliumTracker.Contracts;
using EcoMyceliumTracker.Validation;

namespace EcoMyceliumTracker.UnitTests;

public sealed class RequestValidatorsTests
{
    [Fact]
    public void ValidateNetwork_WithMissingRequiredValues_ReturnsErrors()
    {
        var request = new CreateMyceliumNetworkRequest(null, " ", default);

        var errors = RequestValidators.Validate(request, DateTimeOffset.UtcNow);

        Assert.Equal(3, errors.Count);
    }

    [Fact]
    public void ValidateSensor_WithOutOfRangeMoisture_ReturnsError()
    {
        var request = new CreateSensorNodeRequest(Guid.NewGuid(), "10,20", 100.01m, true);

        var errors = RequestValidators.Validate(request);

        Assert.Contains("moistureLevel", errors.Keys);
    }

    [Fact]
    public void ValidateTransfer_WithFutureTimestampAndSameSensor_ReturnsErrors()
    {
        var sensorId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var request = new CreateNutrientTransferRequest(
            sensorId,
            sensorId,
            0,
            now.AddMinutes(1));

        var errors = RequestValidators.Validate(request, now);

        Assert.Contains(nameof(request.TargetNodeId), errors.Keys);
        Assert.Contains(nameof(request.CarbonAmountMg), errors.Keys);
        Assert.Contains(nameof(request.TransferredAt), errors.Keys);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void ValidatePagination_WithInvalidValues_ReturnsErrors(int page, int pageSize)
    {
        Assert.NotEmpty(RequestValidators.ValidatePagination(page, pageSize));
    }

    // The boundaries below are the values where an off-by-one in a comparison
    // would flip the outcome, so each one pins down a single limit.

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void ValidateSensor_WithMoistureAtRangeLimits_ReturnsNoError(decimal moistureLevel)
    {
        var request = new CreateSensorNodeRequest(Guid.NewGuid(), "10,20", moistureLevel, true);

        Assert.DoesNotContain("moistureLevel", RequestValidators.Validate(request).Keys);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(RequestValidators.MaximumPageSize)]
    public void ValidatePagination_WithPageSizeAtLimits_ReturnsNoErrors(int pageSize)
    {
        Assert.Empty(RequestValidators.ValidatePagination(1, pageSize));
    }

    [Fact]
    public void ValidateNetwork_WithNamesAtMaximumLength_ReturnsNoErrors()
    {
        var request = new CreateMyceliumNetworkRequest(
            new string('a', 200),
            new string('b', 100),
            DateTimeOffset.UtcNow);

        Assert.Empty(RequestValidators.Validate(request, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ValidateNetwork_WithNamesOverMaximumLength_ReturnsErrors()
    {
        var request = new CreateMyceliumNetworkRequest(
            new string('a', 201),
            new string('b', 101),
            DateTimeOffset.UtcNow);

        var errors = RequestValidators.Validate(request, DateTimeOffset.UtcNow);

        Assert.Contains("scientificName", errors.Keys);
        Assert.Contains("soilType", errors.Keys);
    }

    [Fact]
    public void ValidateTransfer_WithTimestampExactlyNow_ReturnsNoError()
    {
        var now = DateTimeOffset.UtcNow;
        var request = new CreateNutrientTransferRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            10,
            now);

        Assert.DoesNotContain(nameof(request.TransferredAt), RequestValidators.Validate(request, now).Keys);
    }

    [Fact]
    public void ValidateNetwork_WithFutureDiscoveryDate_ReturnsError()
    {
        var now = DateTimeOffset.UtcNow;
        var request = new CreateMyceliumNetworkRequest("Armillaria", "Florestal", now.AddDays(1));

        Assert.Contains("discoveredAt", RequestValidators.Validate(request, now).Keys);
    }

    [Fact]
    public void ValidateNetwork_WithDiscoveryDateExactlyNow_ReturnsNoError()
    {
        var now = DateTimeOffset.UtcNow;
        var request = new CreateMyceliumNetworkRequest("Armillaria", "Florestal", now);

        Assert.DoesNotContain("discoveredAt", RequestValidators.Validate(request, now).Keys);
    }

    [Fact]
    public void ValidateNetworkUpdate_WithFutureDiscoveryDate_ReturnsError()
    {
        var now = DateTimeOffset.UtcNow;
        var request = new UpdateMyceliumNetworkRequest("Armillaria", "Florestal", now.AddSeconds(1));

        Assert.Contains("discoveredAt", RequestValidators.Validate(request, now).Keys);
    }

    [Fact]
    public void ValidateSensor_WithEmptyNetworkId_ReturnsError()
    {
        var request = new CreateSensorNodeRequest(Guid.Empty, "10,20", 50, true);

        Assert.Contains(nameof(request.NetworkId), RequestValidators.Validate(request).Keys);
    }

    [Fact]
    public void ValidateTransfer_WithEmptySensorIds_ReturnsErrors()
    {
        var now = DateTimeOffset.UtcNow;
        var request = new CreateNutrientTransferRequest(Guid.Empty, Guid.Empty, 10, now);

        var errors = RequestValidators.Validate(request, now);

        Assert.Contains(nameof(request.SourceNodeId), errors.Keys);
        Assert.Contains(nameof(request.TargetNodeId), errors.Keys);
    }

    [Fact]
    public void Validate_WithValidRequests_ReturnsNoErrors()
    {
        var now = DateTimeOffset.UtcNow;
        var network = new CreateMyceliumNetworkRequest("Armillaria ostoyae", "Florestal", now);
        var sensor = new CreateSensorNodeRequest(Guid.NewGuid(), "1.5,2.5", 50, true);
        var transfer = new CreateNutrientTransferRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            500,
            now.AddMinutes(-1));

        Assert.Empty(RequestValidators.Validate(network, now));
        Assert.Empty(RequestValidators.Validate(sensor));
        Assert.Empty(RequestValidators.Validate(transfer, now));
        Assert.Empty(RequestValidators.ValidatePagination(1, 100));
    }
}
