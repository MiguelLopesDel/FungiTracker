using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
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
        var connectionString = PostgresFactAttribute.RequiredConnectionString();

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
    public async Task Listing_WithPageNumberNearIntMaxValue_DoesNotOverflowTheOffset()
    {
        var connectionString = PostgresFactAttribute.RequiredConnectionString();

        await using var factory = new ApiFactory(connectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", ApiKey);

        // (page - 1) * pageSize overflowed int and produced a negative OFFSET,
        // which PostgreSQL rejects, so this used to answer 500.
        foreach (var path in new[] { "/api/networks", "/api/transfers" })
        {
            var response = await client.GetAsync($"{path}?page={int.MaxValue}&pageSize=100");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [PostgresFact]
    public async Task NetworkFilter_TreatsLikeWildcardsAsLiteralCharacters()
    {
        var connectionString = PostgresFactAttribute.RequiredConnectionString();

        await using var factory = new ApiFactory(connectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", ApiKey);

        // The two names differ only at the position of the underscore, so the
        // filter can only tell them apart when '_' is escaped into a literal.
        var marker = Guid.NewGuid().ToString("N");
        var literal = await CreateNetworkAsync(client, $"Wild_{marker}");
        var other = await CreateNetworkAsync(client, $"Wilda{marker}");

        // '_' is a single-character wildcard in ILIKE, so before escaping this
        // filter also matched the network that has no underscore at all.
        var response = await client.GetAsync($"/api/networks?scientificName=Wild_{marker}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<MyceliumNetwork>>();
        Assert.NotNull(page);

        Assert.Contains(page.Items, item => item.Id == literal.Id);
        Assert.DoesNotContain(page.Items, item => item.Id == other.Id);
    }

    [PostgresFact]
    public async Task Network_WithFutureDiscoveryDate_IsRejected()
    {
        using var client = CreateAuthenticatedClient(out var factory);
        await using var _ = factory;

        var response = await client.PostAsJsonAsync(
            "/api/networks",
            new CreateMyceliumNetworkRequest(
                $"Future-{Guid.NewGuid():N}",
                "Florestal",
                DateTimeOffset.UtcNow.AddYears(1)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [PostgresFact]
    public async Task Transfer_WithoutTimestamp_IsStoredWithTheCurrentInstant()
    {
        using var client = CreateAuthenticatedClient(out var factory);
        await using var _ = factory;

        var network = await CreateNetworkAsync(client, $"Default-{Guid.NewGuid():N}");
        var source = await CreateSensorAsync(client, network.Id, "10,20");
        var target = await CreateSensorAsync(client, network.Id, "30,40");

        var before = DateTimeOffset.UtcNow.AddSeconds(-5);
        var response = await client.PostAsJsonAsync(
            "/api/transfers",
            new CreateNutrientTransferRequest(source.Id, target.Id, 100));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<TransferView>();
        Assert.NotNull(created);
        Assert.InRange(created.TransferredAt, before, DateTimeOffset.UtcNow.AddSeconds(5));
    }

    [PostgresFact]
    public async Task HighEnergyTransfers_IncludeTheThresholdItself()
    {
        using var client = CreateAuthenticatedClient(out var factory);
        await using var _ = factory;

        var network = await CreateNetworkAsync(client, $"Energy-{Guid.NewGuid():N}");
        var source = await CreateSensorAsync(client, network.Id, "10,20");
        var target = await CreateSensorAsync(client, network.Id, "30,40");

        // The threshold defaults to 500 mg and the filter uses >=, so a
        // transfer of exactly 500 belongs in the list and 499 does not.
        var atThreshold = await CreateTransferAsync(client, source.Id, target.Id, 500);
        var belowThreshold = await CreateTransferAsync(client, source.Id, target.Id, 499);

        var page = await client.GetFromJsonAsync<PagedResult<TransferView>>(
            "/api/transfers/high-energy?pageSize=100");
        Assert.NotNull(page);

        Assert.Contains(page.Items, item => item.Id == atThreshold.Id);
        Assert.DoesNotContain(page.Items, item => item.Id == belowThreshold.Id);
    }

    [PostgresFact]
    public async Task DeletingANetwork_AlsoRemovesItsSensorsAndTransfers()
    {
        using var client = CreateAuthenticatedClient(out var factory);
        await using var _ = factory;

        var network = await CreateNetworkAsync(client, $"Cascade-{Guid.NewGuid():N}");
        var source = await CreateSensorAsync(client, network.Id, "10,20");
        var target = await CreateSensorAsync(client, network.Id, "30,40");
        var transfer = await CreateTransferAsync(client, source.Id, target.Id, 120);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.DeleteAsync($"/api/networks/{network.Id}")).StatusCode);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/sensors/{source.Id}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/transfers/{transfer.Id}")).StatusCode);
    }

    [PostgresFact]
    public async Task SensorListing_FiltersByActiveState()
    {
        using var client = CreateAuthenticatedClient(out var factory);
        await using var _ = factory;

        var network = await CreateNetworkAsync(client, $"Filter-{Guid.NewGuid():N}");
        var active = await CreateSensorAsync(client, network.Id, "10,20");
        var inactive = await CreateSensorAsync(client, network.Id, "30,40");

        await client.PatchAsJsonAsync(
            $"/api/sensors/{inactive.Id}/status",
            new SetSensorStatusRequest(false));

        var page = await client.GetFromJsonAsync<PagedResult<SensorNode>>(
            $"/api/networks/{network.Id}/sensors?isActive=true");
        Assert.NotNull(page);

        Assert.Contains(page.Items, item => item.Id == active.Id);
        Assert.DoesNotContain(page.Items, item => item.Id == inactive.Id);
    }

    [PostgresFact]
    public async Task UnknownResources_AnswerNotFound()
    {
        using var client = CreateAuthenticatedClient(out var factory);
        await using var _ = factory;

        var missing = Guid.NewGuid();

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/networks/{missing}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/sensors/{missing}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync("/api/transfers/999999999")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/networks/{missing}/sensors")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.DeleteAsync($"/api/sensors/{missing}")).StatusCode);

        // A sensor cannot be attached to a network that does not exist.
        var orphan = await client.PostAsJsonAsync(
            "/api/sensors",
            new CreateSensorNodeRequest(missing, "10,20", 50, true));
        Assert.Equal(HttpStatusCode.NotFound, orphan.StatusCode);

        // Neither can a transfer reference sensors that do not exist.
        var ghostTransfer = await client.PostAsJsonAsync(
            "/api/transfers",
            new CreateNutrientTransferRequest(Guid.NewGuid(), Guid.NewGuid(), 10));
        Assert.Equal(HttpStatusCode.NotFound, ghostTransfer.StatusCode);
    }

    [PostgresFact]
    public async Task TransferFilters_RejectInvalidRanges()
    {
        using var client = CreateAuthenticatedClient(out var factory);
        await using var _ = factory;

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await client.GetAsync("/api/transfers?minimumCarbonMg=-1")).StatusCode);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await client.GetAsync(
                "/api/transfers?from=2025-01-02T00:00:00Z&to=2025-01-01T00:00:00Z")).StatusCode);
    }

    [PostgresFact]
    public async Task MalformedRequestBody_AnswersBadRequestInsteadOfServerError()
    {
        using var client = CreateAuthenticatedClient(out var factory);
        await using var _ = factory;

        using var content = new StringContent(
            """{"sourceNodeId":"not-a-guid","targetNodeId":"x","carbonAmountMg":1}""",
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync("/api/transfers", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Whatever the failure, the client must never receive internals.
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Npgsql", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("System.", body, StringComparison.Ordinal);
    }

    [PostgresFact]
    public async Task ServerErrors_DoNotLeakInternalDetails()
    {
        using var client = CreateAuthenticatedClient(out var factory);
        await using var _ = factory;

        var response = await client.GetAsync("/api/networks?scientificName=%27%20OR%20%271%27%3D%271");

        // The injection attempt is treated as a literal search, not as SQL.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<MyceliumNetwork>>();
        Assert.NotNull(page);
        Assert.Empty(page.Items);
    }

    [PostgresFact]
    public async Task SensorLocation_RoundTripsThroughThePostgresPointFormat()
    {
        using var client = CreateAuthenticatedClient(out var factory);
        await using var _ = factory;

        var network = await CreateNetworkAsync(client, $"Point-{Guid.NewGuid():N}");

        // A point column is read back as "(x,y)", which is not the "x,y" the
        // caller sent. The parser has to keep accepting both, otherwise an
        // update built from a previous GET would start being rejected.
        var created = await CreateSensorAsync(client, network.Id, "10.5,20.25");
        Assert.Equal("(10.5,20.25)", created.Location);

        var updated = await client.PutAsJsonAsync(
            $"/api/sensors/{created.Id}",
            new UpdateSensorNodeRequest(created.Location, 50, true));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
    }

    [PostgresFact]
    public async Task EveryTransferEndpoint_ReturnsTheSameShape()
    {
        using var client = CreateAuthenticatedClient(out var factory);
        await using var _ = factory;

        var network = await CreateNetworkAsync(client, $"Shape-{Guid.NewGuid():N}");
        var source = await CreateSensorAsync(client, network.Id, "10,20");
        var target = await CreateSensorAsync(client, network.Id, "30,40");

        // The listing used to be richer than the detail: it carried both sensor
        // locations and the single-transfer responses did not.
        var created = await CreateTransferAsync(client, source.Id, target.Id, 900);
        Assert.Equal("(10,20)", created.SourceLocation);
        Assert.Equal("(30,40)", created.TargetLocation);

        var detail = await client.GetFromJsonAsync<TransferView>($"/api/transfers/{created.Id}");
        Assert.NotNull(detail);
        Assert.Equal(created.SourceLocation, detail.SourceLocation);
        Assert.Equal(created.TargetLocation, detail.TargetLocation);

        var page = await client.GetFromJsonAsync<PagedResult<TransferView>>(
            $"/api/transfers?sourceNodeId={source.Id}");
        Assert.NotNull(page);
        var listed = Assert.Single(page.Items);
        Assert.Equal(created.SourceLocation, listed.SourceLocation);
        Assert.Equal(created.TargetLocation, listed.TargetLocation);
    }

    [PostgresFact]
    public async Task EveryListing_WorksWithoutExplicitPaging()
    {
        using var client = CreateAuthenticatedClient(out var factory);
        await using var _ = factory;

        var network = await CreateNetworkAsync(client, $"Paging-{Guid.NewGuid():N}");

        // Every test used to pass page and pageSize explicitly, which hid a
        // binding regression where the transfer listing answered 400 unless
        // paging was stated.
        foreach (var path in new[]
        {
            "/api/networks",
            "/api/transfers",
            "/api/transfers/high-energy",
            $"/api/networks/{network.Id}/sensors",
        })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [PostgresFact]
    public async Task ApiRateLimit_IsAppliedPerClient()
    {
        var connectionString = PostgresFactAttribute.RequiredConnectionString();
        await using var factory = new ApiFactory(connectionString, permitLimit: 1);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", ApiKey);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/networks")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/api/networks")).StatusCode);
    }

    private static HttpClient CreateAuthenticatedClient(out ApiFactory factory)
    {
        var connectionString = PostgresFactAttribute.RequiredConnectionString();
        factory = new ApiFactory(connectionString);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", ApiKey);
        return client;
    }

    private static async Task<TransferView> CreateTransferAsync(
        HttpClient client,
        Guid sourceId,
        Guid targetId,
        int carbonAmountMg)
    {
        var response = await client.PostAsJsonAsync(
            "/api/transfers",
            new CreateNutrientTransferRequest(sourceId, targetId, carbonAmountMg));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TransferView>())!;
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
                    ["RateLimit:PermitLimit"] = permitLimit.ToString(CultureInfo.InvariantCulture)
                });
            });
        }
    }
}
