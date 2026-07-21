using EcoMyceliumTracker.Models;

namespace EcoMyceliumTracker.Repositories;

public interface ITransferRepository
{
    Task<PagedResult<TransferSummary>> GetPageAsync(
        int page,
        int pageSize,
        int? minimumCarbonMg,
        Guid? sourceNodeId,
        Guid? targetNodeId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default);

    Task<NutrientTransfer?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<NutrientTransfer> CreateAsync(NutrientTransfer transfer, CancellationToken cancellationToken = default);
}
