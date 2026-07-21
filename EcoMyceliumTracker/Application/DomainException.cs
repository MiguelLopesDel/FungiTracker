using System.Diagnostics.CodeAnalysis;

namespace EcoMyceliumTracker.Application;

/// <summary>
/// How a domain rule was broken. The transport layer decides which status code
/// each kind becomes; nothing in here knows that HTTP exists.
/// </summary>
public enum DomainErrorKind
{
    Validation,
    NotFound,
    Conflict,
}

[SuppressMessage(
    "Design",
    "CA1032:Implement standard exception constructors",
    Justification = "Every domain error carries a kind and an error code. The " +
        "standard constructors would allow building one without them, so " +
        "instances are created through the factory methods below.")]
public sealed class DomainException(
    string message,
    DomainErrorKind kind,
    string code,
    IReadOnlyDictionary<string, string[]>? errors = null) : Exception(message)
{
    public DomainErrorKind Kind { get; } = kind;
    public string Code { get; } = code;

    /// <summary>
    /// Field-level messages, when the failure came from validating a request.
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; } = errors;

    public static DomainException Validation(string message, string code = "validation_error") =>
        new(message, DomainErrorKind.Validation, code);

    public static DomainException InvalidRequest(IReadOnlyDictionary<string, string[]> errors) =>
        new("The request contains invalid values.", DomainErrorKind.Validation, "invalid_request", errors);

    public static DomainException NotFound(string message, string code = "resource_not_found") =>
        new(message, DomainErrorKind.NotFound, code);

    public static DomainException Conflict(string message, string code = "conflict") =>
        new(message, DomainErrorKind.Conflict, code);
}
