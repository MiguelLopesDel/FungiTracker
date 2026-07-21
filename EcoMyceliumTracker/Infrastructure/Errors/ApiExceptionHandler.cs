using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace EcoMyceliumTracker.Infrastructure.Errors;

public sealed class ApiExceptionHandler(
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail, code) = MapException(exception);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled API exception for {Path}", httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning(
                "API request failed with {StatusCode} and code {ErrorCode} for {Path}",
                status,
                code,
                httpContext.Request.Path);
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);

        return true;
    }

    private static (int Status, string Title, string Detail, string Code) MapException(Exception exception) =>
        exception switch
        {
            DomainException domain =>
                (domain.StatusCode, "Business rule violation", domain.Message, domain.Code),
            PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } =>
                (StatusCodes.Status409Conflict, "Resource conflict",
                    "A resource with the same unique values already exists.", "duplicate_resource"),
            PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } =>
                (StatusCodes.Status409Conflict, "Relationship conflict",
                    "The referenced resource does not exist or is still in use.", "foreign_key_conflict"),
            PostgresException { SqlState: PostgresErrorCodes.CheckViolation } =>
                (StatusCodes.Status422UnprocessableEntity, "Business rule violation",
                    "The supplied data violates a database rule.", "constraint_violation"),
            PostgresException { SqlState: PostgresErrorCodes.NotNullViolation } =>
                (StatusCodes.Status400BadRequest, "Invalid request",
                    "A required value was not supplied.", "required_value_missing"),
            _ =>
                (StatusCodes.Status500InternalServerError, "Unexpected error",
                    "An unexpected error occurred while processing the request.", "unexpected_error")
        };
}
