using EcoMyceliumTracker.Application;
using EcoMyceliumTracker.Models;

namespace EcoMyceliumTracker.Repositories;

public interface ITransferRepository
{
    Task<PagedResult<TransferView>> GetPageAsync(
        int page,
        int pageSize,
        TransferFilter filter,
        CancellationToken cancellationToken = default);

    Task<TransferView?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<TransferView> CreateAsync(NutrientTransfer transfer, CancellationToken cancellationToken = default);
}
