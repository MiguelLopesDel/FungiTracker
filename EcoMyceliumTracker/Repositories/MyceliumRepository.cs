using Dapper;
using Npgsql;
using EcoMyceliumTracker.Models;

namespace EcoMyceliumTracker.Repositories;

public class MyceliumRepository : IMyceliumRepository
{
    private readonly string _connectionString;

    public MyceliumRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("PostgresConnection")!;
    }

    private NpgsqlConnection GetConnection() => new NpgsqlConnection(_connectionString);

    public async Task<IEnumerable<MyceliumNetwork>> GetAllAsync()
    {
        using var connection = GetConnection();
        
        var sql = @"SELECT id, 
                           scientific_name as ScientificName, 
                           soil_type as SoilType, 
                           discovered_at as DiscoveredAt,
                           created_at as CreatedAt 
                    FROM mycelium_networks";
                    
        return await connection.QueryAsync<MyceliumNetwork>(sql);
    }

    public async Task<Guid> CreateAsync(MyceliumNetwork network)
    {
        using var connection = GetConnection();
        
        var sql = @"INSERT INTO mycelium_networks (id, scientific_name, soil_type, discovered_at) 
                    VALUES ($1, $2, $3, $4) 
                    RETURNING id;";

        network.Id = Guid.NewGuid();
        
        await connection.ExecuteAsync(sql, new 
        { 
            network.Id, 
            network.ScientificName, 
            network.SoilType, 
            network.DiscoveredAt 
        });

        return network.Id;
    }
}