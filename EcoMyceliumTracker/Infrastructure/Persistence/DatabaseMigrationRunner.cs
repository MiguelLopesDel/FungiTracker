using System.Reflection;
using Npgsql;

namespace EcoMyceliumTracker.Infrastructure.Persistence;

public sealed class DatabaseMigrationRunner(
    NpgsqlDataSource dataSource,
    ILogger<DatabaseMigrationRunner> logger)
{
    private const long MigrationLockId = 4_243_691_742;

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await ExecuteAsync(
            connection,
            transaction,
            $"SELECT pg_advisory_xact_lock({MigrationLockId});",
            cancellationToken);

        await ExecuteAsync(
            connection,
            transaction,
            """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                name TEXT PRIMARY KEY,
                applied_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
            );
            """,
            cancellationToken);

        var appliedMigrations = await GetAppliedMigrationsAsync(
            connection,
            transaction,
            cancellationToken);

        var assembly = Assembly.GetExecutingAssembly();
        var resources = assembly.GetManifestResourceNames()
            .Where(name => name.Contains(".Migrations.", StringComparison.Ordinal)
                && name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToArray();

        foreach (var resourceName in resources)
        {
            var migrationName = resourceName[(resourceName.LastIndexOf(".Migrations.", StringComparison.Ordinal)
                + ".Migrations.".Length)..];

            if (appliedMigrations.Contains(migrationName))
            {
                continue;
            }

            await using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Migration resource '{resourceName}' was not found.");
            using var reader = new StreamReader(stream);
            var sql = await reader.ReadToEndAsync(cancellationToken);

            logger.LogInformation("Applying database migration {MigrationName}", migrationName);
            await ExecuteAsync(connection, transaction, sql, cancellationToken);

            await using var insertCommand = new NpgsqlCommand(
                "INSERT INTO schema_migrations (name) VALUES (@Name);",
                connection,
                transaction);
            insertCommand.Parameters.AddWithValue("Name", migrationName);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<HashSet<string>> GetAppliedMigrationsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT name FROM schema_migrations;",
            connection,
            transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var migrations = new HashSet<string>(StringComparer.Ordinal);

        while (await reader.ReadAsync(cancellationToken))
        {
            migrations.Add(reader.GetString(0));
        }

        return migrations;
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
