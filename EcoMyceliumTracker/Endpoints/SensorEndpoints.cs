using EcoMyceliumTracker.Contracts;
using EcoMyceliumTracker.Infrastructure.Errors;
using EcoMyceliumTracker.Models;
using EcoMyceliumTracker.Repositories;
using EcoMyceliumTracker.Validation;

namespace EcoMyceliumTracker.Endpoints;

public static class SensorEndpoints
{
    public static RouteGroupBuilder MapSensorEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/networks/{networkId:guid}/sensors", GetPageAsync)
            .WithName("GetNetworkSensors")
            .WithTags("Sensors");

        var group = api.MapGroup("/sensors").WithTags("Sensors");
        group.MapGet("/{id:guid}", GetByIdAsync).WithName("GetSensorById");
        group.MapPost("/", CreateAsync).WithName("CreateSensor");
        group.MapPut("/{id:guid}", UpdateAsync).WithName("UpdateSensor");
        group.MapPatch("/{id:guid}/status", SetStatusAsync).WithName("SetSensorStatus");
        group.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteSensor");

        return api;
    }

    private static async Task<IResult> GetPageAsync(
        Guid networkId,
        ISensorRepository repository,
        IMyceliumRepository networkRepository,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 20,
        bool? isActive = null)
    {
        var errors = RequestValidators.ValidatePagination(page, pageSize);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        if (await networkRepository.GetByIdAsync(networkId, cancellationToken) is null)
        {
            return ApiResults.NotFound("A rede informada não existe.");
        }

        var result = await repository.GetPageByNetworkIdAsync(
            networkId,
            page,
            pageSize,
            isActive,
            cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetByIdAsync(
        Guid id,
        ISensorRepository repository,
        CancellationToken cancellationToken)
    {
        var sensor = await repository.GetByIdAsync(id, cancellationToken);
        return sensor is null
            ? ApiResults.NotFound("O sensor informado não existe.")
            : Results.Ok(sensor);
    }

    private static async Task<IResult> CreateAsync(
        CreateSensorNodeRequest request,
        ISensorRepository repository,
        IMyceliumRepository networkRepository,
        CancellationToken cancellationToken)
    {
        var errors = RequestValidators.Validate(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        if (await networkRepository.GetByIdAsync(request.NetworkId, cancellationToken) is null)
        {
            return ApiResults.NotFound("A rede informada não existe.");
        }

        var sensor = new SensorNode
        {
            NetworkId = request.NetworkId,
            Location = request.Location!.Trim(),
            MoistureLevel = request.MoistureLevel,
            IsActive = request.IsActive
        };
        var created = await repository.CreateAsync(sensor, cancellationToken);
        return Results.Created($"/api/sensors/{created.Id}", created);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateSensorNodeRequest request,
        ISensorRepository repository,
        CancellationToken cancellationToken)
    {
        var errors = RequestValidators.Validate(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var sensor = new SensorNode
        {
            Location = request.Location!.Trim(),
            MoistureLevel = request.MoistureLevel,
            IsActive = request.IsActive
        };
        var updated = await repository.UpdateAsync(id, sensor, cancellationToken);
        return updated is null
            ? ApiResults.NotFound("O sensor informado não existe.")
            : Results.Ok(updated);
    }

    private static async Task<IResult> SetStatusAsync(
        Guid id,
        SetSensorStatusRequest request,
        ISensorRepository repository,
        CancellationToken cancellationToken)
    {
        var updated = await repository.SetActiveAsync(id, request.IsActive, cancellationToken);
        return updated is null
            ? ApiResults.NotFound("O sensor informado não existe.")
            : Results.Ok(updated);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        ISensorRepository repository,
        CancellationToken cancellationToken)
    {
        var deleted = await repository.DeleteAsync(id, cancellationToken);
        return deleted
            ? Results.NoContent()
            : ApiResults.NotFound("O sensor informado não existe.");
    }
}
