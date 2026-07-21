using EcoMyceliumTracker.Application;
using EcoMyceliumTracker.Contracts;

namespace EcoMyceliumTracker.Endpoints;

public static class NetworkEndpoints
{
    public static RouteGroupBuilder MapNetworkEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/networks").WithTags("Networks");

        group.MapGet("/", GetPageAsync).WithName("GetNetworks");
        group.MapGet("/{id:guid}", GetByIdAsync).WithName("GetNetworkById");
        group.MapPost("/", CreateAsync).WithName("CreateNetwork");
        group.MapPut("/{id:guid}", UpdateAsync).WithName("UpdateNetwork");
        group.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteNetwork");

        return api;
    }

    private static async Task<IResult> GetPageAsync(
        NetworkService service,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 20,
        string? scientificName = null,
        string? soilType = null) =>
        Results.Ok(await service.GetPageAsync(page, pageSize, scientificName, soilType, cancellationToken));

    private static async Task<IResult> GetByIdAsync(
        Guid id,
        NetworkService service,
        CancellationToken cancellationToken) =>
        Results.Ok(await service.GetByIdAsync(id, cancellationToken));

    private static async Task<IResult> CreateAsync(
        CreateMyceliumNetworkRequest request,
        NetworkService service,
        CancellationToken cancellationToken)
    {
        var created = await service.CreateAsync(request, cancellationToken);
        return Results.Created($"/api/networks/{created.Id}", created);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateMyceliumNetworkRequest request,
        NetworkService service,
        CancellationToken cancellationToken) =>
        Results.Ok(await service.UpdateAsync(id, request, cancellationToken));

    private static async Task<IResult> DeleteAsync(
        Guid id,
        NetworkService service,
        CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return Results.NoContent();
    }
}
