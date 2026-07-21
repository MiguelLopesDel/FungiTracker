using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using EcoMyceliumTracker.Contracts;
using EcoMyceliumTracker.Models;
using Npgsql;

namespace EcoMyceliumTracker.IntegrationTests;

/// <summary>
/// Pins the row lock the transfer creation depends on.
/// </summary>
/// <remarks>
/// Creating a transfer reads both sensors FOR SHARE and only then inserts, so
/// that neither can be deactivated or moved to another network in between.
/// Measured on PostgreSQL, FOR SHARE is the weakest mode that does this:
/// FOR KEY SHARE lets a deactivation through, while FOR NO KEY UPDATE and
/// FOR UPDATE also block a second concurrent transfer, which never needed to
/// be serialised. Weakening or strengthening the mode breaks one of the two
/// assertions below.
/// </remarks>
public sealed class TransferLockTests
{
    private const string ApiKey = ApiFactory.ApiKey;
    private const int LockWaitMilliseconds = 1200;
    private const string StatementTimeoutSqlState = "57014";

    [PostgresFact]
    public async Task ForShare_BlocksSensorChanges_ButNotAnotherTransfer()
    {
        var connectionString = PostgresFactAttribute.RequiredConnectionString();
        var sensorId = await CreateSensorAsync(connectionString);

        await using var holder = new NpgsqlConnection(connectionString);
        await holder.OpenAsync();
        await using var holdingTransaction = await holder.BeginTransactionAsync(
            );

        await using (var lockCommand = new NpgsqlCommand(
            "SELECT id FROM sensor_nodes WHERE id = @id FOR SHARE",
            holder,
            holdingTransaction))
        {
            lockCommand.Parameters.AddWithValue("id", sensorId);
            await lockCommand.ExecuteScalarAsync();
        }

        Assert.True(
            await IsBlockedAsync(connectionString, "UPDATE sensor_nodes SET is_active = false WHERE id = @id", sensorId),
            "Deactivating a locked sensor must wait for the transfer to commit.");

        Assert.True(
            await IsBlockedAsync(connectionString, "UPDATE sensor_nodes SET network_id = network_id WHERE id = @id", sensorId),
            "Moving a locked sensor to another network must wait for the transfer to commit.");

        Assert.False(
            await IsBlockedAsync(connectionString, "SELECT id FROM sensor_nodes WHERE id = @id FOR SHARE", sensorId),
            "A second concurrent transfer must not be serialised against the first.");

        await holdingTransaction.RollbackAsync();

        await AssertKeyShareIsTooWeakAsync(connectionString, sensorId);
    }

    /// <summary>
    /// The mode below FOR SHARE lets exactly the change through that the lock
    /// exists to prevent, which is why the weaker one cannot be used.
    /// </summary>
    private static async Task AssertKeyShareIsTooWeakAsync(string connectionString, Guid sensorId)
    {
        await using var holder = new NpgsqlConnection(connectionString);
        await holder.OpenAsync();
        await using var holdingTransaction = await holder.BeginTransactionAsync();

        await using (var lockCommand = new NpgsqlCommand(
            "SELECT id FROM sensor_nodes WHERE id = @id FOR KEY SHARE",
            holder,
            holdingTransaction))
        {
            lockCommand.Parameters.AddWithValue("id", sensorId);
            await lockCommand.ExecuteScalarAsync();
        }

        Assert.False(
            await IsBlockedAsync(connectionString, "UPDATE sensor_nodes SET is_active = false WHERE id = @id", sensorId),
            "FOR KEY SHARE does not block deactivation, so it cannot replace FOR SHARE.");

        await holdingTransaction.RollbackAsync();
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The statements are constants declared in this test.")]
    private static async Task<bool> IsBlockedAsync(string connectionString, string sql, Guid sensorId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using (var timeout = new NpgsqlCommand(
            $"SET statement_timeout = {LockWaitMilliseconds}",
            connection))
        {
            await timeout.ExecuteNonQueryAsync();
        }

        try
        {
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("id", sensorId);
            await command.ExecuteNonQueryAsync();
            return false;
        }
        catch (PostgresException exception) when (exception.SqlState == StatementTimeoutSqlState)
        {
            return true;
        }
    }

    private static async Task<Guid> CreateSensorAsync(string connectionString)
    {
        await using var factory = new ApiFactory(connectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", ApiKey);

        var networkResponse = await client.PostAsJsonAsync(
            "/api/networks",
            new CreateMyceliumNetworkRequest($"Lock-{Guid.NewGuid():N}", "Solo", DateTimeOffset.UtcNow.AddDays(-1)));
        var network = await networkResponse.Content.ReadFromJsonAsync<MyceliumNetwork>(
            );
        Assert.NotNull(network);

        var sensorResponse = await client.PostAsJsonAsync(
            "/api/sensors",
            new CreateSensorNodeRequest(network.Id, "1,1", 50, true));
        var sensor = await sensorResponse.Content.ReadFromJsonAsync<SensorNode>(
            );
        Assert.NotNull(sensor);

        return sensor.Id;
    }
}
