using System.Diagnostics.CodeAnalysis;
using Dapper;
using EcoMyceliumTracker.Application;
using EcoMyceliumTracker.Contracts;
using EcoMyceliumTracker.Models;
using Npgsql;

namespace EcoMyceliumTracker.Repositories;

public sealed class TransferRepository(NpgsqlDataSource dataSource) : ITransferRepository
{
    public async Task<PagedResult<TransferSummary>> GetPageAsync(
        int page,
        int pageSize,
        TransferFilter filter,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
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
            SELECT t.id AS Id,
                   t.source_node_id AS SourceNodeId,
                   t.target_node_id AS TargetNodeId,
                   t.carbon_amount_mg AS CarbonAmountMg,
                   t.transferred_at AS TransferredAt,
                   source.location::text AS SourceLocation,
                   target.location::text AS TargetLocation
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

        using var grid = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        var items = (await grid.ReadAsync<TransferSummary>()).AsList();
        var total = await grid.ReadSingleAsync<long>();

        return new PagedResult<TransferSummary>(items, page, pageSize, total);
    }

    public async Task<NutrientTransfer?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        const string sql = """
            SELECT id,
                   source_node_id AS SourceNodeId,
                   target_node_id AS TargetNodeId,
                   carbon_amount_mg AS CarbonAmountMg,
                   transferred_at AS TransferredAt
            FROM nutrient_transfers
            WHERE id = @Id;
            """;

        return await connection.QuerySingleOrDefaultAsync<NutrientTransfer>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<NutrientTransfer> CreateAsync(
        NutrientTransfer transfer,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string sensorSql = """
            SELECT id AS Id, network_id AS NetworkId, is_active AS IsActive
            FROM sensor_nodes
            WHERE id = ANY(@Ids)
            ORDER BY id
            FOR SHARE;
            """;
        var states = (await connection.QueryAsync<TransferSensorState>(
            new CommandDefinition(
                sensorSql,
                new { Ids = new[] { transfer.SourceNodeId, transfer.TargetNodeId } },
                transaction,
                cancellationToken: cancellationToken))).AsList();

        var source = states.SingleOrDefault(sensor => sensor.Id == transfer.SourceNodeId);
        var target = states.SingleOrDefault(sensor => sensor.Id == transfer.TargetNodeId);

        if (source is null || target is null)
        {
            throw DomainException.NotFound(
                "O sensor de origem ou de destino não existe.",
                "transfer_sensor_not_found");
        }

        if (!source.IsActive || !target.IsActive)
        {
            throw DomainException.Validation(
                "Transferências só podem envolver sensores ativos.",
                "inactive_transfer_sensor");
        }

        if (source.NetworkId != target.NetworkId)
        {
            throw DomainException.Validation(
                "Os sensores de origem e destino devem pertencer à mesma rede.",
                "sensors_from_different_networks");
        }

        const string insertSql = """
            INSERT INTO nutrient_transfers
                (source_node_id, target_node_id, carbon_amount_mg, transferred_at)
            VALUES (@SourceNodeId, @TargetNodeId, @CarbonAmountMg, @TransferredAt)
            RETURNING id,
                      source_node_id AS SourceNodeId,
                      target_node_id AS TargetNodeId,
                      carbon_amount_mg AS CarbonAmountMg,
                      transferred_at AS TransferredAt;
            """;
        var created = await connection.QuerySingleAsync<NutrientTransfer>(
            new CommandDefinition(insertSql, transfer, transaction, cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
        return created;
    }

    // Dapper builds this type by reflection, so no analyzer can see it being
    // instantiated or its properties being written.
    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "Dapper materializes this type by reflection.")]
    [SuppressMessage(
        "Major Code Smell",
        "S3459:Unassigned members should be removed",
        Justification = "Dapper assigns these when materializing the row.")]
    [SuppressMessage(
        "Major Code Smell",
        "S1144:Unused private types or members should be removed",
        Justification = "Dapper needs the setters to materialize the row.")]
    private sealed class TransferSensorState
    {
        public Guid Id { get; init; }
        public Guid NetworkId { get; init; }
        public bool IsActive { get; init; }
    }
}
