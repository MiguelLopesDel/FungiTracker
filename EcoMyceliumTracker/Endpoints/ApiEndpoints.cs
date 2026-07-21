namespace EcoMyceliumTracker.Endpoints;

public static class ApiEndpoints
{
    public static IEndpointRouteBuilder MapApi(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        api.MapNetworkEndpoints();
        api.MapSensorEndpoints();
        api.MapTransferEndpoints();

        return endpoints;
    }
}
