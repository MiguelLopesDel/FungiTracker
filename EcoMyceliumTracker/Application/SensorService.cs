using EcoMyceliumTracker.Contracts;
using EcoMyceliumTracker.Domain;
using EcoMyceliumTracker.Models;
using EcoMyceliumTracker.Repositories;
using EcoMyceliumTracker.Validation;

namespace EcoMyceliumTracker.Application;

public sealed class SensorService(
    ISensorRepository repository,
    INetworkExistence networks)
{
    public async Task<PagedResult<SensorNode>> GetPageByNetworkIdAsync(
        Guid networkId,
        int page,
        int pageSize,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        Paging.EnsureValid(page, pageSize);
        await EnsureNetworkExistsAsync(networkId, cancellationToken);

        return await repository.GetPageByNetworkIdAsync(
            networkId,
            page,
            pageSize,
            isActive,
            cancellationToken);
    }

    public async Task<SensorNode> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await repository.GetByIdAsync(id, cancellationToken) ?? throw SensorNotFound();

    public async Task<SensorNode> CreateAsync(
        CreateSensorNodeRequest request,
        CancellationToken cancellationToken = default)
    {
        var location = Parsed(RequestValidators.ParseSensor(request));
        await EnsureNetworkExistsAsync(request.NetworkId, cancellationToken);

        return await repository.CreateAsync(
            new SensorNode
            {
                Id = Guid.NewGuid(),
                NetworkId = request.NetworkId,
                Location = location,
                MoistureLevel = request.MoistureLevel,
                IsActive = request.IsActive,
            },
            cancellationToken);
    }

    public async Task<SensorNode> UpdateAsync(
        Guid id,
        UpdateSensorNodeRequest request,
        CancellationToken cancellationToken = default)
    {
        var location = Parsed(RequestValidators.ParseSensor(request.Location, request.MoistureLevel));

        return await repository.UpdateAsync(
            id,
            new SensorNode
            {
                Location = location,
                MoistureLevel = request.MoistureLevel,
                IsActive = request.IsActive,
            },
            cancellationToken) ?? throw SensorNotFound();
    }

    public async Task<SensorNode> SetActiveAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default) =>
        await repository.SetActiveAsync(id, isActive, cancellationToken) ?? throw SensorNotFound();

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await repository.DeleteAsync(id, cancellationToken))
        {
            throw SensorNotFound();
        }
    }

    private static Coordinates Parsed((Dictionary<string, string[]> Errors, Coordinates Location) result) =>
        result.Errors.Count > 0
            ? throw DomainException.InvalidRequest(result.Errors)
            : result.Location;

    private async Task EnsureNetworkExistsAsync(Guid networkId, CancellationToken cancellationToken)
    {
        if (!await networks.ExistsAsync(networkId, cancellationToken))
        {
            throw NetworkService.NetworkNotFound();
        }
    }

    private static DomainException SensorNotFound() =>
        DomainException.NotFound("O sensor informado não existe.", "sensor_not_found");
}
