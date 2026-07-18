using Dapper;
using Npgsql;
using EcoMyceliumTracker.Models;

namespace EcoMyceliumTracker.Repositories;

public class SensorRepository : ISensorRepository
{
    private readonly string _connectionString;

    public SensorRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("PostgresConnection")!;
    }

    private NpgsqlConnection GetConnection() => new NpgsqlConnection(_connectionString);

    public async Task<IEnumerable<SensorNode>> GetByNetworkIdAsync(Guid networkId)
    {
        using var connection = GetConnection();
        var sql = @"SELECT id, 
                           network_id as NetworkId, 
                           location::text as Location, 
                           moisture_level as MoistureLevel, 
                           is_active as IsActive 
                    FROM sensor_nodes 
                    WHERE network_id = $1";
                    
        return await connection.QueryAsync<SensorNode>(sql, new { networkId });
    }

    public async Task<Guid> CreateAsync(SensorNode sensor)
    {
        using var connection = GetConnection();
        var sql = @"INSERT INTO sensor_nodes (id, network_id, location, moisture_level, is_active) 
                    VALUES ($1, $2, point($3, $4), $5, $6) 
                    RETURNING id;";

        sensor.Id = Guid.NewGuid();

        var coordenadas = sensor.Location.Split(',');
        double x = coordenadas.Length > 0 && double.TryParse(coordenadas[0], out var resX) ? resX : 0;
        double y = coordenadas.Length > 1 && double.TryParse(coordenadas[1], out var resY) ? resY : 0;

        await connection.ExecuteAsync(sql, new 
        { 
            sensor.Id, 
            sensor.NetworkId, 
            x, 
            y, 
            sensor.MoistureLevel, 
            sensor.IsActive 
        });

        return sensor.Id;
    }
}