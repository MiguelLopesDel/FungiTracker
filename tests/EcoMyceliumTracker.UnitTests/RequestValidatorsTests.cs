using EcoMyceliumTracker.Contracts;
using EcoMyceliumTracker.Validation;

namespace EcoMyceliumTracker.UnitTests;

public sealed class RequestValidatorsTests
{
    [Fact]
    public void ValidateNetwork_WithMissingRequiredValues_ReturnsErrors()
    {
        var request = new CreateMyceliumNetworkRequest(null, " ", default);

        var errors = RequestValidators.Validate(request);

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

        Assert.Empty(RequestValidators.Validate(network));
        Assert.Empty(RequestValidators.Validate(sensor));
        Assert.Empty(RequestValidators.Validate(transfer, now));
        Assert.Empty(RequestValidators.ValidatePagination(1, 100));
    }
}
