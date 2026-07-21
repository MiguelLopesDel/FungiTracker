using EcoMyceliumTracker.Models;

namespace EcoMyceliumTracker.Repositories;

public interface IMyceliumRepository
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
