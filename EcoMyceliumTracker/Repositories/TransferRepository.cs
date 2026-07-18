using Dapper;
using Npgsql;

namespace EcoMyceliumTracker.Repositories;

public class TransferRepository : ITransferRepository
{
    private readonly string _connectionString;

    public TransferRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("PostgresConnection")!;
    }

    private NpgsqlConnection GetConnection() => new NpgsqlConnection(_connectionString);

    public async Task<IEnumerable<object>> GetHighEnergyTransfersAsync()
    {
        using var connection = GetConnection();
        var sql = @"
            SELECT 
                t.id as Id, 
                t.carbon_amount_mg as CarbonAmountMg, 
                t.transferred_at as TransferredAt,
                origem.location::text as SourceLocation,
                destino.location::text as TargetLocation
            FROM nutrient_transfers t
            JOIN sensor_nodes origem ON t.source_node_id = origem.id
            JOIN sensor_nodes destino ON t.target_node_id = destino.id
            WHERE t.carbon_amount_mg > 500;";

        return await connection.QueryAsync<object>(sql);
    }

    public async Task<long> CreateAsync(Guid sourceId, Guid targetId, int carbonAmount)
    {
        using var connection = GetConnection();
        var sql = @"INSERT INTO nutrient_transfers (source_node_id, target_node_id, carbon_amount_mg) 
                    VALUES ($1, $2, $3) 
                    RETURNING id;";

        return await connection.ExecuteScalarAsync<long>(sql, new { sourceId, targetId, carbonAmount });
    }
}