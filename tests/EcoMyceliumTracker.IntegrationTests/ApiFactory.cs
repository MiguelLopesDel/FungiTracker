using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace EcoMyceliumTracker.IntegrationTests;

/// <summary>
/// Boots the application against a real database, with the API key and CORS
/// origin the tests expect.
/// </summary>
internal sealed class ApiFactory(
    string connectionString,
    int permitLimit = 1000) : WebApplicationFactory<Program>
{
    internal const string ApiKey = "integration-test-api-key";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgresConnection"] = connectionString,
                ["Authentication:ApiKey"] = ApiKey,
                ["Cors:AllowedOrigins:0"] = "http://localhost:3000",
                ["Database:RunMigrations"] = "true",
                ["RateLimit:PermitLimit"] = permitLimit.ToString(CultureInfo.InvariantCulture)
            });
        });
    }
}
