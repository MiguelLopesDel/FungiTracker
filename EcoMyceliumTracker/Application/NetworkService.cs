using EcoMyceliumTracker.Contracts;
using EcoMyceliumTracker.Domain;
using EcoMyceliumTracker.Models;
using EcoMyceliumTracker.Repositories;
using EcoMyceliumTracker.Validation;

namespace EcoMyceliumTracker.Application;

public sealed class NetworkService(
    IMyceliumRepository repository,
    TimeProvider timeProvider)
{
    public async Task<PagedResult<MyceliumNetwork>> GetPageAsync(
        int page,
        int pageSize,
        string? scientificName,
        string? soilType,
        CancellationToken cancellationToken = default)
    {
        EnsureValidPagination(page, pageSize);

        return await repository.GetPageAsync(page, pageSize, scientificName, soilType, cancellationToken);
    }

    public async Task<MyceliumNetwork> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await repository.GetByIdAsync(id, cancellationToken) ?? throw NetworkNotFound();

    public async Task<MyceliumNetwork> CreateAsync(
        CreateMyceliumNetworkRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = RequestValidators.Validate(request, timeProvider.GetUtcNow());
        if (errors.Count > 0)
        {
            throw DomainException.InvalidRequest(errors);
        }

        return await repository.CreateAsync(
            ToModel(
                Validated.Required(request.ScientificName),
                Validated.Required(request.SoilType),
                request.DiscoveredAt),
            cancellationToken);
    }

    public async Task<MyceliumNetwork> UpdateAsync(
        Guid id,
        UpdateMyceliumNetworkRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = RequestValidators.Validate(request, timeProvider.GetUtcNow());
        if (errors.Count > 0)
        {
            throw DomainException.InvalidRequest(errors);
        }

        return await repository.UpdateAsync(
            id,
            ToModel(
                Validated.Required(request.ScientificName),
                Validated.Required(request.SoilType),
                request.DiscoveredAt),
            cancellationToken) ?? throw NetworkNotFound();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await repository.DeleteAsync(id, cancellationToken))
        {
            throw NetworkNotFound();
        }
    }

    internal static void EnsureValidPagination(int page, int pageSize)
    {
        var errors = RequestValidators.ValidatePagination(page, pageSize);
        if (errors.Count > 0)
        {
            throw DomainException.InvalidRequest(errors);
        }
    }

    internal static DomainException NetworkNotFound() =>
        DomainException.NotFound("A rede informada não existe.", "network_not_found");

    private static MyceliumNetwork ToModel(
        string scientificName,
        string soilType,
        DateTimeOffset discoveredAt) =>
        new()
        {
            ScientificName = scientificName.Trim(),
            SoilType = soilType.Trim(),
            DiscoveredAt = discoveredAt.ToUniversalTime(),
        };
}
