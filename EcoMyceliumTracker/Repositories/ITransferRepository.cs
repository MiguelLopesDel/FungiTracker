using EcoMyceliumTracker.Domain;
using EcoMyceliumTracker.Models;

namespace EcoMyceliumTracker.Repositories;

public interface ITransferRepository
{
    Task<PagedResult<NutrientTransferDetails>> GetPageAsync(
        int page,
        int pageSize,
        TransferFilter filter,
        CancellationToken cancellationToken = default);

    Task<NutrientTransferDetails?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<NutrientTransferDetails> CreateAsync(NutrientTransfer transfer, CancellationToken cancellationToken = default);
}
