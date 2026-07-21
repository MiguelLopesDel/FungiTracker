using EcoMyceliumTracker.Application;
using EcoMyceliumTracker.Contracts;
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
        SensorService service,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = RequestValidators.DefaultPageSize,
        bool? isActive = null) =>
        Results.Ok(await service.GetPageByNetworkIdAsync(
            networkId,
            page,
            pageSize,
            isActive,
            cancellationToken));

    private static async Task<IResult> GetByIdAsync(
        Guid id,
        SensorService service,
        CancellationToken cancellationToken) =>
        Results.Ok(await service.GetByIdAsync(id, cancellationToken));

    private static async Task<IResult> CreateAsync(
        CreateSensorNodeRequest request,
        SensorService service,
        CancellationToken cancellationToken)
    {
        var created = await service.CreateAsync(request, cancellationToken);
        return Results.Created($"/api/sensors/{created.Id}", created);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateSensorNodeRequest request,
        SensorService service,
        CancellationToken cancellationToken) =>
        Results.Ok(await service.UpdateAsync(id, request, cancellationToken));

    private static async Task<IResult> SetStatusAsync(
        Guid id,
        SetSensorStatusRequest request,
        SensorService service,
        CancellationToken cancellationToken) =>
        Results.Ok(await service.SetActiveAsync(id, request.IsActive, cancellationToken));

    private static async Task<IResult> DeleteAsync(
        Guid id,
        SensorService service,
        CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return Results.NoContent();
    }
}
