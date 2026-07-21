using EcoMyceliumTracker.Contracts;
using EcoMyceliumTracker.Infrastructure.Errors;
using EcoMyceliumTracker.Models;
using EcoMyceliumTracker.Repositories;
using EcoMyceliumTracker.Validation;

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
        IMyceliumRepository repository,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 20,
        string? scientificName = null,
        string? soilType = null)
    {
        var errors = RequestValidators.ValidatePagination(page, pageSize);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var result = await repository.GetPageAsync(
            page,
            pageSize,
            scientificName,
            soilType,
            cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetByIdAsync(
        Guid id,
        IMyceliumRepository repository,
        CancellationToken cancellationToken)
    {
        var network = await repository.GetByIdAsync(id, cancellationToken);
        return network is null
            ? ApiResults.NotFound("A rede informada não existe.")
            : Results.Ok(network);
    }

    private static async Task<IResult> CreateAsync(
        CreateMyceliumNetworkRequest request,
        IMyceliumRepository repository,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var errors = RequestValidators.Validate(request, timeProvider.GetUtcNow());
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var network = ToModel(request.ScientificName!, request.SoilType!, request.DiscoveredAt);
        var created = await repository.CreateAsync(network, cancellationToken);
        return Results.Created($"/api/networks/{created.Id}", created);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateMyceliumNetworkRequest request,
        IMyceliumRepository repository,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var errors = RequestValidators.Validate(request, timeProvider.GetUtcNow());
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var network = ToModel(request.ScientificName!, request.SoilType!, request.DiscoveredAt);
        var updated = await repository.UpdateAsync(id, network, cancellationToken);
        return updated is null
            ? ApiResults.NotFound("A rede informada não existe.")
            : Results.Ok(updated);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        IMyceliumRepository repository,
        CancellationToken cancellationToken)
    {
        var deleted = await repository.DeleteAsync(id, cancellationToken);
        return deleted
            ? Results.NoContent()
            : ApiResults.NotFound("A rede informada não existe.");
    }

    private static MyceliumNetwork ToModel(
        string scientificName,
        string soilType,
        DateTimeOffset discoveredAt) =>
        new()
        {
            ScientificName = scientificName.Trim(),
            SoilType = soilType.Trim(),
            DiscoveredAt = discoveredAt.ToUniversalTime()
        };
}
