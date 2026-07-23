using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EcoMyceliumTracker.Domain;

// A point is stored as PostgreSQL text and arrives from callers as "x,y", so
// converting between the two is domain vocabulary rather than validation:
// the repository needs it to write, and the validator only borrows it to
// decide whether an input is well formed.

/// <summary>
/// A sensor position. Carrying the pair as a type keeps the "is a point"
/// check at the edge, where the text is parsed once, instead of leaving a
/// string to be re-parsed by whoever needs the numbers.
/// </summary>
/// <remarks>
/// Serialised as the same "(x,y)" text PostgreSQL prints and callers already
/// send, so the wire format is unchanged by the type.
/// </remarks>
[JsonConverter(typeof(CoordinatesJsonConverter))]
public readonly record struct Coordinates(double X, double Y)
{
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"({X},{Y})");
}

public sealed class CoordinatesJsonConverter : JsonConverter<Coordinates>
{
    public override Coordinates Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        CoordinatesParser.TryParse(reader.GetString(), out var coordinates)
            ? coordinates
            : throw new JsonException("Expected a point in the format 'x,y'.");

    public override void Write(
        Utf8JsonWriter writer,
        Coordinates value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.ToString());
    }
}

public static class CoordinatesParser
{
    public static bool TryParse(string? value, out Coordinates coordinates)
    {
        coordinates = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (normalized.Length >= 2 && normalized[0] == '(' && normalized[^1] == ')')
        {
            normalized = normalized[1..^1];
        }

        var parts = normalized.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 2
            || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
            || !double.IsFinite(x)
            || !double.IsFinite(y))
        {
            return false;
        }

        coordinates = new Coordinates(x, y);
        return true;
    }
}
