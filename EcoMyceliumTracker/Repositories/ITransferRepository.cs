namespace EcoMyceliumTracker.Repositories;

public interface ITransferRepository
{
    Task<IEnumerable<object>> GetHighEnergyTransfersAsync();
    Task<long> CreateAsync(Guid sourceId, Guid targetId, int carbonAmount);
}