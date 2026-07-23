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
    /// <summary>
    /// Both sensors, locked against change until the current transaction ends.
    /// </summary>
    Task<IReadOnlyList<TransferSensor>> GetForTransferAsync(
        Guid sourceNodeId,
        Guid targetNodeId,
        CancellationToken cancellationToken = default);

    Task<NutrientTransfer> AddAsync(NutrientTransfer transfer, CancellationToken cancellationToken = default);
}
