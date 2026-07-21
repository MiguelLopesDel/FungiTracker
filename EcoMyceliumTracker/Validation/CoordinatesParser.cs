using System.Globalization;

namespace EcoMyceliumTracker.Validation;

public readonly record struct Coordinates(double X, double Y);

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
