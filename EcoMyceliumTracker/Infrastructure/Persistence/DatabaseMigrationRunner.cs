using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Npgsql;

namespace EcoMyceliumTracker.Infrastructure.Persistence;

public sealed partial class DatabaseMigrationRunner(
    NpgsqlDataSource dataSource,
    ILogger<DatabaseMigrationRunner> logger)
{
    // Arbitrary but fixed: every instance must pick the same advisory lock id
    // so that only one of them applies migrations at startup. The value itself
    // has no meaning and only has to stay stable.
    private const long MigrationLockId = 4_243_691_742;

    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Information,
        Message = "Applying database migration {MigrationName}")]
    private static partial void LogApplyingMigration(ILogger logger, string migrationName);

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

            var sql = await ReadMigrationAsync(assembly, resourceName, cancellationToken);

            LogApplyingMigration(logger, migrationName);
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

    private static async Task<string> ReadMigrationAsync(
        Assembly assembly,
        string resourceName,
        CancellationToken cancellationToken)
    {
        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Migration resource '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);

        return await reader.ReadToEndAsync(cancellationToken);
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

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The statements come from migration files embedded in the " +
            "assembly at build time, never from user input.")]
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
