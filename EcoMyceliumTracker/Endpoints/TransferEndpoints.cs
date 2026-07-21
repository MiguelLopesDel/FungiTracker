using EcoMyceliumTracker.Configuration;
using EcoMyceliumTracker.Contracts;
using EcoMyceliumTracker.Infrastructure.Errors;
using EcoMyceliumTracker.Models;
using EcoMyceliumTracker.Repositories;
using EcoMyceliumTracker.Validation;
using Microsoft.Extensions.Options;

namespace EcoMyceliumTracker.Endpoints;

public static class TransferEndpoints
{
    public static RouteGroupBuilder MapTransferEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/transfers").WithTags("Transfers");

        group.MapGet("/", GetPageAsync).WithName("GetTransfers");
        group.MapGet("/high-energy", GetHighEnergyAsync).WithName("GetHighEnergyTransfers");
        group.MapGet("/{id:long}", GetByIdAsync).WithName("GetTransferById");
        group.MapPost("/", CreateAsync).WithName("CreateTransfer");

        return api;
    }

    private static Task<IResult> GetPageAsync(
        ITransferRepository repository,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 20,
        int? minimumCarbonMg = null,
        Guid? sourceNodeId = null,
        Guid? targetNodeId = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null) =>
        GetFilteredPageAsync(
            repository,
            page,
            pageSize,
            minimumCarbonMg,
            sourceNodeId,
            targetNodeId,
            from,
            to,
            cancellationToken);

    private static Task<IResult> GetHighEnergyAsync(
        ITransferRepository repository,
        IOptions<TransferOptions> options,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 20) =>
        GetFilteredPageAsync(
            repository,
            page,
            pageSize,
            options.Value.HighEnergyThresholdMg,
            null,
            null,
            null,
            null,
            cancellationToken);

    private static async Task<IResult> GetFilteredPageAsync(
        ITransferRepository repository,
        int page,
        int pageSize,
        int? minimumCarbonMg,
        Guid? sourceNodeId,
        Guid? targetNodeId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var errors = RequestValidators.ValidatePagination(page, pageSize);
        if (minimumCarbonMg < 0)
        {
            errors[nameof(minimumCarbonMg)] = ["O valor mínimo de carbono não pode ser negativo."];
        }

        if (from > to)
        {
            errors[nameof(from)] = ["A data inicial deve ser anterior à data final."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var result = await repository.GetPageAsync(
            page,
            pageSize,
            minimumCarbonMg,
            sourceNodeId,
            targetNodeId,
            from?.ToUniversalTime(),
            to?.ToUniversalTime(),
            cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetByIdAsync(
        long id,
        ITransferRepository repository,
        CancellationToken cancellationToken)
    {
        var transfer = await repository.GetByIdAsync(id, cancellationToken);
        return transfer is null
            ? ApiResults.NotFound("A transferência informada não existe.")
            : Results.Ok(transfer);
    }

    private static async Task<IResult> CreateAsync(
        CreateNutrientTransferRequest request,
        ITransferRepository repository,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var errors = RequestValidators.Validate(request, now);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var transfer = new NutrientTransfer
        {
            SourceNodeId = request.SourceNodeId,
            TargetNodeId = request.TargetNodeId,
            CarbonAmountMg = request.CarbonAmountMg,
            TransferredAt = (request.TransferredAt ?? now).ToUniversalTime()
        };
        var created = await repository.CreateAsync(transfer, cancellationToken);
        return Results.Created($"/api/transfers/{created.Id}", created);
    }
}
