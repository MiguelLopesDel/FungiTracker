using EcoMyceliumTracker.Repositories;
using Npgsql;

namespace EcoMyceliumTracker.Infrastructure.Persistence;

/// <summary>
/// One database session per request. It is both the unit of work the services
/// begin and the source of connections the repositories lease.
/// </summary>
public sealed class DbSession(NpgsqlDataSource dataSource) : IUnitOfWork, IDbSession, IAsyncDisposable
{
    private NpgsqlConnection? ambientConnection;
    private NpgsqlTransaction? ambientTransaction;

    public async ValueTask<DbLease> LeaseAsync(CancellationToken cancellationToken = default)
    {
        if (ambientConnection is not null)
        {
            return new DbLease(ambientConnection, ambientTransaction, ownsConnection: false);
        }

        var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return new DbLease(connection, transaction: null, ownsConnection: true);
    }

    public async Task<IUnitOfWorkTransaction> BeginAsync(CancellationToken cancellationToken = default)
    {
        if (ambientTransaction is not null)
        {
            throw new InvalidOperationException("A transaction is already open on this session.");
        }

        ambientConnection = await dataSource.OpenConnectionAsync(cancellationToken);
        ambientTransaction = await ambientConnection.BeginTransactionAsync(cancellationToken);
        return new Scope(this);
    }

    public async ValueTask DisposeAsync() => await ReleaseAsync();

    private async ValueTask ReleaseAsync()
    {
        if (ambientTransaction is not null)
        {
            await ambientTransaction.DisposeAsync();
            ambientTransaction = null;
        }

        if (ambientConnection is not null)
        {
            await ambientConnection.DisposeAsync();
            ambientConnection = null;
        }
    }

    private sealed class Scope(DbSession session) : IUnitOfWorkTransaction
    {
        private bool committed;

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            if (session.ambientTransaction is null)
            {
                throw new InvalidOperationException("The transaction has already ended.");
            }

            await session.ambientTransaction.CommitAsync(cancellationToken);
            committed = true;
        }

        // Disposing without committing rolls back, which is what an exception
        // escaping the service should do.
        public async ValueTask DisposeAsync()
        {
            if (!committed && session.ambientTransaction is not null)
            {
                await session.ambientTransaction.RollbackAsync();
            }

            await session.ReleaseAsync();
        }
    }
}
