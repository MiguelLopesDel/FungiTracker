using Dapper;
using DotNetEnv;
using EcoMyceliumTracker.Models;
using Npgsql;
Env.Load();

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("PostgresConnection");

var app = builder.Build();

NpgsqlConnection GetConnection() => new NpgsqlConnection(connectionString);

app.MapGet("/api/networks", async () =>
{
    using var connection = GetConnection();
    var sql = "SELECT id, scientific_name as ScientificName, soil_type as SoilType, discovered_at as DiscoveredAt FROM mycelium_networks";
    var networks = await connection.QueryAsync<MyceliumNetwork>(sql);
    return Results.Ok(networks);
});

app.MapPost("/api/networks", async (MyceliumNetwork network) =>
{
    using var connection = GetConnection();
    var sql = @"
        INSERT INTO mycelium_networks (id, scientific_name, soil_type, discovered_at) 
        VALUES ($1, $2, $3, $4) 
        RETURNING id;";
    
    network.Id = Guid.NewGuid();
    
    await connection.ExecuteAsync(sql, new { network.Id, network.ScientificName, network.SoilType, network.DiscoveredAt });
    return Results.Created($"/api/networks/{network.Id}", network);
});
app.MapGet("/api/transfers/high-energy", async () =>
{
    using var connection = GetConnection();
    var sql = @"
        SELECT 
            t.id, 
            t.carbon_amount_mg as CarbonAmountMg, 
            t.transferred_at as TransferredAt,
            origem.location as SourceLocation,
            destino.location as TargetLocation
        FROM nutrient_transfers t
        JOIN sensor_nodes origem ON t.source_node_id = origem.id
        JOIN sensor_nodes destino ON t.target_node_id = destino.id
        WHERE t.carbon_amount_mg > 500;";

    var result = await connection.QueryAsync(sql);
    return Results.Ok(result);
});

app.Run();