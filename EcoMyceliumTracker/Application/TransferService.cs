using EcoMyceliumTracker.Configuration;
using EcoMyceliumTracker.Contracts;
using EcoMyceliumTracker.Domain;
using EcoMyceliumTracker.Models;
using EcoMyceliumTracker.Repositories;
using EcoMyceliumTracker.Validation;
using Microsoft.Extensions.Options;

namespace EcoMyceliumTracker.Application;

public sealed class TransferService(
    ITransferRepository repository,
    IUnitOfWork unitOfWork,
    IOptions<TransferOptions> options,
    TimeProvider timeProvider)
{
    // async on purpose, even though it only delegates: a non-async method
    // throws when it is called rather than when it is awaited, which would
    // make this behave differently from every other service method.
    public async Task<PagedResult<NutrientTransferDetails>> GetPageAsync(
        int page,
        int pageSize,
        TransferFilter filter,
        CancellationToken cancellationToken = default)
    {
        Paging.EnsureValid(page, pageSize);
        var errors = filter.Validate();

        if (errors.Count > 0)
        {
            throw DomainException.InvalidRequest(errors);
        }

        return await repository.GetPageAsync(page, pageSize, filter.ToUniversalTime(), cancellationToken);
    }

    /// <summary>
    /// Transfers at or above the configured threshold. The bound is inclusive.
    /// </summary>
    public async Task<PagedResult<NutrientTransferDetails>> GetHighEnergyPageAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        await GetPageAsync(
            page,
            pageSize,
            new TransferFilter { MinimumCarbonMg = options.Value.HighEnergyThresholdMg },
            cancellationToken);

    public async Task<NutrientTransferDetails> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        await repository.GetByIdAsync(id, cancellationToken)
            ?? throw DomainException.NotFound(
                "A transferência informada não existe.",
                "transfer_not_found");

    /// <summary>
    /// Reads both sensors under a lock, applies the policy and inserts, all in
    /// one transaction, so a sensor cannot be deactivated in between.
    /// </summary>
    public async Task<NutrientTransferDetails> CreateAsync(
        CreateNutrientTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var errors = RequestValidators.Validate(request, now);
        if (errors.Count > 0)
        {
            throw DomainException.InvalidRequest(errors);
        }

        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);

        var sensors = await repository.GetForTransferAsync(
            request.SourceNodeId,
            request.TargetNodeId,
            cancellationToken);

        var source = sensors.FirstOrDefault(sensor => sensor.Id == request.SourceNodeId);
        var target = sensors.FirstOrDefault(sensor => sensor.Id == request.TargetNodeId);
        TransferPolicy.EnsureAllowed(source, target);

        var created = await repository.AddAsync(
            new NutrientTransfer
            {
                SourceNodeId = request.SourceNodeId,
                TargetNodeId = request.TargetNodeId,
                CarbonAmountMg = request.CarbonAmountMg,
                TransferredAt = (request.TransferredAt ?? now).ToUniversalTime(),
            },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return NutrientTransferDetails.From(created, source, target);
    }
}
