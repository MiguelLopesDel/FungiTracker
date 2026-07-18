using EcoMyceliumTracker.Models;

namespace EcoMyceliumTracker.Repositories;

public interface ISensorRepository
{
    Task<IEnumerable<SensorNode>> GetByNetworkIdAsync(Guid networkId);
    Task<Guid> CreateAsync(SensorNode sensor);
}