using EcoMyceliumTracker.Models;

namespace EcoMyceliumTracker.Repositories;

public interface ISensorRepository
{
    Task<PagedResult<SensorNode>> GetPageByNetworkIdAsync(
        Guid networkId,
        int page,
        int pageSize,
        bool? isActive,
        CancellationToken cancellationToken = default);

    Task<SensorNode?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SensorNode> CreateAsync(SensorNode sensor, CancellationToken cancellationToken = default);
    Task<SensorNode?> UpdateAsync(Guid id, SensorNode sensor, CancellationToken cancellationToken = default);
    Task<SensorNode?> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
