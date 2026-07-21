namespace EcoMyceliumTracker.Infrastructure.Errors;

public static class ApiResults
{
    public static IResult NotFound(string detail) =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Resource not found",
            detail: detail,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "resource_not_found"
            });
}
