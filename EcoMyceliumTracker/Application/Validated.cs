using System.Runtime.CompilerServices;

namespace EcoMyceliumTracker.Application;

internal static class Validated
{
    /// <summary>
    /// Reads a value the validators have already guaranteed to be present.
    /// </summary>
    /// <remarks>
    /// The alternative is the null-forgiving operator, which silently produces
    /// a NullReferenceException somewhere else if that guarantee ever stops
    /// holding. This fails at the spot, naming the field.
    /// </remarks>
    public static T Required<T>(
        T? value,
        [CallerArgumentExpression(nameof(value))] string? expression = null)
        where T : class =>
        value ?? throw new InvalidOperationException(
            $"'{expression}' passed validation but is null. The validator and this " +
            "mapping are out of sync.");
}
