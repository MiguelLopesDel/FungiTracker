using EcoMyceliumTracker.Contracts;
using EcoMyceliumTracker.Domain;
using EcoMyceliumTracker.Models;
using EcoMyceliumTracker.Repositories;
using EcoMyceliumTracker.Validation;

namespace EcoMyceliumTracker.Application;

public sealed class SensorService(
    ISensorRepository repository,
    IMyceliumRepository networkRepository)
{
    public async Task<PagedResult<SensorNode>> GetPageByNetworkIdAsync(
        Guid networkId,
        int page,
        int pageSize,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        NetworkService.EnsureValidPagination(page, pageSize);
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
        var errors = RequestValidators.Validate(request);
        if (errors.Count > 0)
        {
            throw DomainException.InvalidRequest(errors);
        }

        await EnsureNetworkExistsAsync(request.NetworkId, cancellationToken);

        return await repository.CreateAsync(
            new SensorNode
            {
                NetworkId = request.NetworkId,
                Location = Validated.Required(request.Location).Trim(),
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
        var errors = RequestValidators.Validate(request);
        if (errors.Count > 0)
        {
            throw DomainException.InvalidRequest(errors);
        }

        return await repository.UpdateAsync(
            id,
            new SensorNode
            {
                Location = Validated.Required(request.Location).Trim(),
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

    private async Task EnsureNetworkExistsAsync(Guid networkId, CancellationToken cancellationToken)
    {
        if (await networkRepository.GetByIdAsync(networkId, cancellationToken) is null)
        {
            throw NetworkService.NetworkNotFound();
        }
    }

    private static DomainException SensorNotFound() =>
        DomainException.NotFound("O sensor informado não existe.", "sensor_not_found");
}
