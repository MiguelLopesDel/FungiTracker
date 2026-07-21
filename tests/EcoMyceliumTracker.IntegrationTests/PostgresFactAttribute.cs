namespace EcoMyceliumTracker.IntegrationTests;

/// <summary>
/// Marks a test that needs a real PostgreSQL instance.
/// </summary>
/// <remarks>
/// Skipping keeps `dotnet test` usable on a machine without a database, but a
/// skip is invisible in a green pipeline: a renamed secret or a typo in the
/// variable name would silently stop the whole integration suite from running
/// and CI would still pass. So on CI the missing variable is a failure, not a
/// skip. GitHub Actions and most other providers set CI=true.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class PostgresFactAttribute : FactAttribute
{
    internal const string ConnectionVariable = "TEST_POSTGRES_CONNECTION";

    public PostgresFactAttribute()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable)))
        {
            return;
        }

        if (IsContinuousIntegration())
        {
            // Not Skip: the test must fail loudly here.
            return;
        }

        Skip = $"Set {ConnectionVariable} to run PostgreSQL integration tests.";
    }

    internal static string RequiredConnectionString() =>
        Environment.GetEnvironmentVariable(ConnectionVariable) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException(
                $"{ConnectionVariable} is not set. On CI this is a failure rather than a skip, " +
                "so that a renamed secret cannot silently disable the integration suite.");

    internal static bool IsContinuousIntegration() =>
        string.Equals(
            Environment.GetEnvironmentVariable("CI"),
            "true",
            StringComparison.OrdinalIgnoreCase);
}
