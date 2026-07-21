using EcoMyceliumTracker.Application;
using EcoMyceliumTracker.Contracts;

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

    private static async Task<IResult> GetPageAsync(
        TransferService service,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 20,
        int? minimumCarbonMg = null,
        Guid? sourceNodeId = null,
        Guid? targetNodeId = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null) =>
        Results.Ok(await service.GetPageAsync(
            page,
            pageSize,
            minimumCarbonMg,
            sourceNodeId,
            targetNodeId,
            from,
            to,
            cancellationToken));

    private static async Task<IResult> GetHighEnergyAsync(
        TransferService service,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 20) =>
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
