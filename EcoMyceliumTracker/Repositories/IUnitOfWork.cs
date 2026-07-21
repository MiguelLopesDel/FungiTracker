using Npgsql;

namespace EcoMyceliumTracker.Repositories;

/// <summary>
/// Lets an application service span several repository calls with one
/// transaction, without ever seeing a connection or a transaction object.
/// </summary>
public interface IUnitOfWork
{
    Task<IUnitOfWorkTransaction> BeginAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Rolls back on dispose unless it was committed first.
/// </summary>
public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// How a repository obtains a connection. Inside a unit of work every lease
/// returns the same connection and transaction; outside one, each lease opens
/// and closes its own, exactly as before.
/// </summary>
public interface IDbSession
{
    ValueTask<DbLease> LeaseAsync(CancellationToken cancellationToken = default);
}

public sealed class DbLease(
    NpgsqlConnection connection,
    NpgsqlTransaction? transaction,
    bool ownsConnection) : IAsyncDisposable
{
    public NpgsqlConnection Connection { get; } = connection;

    public NpgsqlTransaction? Transaction { get; } = transaction;

    public ValueTask DisposeAsync() =>
        ownsConnection ? Connection.DisposeAsync() : ValueTask.CompletedTask;
}
