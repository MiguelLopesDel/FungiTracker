using System.Collections.ObjectModel;
using EcoMyceliumTracker.Application;
using EcoMyceliumTracker.Models;
using EcoMyceliumTracker.Repositories;

namespace EcoMyceliumTracker.UnitTests.Fakes;

/// <summary>
/// Enough of the persistence contract to drive the services in memory. The
/// point is that the rules above the repository no longer need PostgreSQL to
/// be exercised.
/// </summary>
public sealed class FakeNetworkRepository : IMyceliumRepository
{
    public Dictionary<Guid, MyceliumNetwork> Items { get; } = [];

    public Task<PagedResult<MyceliumNetwork>> GetPageAsync(
        int page,
        int pageSize,
        string? scientificName,
        string? soilType,
        CancellationToken cancellationToken = default)
    {
        var matches = Items.Values
            .Where(item => scientificName is null
                || item.ScientificName.Contains(scientificName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return Task.FromResult(new PagedResult<MyceliumNetwork>(
            matches.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            page,
            pageSize,
            matches.Count));
    }

    public Task<MyceliumNetwork?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.GetValueOrDefault(id));

    public Task<MyceliumNetwork> CreateAsync(
        MyceliumNetwork network,
        CancellationToken cancellationToken = default)
    {
        network.Id = Guid.NewGuid();
        Items[network.Id] = network;
        return Task.FromResult(network);
    }

    public Task<MyceliumNetwork?> UpdateAsync(
        Guid id,
        MyceliumNetwork network,
        CancellationToken cancellationToken = default)
    {
        if (!Items.ContainsKey(id))
        {
            return Task.FromResult<MyceliumNetwork?>(null);
        }

        network.Id = id;
        Items[id] = network;
        return Task.FromResult<MyceliumNetwork?>(network);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Remove(id));
}

public sealed class FakeSensorRepository : ISensorRepository
{
    public Dictionary<Guid, SensorNode> Items { get; } = [];

    public Task<PagedResult<SensorNode>> GetPageByNetworkIdAsync(
        Guid networkId,
        int page,
        int pageSize,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var matches = Items.Values
            .Where(item => item.NetworkId == networkId)
            .Where(item => isActive is null || item.IsActive == isActive)
            .ToList();

        return Task.FromResult(new PagedResult<SensorNode>(matches, page, pageSize, matches.Count));
    }

    public Task<SensorNode?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.GetValueOrDefault(id));

    public Task<SensorNode> CreateAsync(SensorNode sensor, CancellationToken cancellationToken = default)
    {
        sensor.Id = Guid.NewGuid();
        Items[sensor.Id] = sensor;
        return Task.FromResult(sensor);
    }

    public Task<SensorNode?> UpdateAsync(
        Guid id,
        SensorNode sensor,
        CancellationToken cancellationToken = default)
    {
        if (!Items.ContainsKey(id))
        {
            return Task.FromResult<SensorNode?>(null);
        }

        sensor.Id = id;
        Items[id] = sensor;
        return Task.FromResult<SensorNode?>(sensor);
    }

    public Task<SensorNode?> SetActiveAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        if (!Items.TryGetValue(id, out var sensor))
        {
            return Task.FromResult<SensorNode?>(null);
        }

        sensor.IsActive = isActive;
        return Task.FromResult<SensorNode?>(sensor);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Remove(id));
}

public sealed class FakeTransferRepository : ITransferRepository
{
    public Collection<NutrientTransfer> Items { get; } = [];

    public int? LastMinimumCarbonMg { get; private set; }

    public Task<PagedResult<TransferView>> GetPageAsync(
        int page,
        int pageSize,
        TransferFilter filter,
        CancellationToken cancellationToken = default)
    {
        LastMinimumCarbonMg = filter.MinimumCarbonMg;

        var matches = Items
            .Where(item => filter.MinimumCarbonMg is null || item.CarbonAmountMg >= filter.MinimumCarbonMg)
            .Select(ToView)
            .ToList();

        return Task.FromResult(new PagedResult<TransferView>(matches, page, pageSize, matches.Count));
    }

    public Task<TransferView?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(item => item.Id == id) is { } found ? ToView(found) : null);

    public Task<TransferView> CreateAsync(
        NutrientTransfer transfer,
        CancellationToken cancellationToken = default)
    {
        transfer.Id = Items.Count + 1;
        Items.Add(transfer);
        return Task.FromResult(ToView(transfer));
    }

    private static TransferView ToView(NutrientTransfer item) => new()
    {
        Id = item.Id,
        SourceNodeId = item.SourceNodeId,
        TargetNodeId = item.TargetNodeId,
        CarbonAmountMg = item.CarbonAmountMg,
        TransferredAt = item.TransferredAt,
    };
}
