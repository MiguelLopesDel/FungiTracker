using Dapper;
using EcoMyceliumTracker.Models;
using Npgsql;

namespace EcoMyceliumTracker.Repositories;

public sealed class MyceliumRepository(NpgsqlDataSource dataSource) : IMyceliumRepository
{
    private const string SelectColumns = """
        id,
        scientific_name AS ScientificName,
        soil_type AS SoilType,
        discovered_at AS DiscoveredAt,
        created_at AS CreatedAt
        """;

    public async Task<PagedResult<MyceliumNetwork>> GetPageAsync(
        int page,
        int pageSize,
        string? scientificName,
        string? soilType,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var parameters = new
        {
            ScientificName = NullIfWhiteSpace(scientificName),
            SoilType = NullIfWhiteSpace(soilType),
            PageSize = pageSize,
            // Widened before multiplying: page is only bounded from below, so
            // int arithmetic here overflows into a negative OFFSET.
            Offset = (long)(page - 1) * pageSize
        };

        var sql = $$"""
            SELECT {{SelectColumns}}
            FROM mycelium_networks
            WHERE (CAST(@ScientificName AS text) IS NULL OR scientific_name ILIKE '%' || @ScientificName || '%' ESCAPE '\')
              AND (CAST(@SoilType AS text) IS NULL OR soil_type ILIKE '%' || @SoilType || '%' ESCAPE '\')
            ORDER BY created_at DESC, id
            LIMIT @PageSize OFFSET @Offset;

            SELECT COUNT(*)
            FROM mycelium_networks
            WHERE (CAST(@ScientificName AS text) IS NULL OR scientific_name ILIKE '%' || @ScientificName || '%' ESCAPE '\')
              AND (CAST(@SoilType AS text) IS NULL OR soil_type ILIKE '%' || @SoilType || '%' ESCAPE '\');
            """;

        await using var grid = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        var items = (await grid.ReadAsync<MyceliumNetwork>()).AsList();
        var total = await grid.ReadSingleAsync<long>();

        return new PagedResult<MyceliumNetwork>(items, page, pageSize, total);
    }

    public async Task<MyceliumNetwork?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var sql = $$"""
            SELECT {{SelectColumns}}
            FROM mycelium_networks
            WHERE id = @Id;
            """;

        return await connection.QuerySingleOrDefaultAsync<MyceliumNetwork>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<MyceliumNetwork> CreateAsync(
        MyceliumNetwork network,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var sql = $$"""
            INSERT INTO mycelium_networks (id, scientific_name, soil_type, discovered_at)
            VALUES (@Id, @ScientificName, @SoilType, @DiscoveredAt)
            RETURNING {{SelectColumns}};
            """;

        return await connection.QuerySingleAsync<MyceliumNetwork>(
            new CommandDefinition(sql, network, cancellationToken: cancellationToken));
    }

    public async Task<MyceliumNetwork?> UpdateAsync(
        Guid id,
        MyceliumNetwork network,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var sql = $$"""
            UPDATE mycelium_networks
            SET scientific_name = @ScientificName,
                soil_type = @SoilType,
                discovered_at = @DiscoveredAt
            WHERE id = @Id
            RETURNING {{SelectColumns}};
            """;

        return await connection.QuerySingleOrDefaultAsync<MyceliumNetwork>(
            new CommandDefinition(
                sql,
                new { Id = id, network.ScientificName, network.SoilType, network.DiscoveredAt },
                cancellationToken: cancellationToken));
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        const string sql = "DELETE FROM mycelium_networks WHERE id = @Id;";
        var affectedRows = await connection.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return affectedRows > 0;
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : EscapeLikePattern(value.Trim());

    // '%' and '_' are wildcards inside ILIKE, so a search for a name that
    // contains them has to escape them to stay a literal search. The escape
    // character itself is doubled first, otherwise it would escape the escape.
    private static string EscapeLikePattern(string value) => value
        .Replace(@"\", @"\\", StringComparison.Ordinal)
        .Replace("%", @"\%", StringComparison.Ordinal)
        .Replace("_", @"\_", StringComparison.Ordinal);
}
