using System.Data;
using Dapper;
using EcoMyceliumTracker.Domain;

namespace EcoMyceliumTracker.Infrastructure.Persistence;

/// <summary>
/// Maps the text form of a PostgreSQL point to Coordinates, so the queries
/// keep selecting location::text and nothing downstream parses it again.
/// </summary>
public sealed class CoordinatesTypeHandler : SqlMapper.TypeHandler<Coordinates>
{
    public override Coordinates Parse(object value) =>
        CoordinatesParser.TryParse(value?.ToString(), out var coordinates)
            ? coordinates
            : throw new InvalidOperationException($"Stored location '{value}' is not a valid point.");

    public override void SetValue(IDbDataParameter parameter, Coordinates value)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        parameter.Value = value.ToString();
    }
}
