using EcoMyceliumTracker.Models;

namespace EcoMyceliumTracker.Repositories;

/// <summary>
/// Answers only whether a network is there.
/// </summary>
/// <remarks>
/// SensorService needs nothing else about a network, so it depends on this
/// instead of the full repository, and asking is a cheaper query than
/// fetching a row only to discard it.
/// </remarks>
public interface INetworkExistence
{
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IMyceliumRepository : INetworkExistence
{
    Task<PagedResult<MyceliumNetwork>> GetPageAsync(
        int page,
        int pageSize,
        string? scientificName,
        string? soilType,
        CancellationToken cancellationToken = default);

    Task<MyceliumNetwork?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MyceliumNetwork> CreateAsync(MyceliumNetwork network, CancellationToken cancellationToken = default);
    Task<MyceliumNetwork?> UpdateAsync(Guid id, MyceliumNetwork network, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
