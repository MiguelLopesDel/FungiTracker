using System.Diagnostics.CodeAnalysis;

namespace EcoMyceliumTracker.Infrastructure.Errors;

[SuppressMessage(
    "Design",
    "CA1032:Implement standard exception constructors",
    Justification = "Every domain error carries a status code and an error code. " +
        "The standard constructors would allow building one without them, so " +
        "instances are created through the factory methods below instead.")]
public sealed class DomainException(
    string message,
    int statusCode,
    string code) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;

    public static DomainException Validation(string message, string code = "validation_error") =>
        new(message, StatusCodes.Status422UnprocessableEntity, code);

    public static DomainException NotFound(string message, string code = "not_found") =>
        new(message, StatusCodes.Status404NotFound, code);

    public static DomainException Conflict(string message, string code = "conflict") =>
        new(message, StatusCodes.Status409Conflict, code);
}
