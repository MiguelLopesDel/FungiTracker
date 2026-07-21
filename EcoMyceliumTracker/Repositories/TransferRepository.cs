using Dapper;
using EcoMyceliumTracker.Domain;
using EcoMyceliumTracker.Models;

namespace EcoMyceliumTracker.Repositories;

public sealed class TransferRepository(IDbSession session) : ITransferRepository
{
    private const string SelectColumns = """
        t.id AS Id,
        t.source_node_id AS SourceNodeId,
        t.target_node_id AS TargetNodeId,
        t.carbon_amount_mg AS CarbonAmountMg,
        t.transferred_at AS TransferredAt,
        source.location::text AS SourceLocation,
        target.location::text AS TargetLocation
        """;

    public async Task<PagedResult<NutrientTransferDetails>> GetPageAsync(
        int page,
        int pageSize,
        TransferFilter filter,
        CancellationToken cancellationToken = default)
    {
        await using var lease = await session.LeaseAsync(cancellationToken);
        var parameters = new
        {
            filter.MinimumCarbonMg,
            filter.SourceNodeId,
            filter.TargetNodeId,
            filter.From,
            filter.To,
            PageSize = pageSize,
            // Widened before multiplying: page is only bounded from below, so
            // int arithmetic here overflows into a negative OFFSET.
            Offset = (long)(page - 1) * pageSize
        };
        const string filters = """
            (CAST(@MinimumCarbonMg AS integer) IS NULL OR t.carbon_amount_mg >= @MinimumCarbonMg)
            AND (CAST(@SourceNodeId AS uuid) IS NULL OR t.source_node_id = @SourceNodeId)
            AND (CAST(@TargetNodeId AS uuid) IS NULL OR t.target_node_id = @TargetNodeId)
            AND (CAST(@From AS timestamptz) IS NULL OR t.transferred_at >= @From)
            AND (CAST(@To AS timestamptz) IS NULL OR t.transferred_at <= @To)
            """;
        var sql = $$"""
            SELECT {{SelectColumns}}
            FROM nutrient_transfers t
            JOIN sensor_nodes source ON t.source_node_id = source.id
            JOIN sensor_nodes target ON t.target_node_id = target.id
            WHERE {{filters}}
            ORDER BY t.transferred_at DESC, t.id DESC
            LIMIT @PageSize OFFSET @Offset;

            SELECT COUNT(*)
            FROM nutrient_transfers t
            WHERE {{filters}};
            """;

        await using var grid = await lease.Connection.QueryMultipleAsync(
            new CommandDefinition(sql, parameters, transaction: lease.Transaction, cancellationToken: cancellationToken));
        var items = (await grid.ReadAsync<NutrientTransferDetails>()).AsList();
        var total = await grid.ReadSingleAsync<long>();

        return new PagedResult<NutrientTransferDetails>(items, page, pageSize, total);
    }

    public async Task<NutrientTransferDetails?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        await using var lease = await session.LeaseAsync(cancellationToken);
        const string sql = $"""
            SELECT {SelectColumns}
            FROM nutrient_transfers t
            JOIN sensor_nodes source ON t.source_node_id = source.id
            JOIN sensor_nodes target ON t.target_node_id = target.id
            WHERE t.id = @Id;
            """;

        return await lease.Connection.QuerySingleOrDefaultAsync<NutrientTransferDetails>(
            new CommandDefinition(sql, new { Id = id }, transaction: lease.Transaction, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<TransferSensor>> GetForTransferAsync(
        Guid sourceNodeId,
        Guid targetNodeId,
        CancellationToken cancellationToken = default)
    {
        await using var lease = await session.LeaseAsync(cancellationToken);

        // FOR SHARE is the weakest mode that stops either sensor from being
        // deactivated, moved or deleted before the insert. See TransferLockTests.
        const string sql = """
            SELECT id AS Id,
                   network_id AS NetworkId,
                   is_active AS IsActive,
                   location::text AS Location
            FROM sensor_nodes
            WHERE id = ANY(@Ids)
            ORDER BY id
            FOR SHARE;
            """;

        var sensors = await lease.Connection.QueryAsync<TransferSensor>(
            new CommandDefinition(
                sql,
                new { Ids = new[] { sourceNodeId, targetNodeId } },
                transaction: lease.Transaction,
                cancellationToken: cancellationToken));

        return sensors.AsList();
    }

    public async Task<NutrientTransfer> AddAsync(
        NutrientTransfer transfer,
        CancellationToken cancellationToken = default)
    {
        await using var lease = await session.LeaseAsync(cancellationToken);

        const string sql = """
            INSERT INTO nutrient_transfers
                (source_node_id, target_node_id, carbon_amount_mg, transferred_at)
            VALUES (@SourceNodeId, @TargetNodeId, @CarbonAmountMg, @TransferredAt)
            RETURNING id,
                      source_node_id AS SourceNodeId,
                      target_node_id AS TargetNodeId,
                      carbon_amount_mg AS CarbonAmountMg,
                      transferred_at AS TransferredAt;
            """;

        return await lease.Connection.QuerySingleAsync<NutrientTransfer>(
            new CommandDefinition(
                sql,
                transfer,
                transaction: lease.Transaction,
                cancellationToken: cancellationToken));
    }
}
