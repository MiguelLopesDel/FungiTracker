using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace EcoMyceliumTracker.Infrastructure.Errors;

public sealed partial class ApiExceptionHandler(
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Error,
        Message = "Unhandled API exception for {Path}")]
    private static partial void LogUnhandledException(
        ILogger logger,
        string path,
        Exception exception);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "API request failed with {StatusCode} and code {ErrorCode} for {Path}")]
    private static partial void LogFailedRequest(
        ILogger logger,
        int statusCode,
        string errorCode,
        string path);

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail, code) = MapException(exception);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            LogUnhandledException(logger, httpContext.Request.Path, exception);
        }
        else
        {
            LogFailedRequest(logger, status, code, httpContext.Request.Path);
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
            // Model binding reports a malformed body by throwing this with the
            // status it wants. Letting it fall through turned every unparsable
            // payload into a 500 and an error-level log entry.
            BadHttpRequestException badRequest =>
                (badRequest.StatusCode, "Invalid request",
                    "The request body could not be read.", "malformed_request"),
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
