using EcoMyceliumTracker.Configuration;
using EcoMyceliumTracker.Contracts;
using EcoMyceliumTracker.Models;
using EcoMyceliumTracker.Repositories;
using EcoMyceliumTracker.Validation;
using Microsoft.Extensions.Options;

namespace EcoMyceliumTracker.Application;

public sealed class TransferService(
    ITransferRepository repository,
    IOptions<TransferOptions> options,
    TimeProvider timeProvider)
{
    // async on purpose, even though it only delegates: a non-async method
    // throws when it is called rather than when it is awaited, which would
    // make this behave differently from every other service method.
    public async Task<PagedResult<TransferSummary>> GetPageAsync(
        int page,
        int pageSize,
        TransferFilter filter,
        CancellationToken cancellationToken = default)
    {
        var errors = RequestValidators.ValidateTransferFilters(page, pageSize, filter);
        if (errors.Count > 0)
        {
            throw DomainException.InvalidRequest(errors);
        }

        return await repository.GetPageAsync(page, pageSize, filter.ToUniversalTime(), cancellationToken);
    }

    /// <summary>
    /// Transfers at or above the configured threshold. The bound is inclusive.
    /// </summary>
    public async Task<PagedResult<TransferSummary>> GetHighEnergyPageAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        await GetPageAsync(
            page,
            pageSize,
            new TransferFilter { MinimumCarbonMg = options.Value.HighEnergyThresholdMg },
            cancellationToken);

    public async Task<NutrientTransfer> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        await repository.GetByIdAsync(id, cancellationToken)
            ?? throw DomainException.NotFound(
                "A transferência informada não existe.",
                "transfer_not_found");

    public async Task<NutrientTransfer> CreateAsync(
        CreateNutrientTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var errors = RequestValidators.Validate(request, now);
        if (errors.Count > 0)
        {
            throw DomainException.InvalidRequest(errors);
        }

        return await repository.CreateAsync(
            new NutrientTransfer
            {
                SourceNodeId = request.SourceNodeId,
                TargetNodeId = request.TargetNodeId,
                CarbonAmountMg = request.CarbonAmountMg,
                TransferredAt = (request.TransferredAt ?? now).ToUniversalTime(),
            },
            cancellationToken);
    }
}
