using EcoMyceliumTracker.Contracts;
using EcoMyceliumTracker.Models;

namespace EcoMyceliumTracker.Repositories;

public interface ITransferRepository
{
    Task<PagedResult<TransferSummary>> GetPageAsync(
        int page,
        int pageSize,
        TransferFilter filter,
        CancellationToken cancellationToken = default);

    Task<NutrientTransfer?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<NutrientTransfer> CreateAsync(NutrientTransfer transfer, CancellationToken cancellationToken = default);
}
