using EcoMyceliumTracker.Application;
using EcoMyceliumTracker.Contracts;
using EcoMyceliumTracker.Domain;
using EcoMyceliumTracker.Validation;

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

    // The filter binds straight from the query string, so there is no second
    // type mirroring its fields. Paging stays as ordinary parameters, whose
    // defaults [AsParameters] would otherwise overwrite with default(int).
    private static async Task<IResult> GetPageAsync(
        [AsParameters] TransferFilter filter,
        TransferService service,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = RequestValidators.DefaultPageSize) =>
        Results.Ok(await service.GetPageAsync(page, pageSize, filter, cancellationToken));

    private static async Task<IResult> GetHighEnergyAsync(
        TransferService service,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = RequestValidators.DefaultPageSize) =>
        Results.Ok(await service.GetHighEnergyPageAsync(page, pageSize, cancellationToken));

    private static async Task<IResult> GetByIdAsync(
        long id,
        TransferService service,
        CancellationToken cancellationToken) =>
        Results.Ok(await service.GetByIdAsync(id, cancellationToken));

    private static async Task<IResult> CreateAsync(
        CreateNutrientTransferRequest request,
        TransferService service,
        CancellationToken cancellationToken)
    {
        var created = await service.CreateAsync(request, cancellationToken);
        return Results.Created($"/api/transfers/{created.Id}", created);
    }
}
