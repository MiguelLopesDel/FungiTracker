using System.Net;
using System.Net.Http.Json;
using EcoMyceliumTracker.Contracts;
using EcoMyceliumTracker.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace EcoMyceliumTracker.IntegrationTests;

public sealed class ApiIntegrationTests
{
    private const string ApiKey = "integration-test-api-key";

    [PostgresFact]
    public async Task ApiWorkflow_EnforcesAuthenticationPaginationAndTransferRules()
    {
        var connectionString = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION")!;

        await using var factory = new ApiFactory(connectionString);
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/index.html")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/v1/swagger.json")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/networks")).StatusCode);

        using var preflightRequest = new HttpRequestMessage(HttpMethod.Options, "/api/networks");
        preflightRequest.Headers.Add("Origin", "http://localhost:3000");
        preflightRequest.Headers.Add("Access-Control-Request-Method", "GET");
        var preflightResponse = await client.SendAsync(preflightRequest);
        Assert.Equal(HttpStatusCode.NoContent, preflightResponse.StatusCode);
        Assert.Contains(
            "http://localhost:3000",
            preflightResponse.Headers.GetValues("Access-Control-Allow-Origin"));

        client.DefaultRequestHeaders.Add("X-API-Key", ApiKey);

        var networkResponse = await client.PostAsJsonAsync(
            "/api/networks",
            new CreateMyceliumNetworkRequest(
                $"Integration-{Guid.NewGuid():N}",
                "Florestal",
                DateTimeOffset.UtcNow.AddDays(-1)));
        Assert.Equal(HttpStatusCode.Created, networkResponse.StatusCode);
        var network = await networkResponse.Content.ReadFromJsonAsync<MyceliumNetwork>();
        Assert.NotNull(network);

        var updateNetworkResponse = await client.PutAsJsonAsync(
            $"/api/networks/{network.Id}",
            new UpdateMyceliumNetworkRequest(
                network.ScientificName,
                "Florestal úmido",
                network.DiscoveredAt));
        Assert.Equal(HttpStatusCode.OK, updateNetworkResponse.StatusCode);

        var source = await CreateSensorAsync(client, network.Id, "10,20");
        var target = await CreateSensorAsync(client, network.Id, "30,40");
        var transferredAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var transferRequest = new CreateNutrientTransferRequest(
            source.Id,
            target.Id,
            750,
            transferredAt);

        var transferResponse = await client.PostAsJsonAsync("/api/transfers", transferRequest);
        Assert.Equal(HttpStatusCode.Created, transferResponse.StatusCode);

        var selfTransfer = transferRequest with { TargetNodeId = source.Id };
        var selfTransferResponse = await client.PostAsJsonAsync("/api/transfers", selfTransfer);
        Assert.Equal(HttpStatusCode.BadRequest, selfTransferResponse.StatusCode);

        var futureTransfer = transferRequest with { TransferredAt = DateTimeOffset.UtcNow.AddDays(1) };
        var futureResponse = await client.PostAsJsonAsync("/api/transfers", futureTransfer);
        Assert.Equal(HttpStatusCode.BadRequest, futureResponse.StatusCode);

        var paginationResponse = await client.GetAsync(
            $"/api/networks?page=1&pageSize=10&scientificName=Integration");
        Assert.Equal(HttpStatusCode.OK, paginationResponse.StatusCode);
        var page = await paginationResponse.Content.ReadFromJsonAsync<PagedResult<MyceliumNetwork>>();
        Assert.NotNull(page);
        Assert.Contains(page.Items, item => item.Id == network.Id);

        var statusResponse = await client.PatchAsJsonAsync(
            $"/api/sensors/{source.Id}/status",
            new SetSensorStatusRequest(false));
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);

        var inactiveResponse = await client.PostAsJsonAsync("/api/transfers", transferRequest);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, inactiveResponse.StatusCode);

        var otherNetwork = await CreateNetworkAsync(client, $"Other-{Guid.NewGuid():N}");
        var otherSensor = await CreateSensorAsync(client, otherNetwork.Id, "50,60");
        var crossNetworkResponse = await client.PostAsJsonAsync(
            "/api/transfers",
            new CreateNutrientTransferRequest(
                target.Id,
                otherSensor.Id,
                100,
                transferredAt));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, crossNetworkResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.DeleteAsync($"/api/networks/{otherNetwork.Id}")).StatusCode);

        var deleteResponse = await client.DeleteAsync($"/api/networks/{network.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [PostgresFact]
    public async Task ApiRateLimit_IsAppliedPerClient()
    {
        var connectionString = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION")!;
        await using var factory = new ApiFactory(connectionString, permitLimit: 1);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", ApiKey);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/networks")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/api/networks")).StatusCode);
    }

    private static async Task<SensorNode> CreateSensorAsync(
        HttpClient client,
        Guid networkId,
        string location)
    {
        var response = await client.PostAsJsonAsync(
            "/api/sensors",
            new CreateSensorNodeRequest(networkId, location, 60, true));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SensorNode>())!;
    }

    private static async Task<MyceliumNetwork> CreateNetworkAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync(
            "/api/networks",
            new CreateMyceliumNetworkRequest(
                name,
                "Florestal",
                DateTimeOffset.UtcNow.AddDays(-1)));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MyceliumNetwork>())!;
    }

    private sealed class ApiFactory(
        string connectionString,
        int permitLimit = 1000) : WebApplicationFactory<Program>
    {
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
                    ["RateLimit:PermitLimit"] = permitLimit.ToString()
                });
            });
        }
    }
}
