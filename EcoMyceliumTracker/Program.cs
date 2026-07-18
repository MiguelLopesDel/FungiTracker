using EcoMyceliumTracker.Models;
using EcoMyceliumTracker.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<IMyceliumRepository, MyceliumRepository>();
builder.Services.AddScoped<ISensorRepository, SensorRepository>();
builder.Services.AddScoped<ITransferRepository, TransferRepository>();

var app = builder.Build();

app.MapGet("/api/networks", async (IMyceliumRepository repo) => 
    Results.Ok(await repo.GetAllAsync()));

app.MapPost("/api/networks", async (MyceliumNetwork network, IMyceliumRepository repo) =>
{
    if (string.IsNullOrWhiteSpace(network.ScientificName)) return Results.BadRequest("Nome obrigatório.");
    var id = await repo.CreateAsync(network);
    return Results.Created($"/api/networks/{id}", network);
});


app.MapGet("/api/networks/{networkId:guid}/sensors", async (Guid networkId, ISensorRepository repo) => 
    Results.Ok(await repo.GetByNetworkIdAsync(networkId)));

app.MapPost("/api/sensors", async (SensorNode sensor, ISensorRepository repo) =>
{
    if (string.IsNullOrWhiteSpace(sensor.Location)) return Results.BadRequest("Coordenadas '(x,y)' obrigatórias.");
    var id = await repo.CreateAsync(sensor);
    return Results.Created($"/api/sensors/{id}", sensor);
});

app.MapGet("/api/transfers/high-energy", async (ITransferRepository repo) => 
    Results.Ok(await repo.GetHighEnergyTransfersAsync()));

app.MapPost("/api/transfers", async (NutrientTransfers transfer, ITransferRepository repo) =>
{
    if (transfer.SourceNodeId == transfer.TargetNodeId) 
        return Results.BadRequest("O sensor de origem não pode ser igual ao de destino.");
        
    var id = await repo.CreateAsync(transfer.SourceNodeId, transfer.TargetNodeId, transfer.CarbonAmountMg);
    transfer.Id = id;
    return Results.Created($"/api/transfers/{id}", transfer);
});

app.Run();