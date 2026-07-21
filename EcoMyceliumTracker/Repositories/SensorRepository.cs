using Dapper;
using EcoMyceliumTracker.Models;
using EcoMyceliumTracker.Validation;
using Npgsql;

namespace EcoMyceliumTracker.Repositories;

public sealed class SensorRepository(NpgsqlDataSource dataSource) : ISensorRepository
{
    private const string SelectColumns = """
        id,
        network_id AS NetworkId,
        location::text AS Location,
        moisture_level AS MoistureLevel,
        is_active AS IsActive
        """;

    public async Task<PagedResult<SensorNode>> GetPageByNetworkIdAsync(
        Guid networkId,
        int page,
        int pageSize,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var parameters = new
        {
            NetworkId = networkId,
            IsActive = isActive,
            PageSize = pageSize,
            // Widened before multiplying: page is only bounded from below, so
            // int arithmetic here overflows into a negative OFFSET.
            Offset = (long)(page - 1) * pageSize
        };
        var sql = $$"""
            SELECT {{SelectColumns}}
            FROM sensor_nodes
            WHERE network_id = @NetworkId
              AND (CAST(@IsActive AS boolean) IS NULL OR is_active = @IsActive)
            ORDER BY id
            LIMIT @PageSize OFFSET @Offset;

            SELECT COUNT(*)
            FROM sensor_nodes
            WHERE network_id = @NetworkId
              AND (CAST(@IsActive AS boolean) IS NULL OR is_active = @IsActive);
            """;

        using var grid = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        var items = (await grid.ReadAsync<SensorNode>()).AsList();
        var total = await grid.ReadSingleAsync<long>();

        return new PagedResult<SensorNode>(items, page, pageSize, total);
    }

    public async Task<SensorNode?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var sql = $$"""
            SELECT {{SelectColumns}}
            FROM sensor_nodes
            WHERE id = @Id;
            """;

        return await connection.QuerySingleOrDefaultAsync<SensorNode>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<SensorNode> CreateAsync(
        SensorNode sensor,
        CancellationToken cancellationToken = default)
    {
        if (!CoordinatesParser.TryParse(sensor.Location, out var coordinates))
        {
            throw new ArgumentException("A localização deve usar o formato 'x,y'.", nameof(sensor));
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        sensor.Id = Guid.NewGuid();
        var sql = $$"""
            INSERT INTO sensor_nodes (id, network_id, location, moisture_level, is_active)
            VALUES (@Id, @NetworkId, point(@X, @Y), @MoistureLevel, @IsActive)
            RETURNING {{SelectColumns}};
            """;

        return await connection.QuerySingleAsync<SensorNode>(
            new CommandDefinition(
                sql,
                new
                {
                    sensor.Id,
                    sensor.NetworkId,
                    coordinates.X,
                    coordinates.Y,
                    sensor.MoistureLevel,
                    sensor.IsActive
                },
                cancellationToken: cancellationToken));
    }

    public async Task<SensorNode?> UpdateAsync(
        Guid id,
        SensorNode sensor,
        CancellationToken cancellationToken = default)
    {
        if (!CoordinatesParser.TryParse(sensor.Location, out var coordinates))
        {
            throw new ArgumentException("A localização deve usar o formato 'x,y'.", nameof(sensor));
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var sql = $$"""
            UPDATE sensor_nodes
            SET location = point(@X, @Y),
                moisture_level = @MoistureLevel,
                is_active = @IsActive
            WHERE id = @Id
            RETURNING {{SelectColumns}};
            """;

        return await connection.QuerySingleOrDefaultAsync<SensorNode>(
            new CommandDefinition(
                sql,
                new { Id = id, coordinates.X, coordinates.Y, sensor.MoistureLevel, sensor.IsActive },
                cancellationToken: cancellationToken));
    }

    public async Task<SensorNode?> SetActiveAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var sql = $$"""
            UPDATE sensor_nodes
            SET is_active = @IsActive
            WHERE id = @Id
            RETURNING {{SelectColumns}};
            """;

        return await connection.QuerySingleOrDefaultAsync<SensorNode>(
            new CommandDefinition(sql, new { Id = id, IsActive = isActive }, cancellationToken: cancellationToken));
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        const string sql = "DELETE FROM sensor_nodes WHERE id = @Id;";
        var affectedRows = await connection.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return affectedRows > 0;
    }
}
