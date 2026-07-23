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
        Paging.EnsureValid(page, pageSize);

        return await repository.GetPageAsync(page, pageSize, scientificName, soilType, cancellationToken);
    }

    public async Task<MyceliumNetwork> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await repository.GetByIdAsync(id, cancellationToken) ?? throw NetworkNotFound();

    public async Task<MyceliumNetwork> CreateAsync(
        CreateMyceliumNetworkRequest request,
        CancellationToken cancellationToken = default)
    {
        return await repository.CreateAsync(
            ToModel(ParseOrThrow(request.ScientificName, request.SoilType, request.DiscoveredAt)),
            cancellationToken);
    }

    public async Task<MyceliumNetwork> UpdateAsync(
        Guid id,
        UpdateMyceliumNetworkRequest request,
        CancellationToken cancellationToken = default)
    {
        return await repository.UpdateAsync(
            id,
            ToModel(ParseOrThrow(request.ScientificName, request.SoilType, request.DiscoveredAt)),
            cancellationToken) ?? throw NetworkNotFound();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await repository.DeleteAsync(id, cancellationToken))
        {
            throw NetworkNotFound();
        }
    }

    internal static DomainException NetworkNotFound() =>
        DomainException.NotFound("A rede informada não existe.", "network_not_found");

    private NetworkFields ParseOrThrow(
        string? scientificName,
        string? soilType,
        DateTimeOffset discoveredAt)
    {
        var (errors, fields) = RequestValidators.ParseNetwork(
            scientificName,
            soilType,
            discoveredAt,
            timeProvider.GetUtcNow());

        return errors.Count > 0 ? throw DomainException.InvalidRequest(errors) : fields;
    }

    private static MyceliumNetwork ToModel(NetworkFields fields) =>
        new()
        {
            // The caller decides identity; a repository should persist what it
            // is given, not alter it.
            Id = Guid.NewGuid(),
            ScientificName = fields.ScientificName,
            SoilType = fields.SoilType,
            DiscoveredAt = fields.DiscoveredAt,
        };
}
