using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gatekeeper.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gatekeeper.Tests;

public sealed class AccessRequestEndpointTests
{
    [Fact]
    public async Task PostCreatesRequestAndGetByIdReturnsRequestAndListContainsRequest()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage postResponse = await client.PostAsJsonAsync(
            "/api/v1/access-requests",
            CreateValidRequest(),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
        using JsonDocument createDocument = await JsonDocument.ParseAsync(
            await postResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken
        );
        Guid id = createDocument.RootElement.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal("Pending", createDocument.RootElement.GetProperty("status").GetString());

        using HttpResponseMessage getResponse = await client.GetAsync(
            $"/api/v1/access-requests/{id}",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        using JsonDocument getDocument = await JsonDocument.ParseAsync(
            await getResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Equal(id, getDocument.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(
            "Investigate production incident",
            getDocument.RootElement.GetProperty("intent").GetString()
        );
        Assert.Equal("Pending", getDocument.RootElement.GetProperty("status").GetString());

        using HttpResponseMessage listResponse = await client.GetAsync(
            "/api/v1/access-requests",
            TestContext.Current.CancellationToken
        );

        string listResponseBody = await listResponse.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken
        );
        Assert.True(
            listResponse.StatusCode == HttpStatusCode.OK,
            $"Expected OK but got {listResponse.StatusCode}: {listResponseBody}"
        );
        using JsonDocument listDocument = JsonDocument.Parse(listResponseBody);
        JsonElement item = Assert.Single(
            listDocument.RootElement.GetProperty("items").EnumerateArray()
        );
        Assert.Equal(id, item.GetProperty("id").GetGuid());

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        GatekeeperDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<GatekeeperDbContext>();
        int auditCount = await dbContext.AuditEvents.CountAsync(
            TestContext.Current.CancellationToken
        );
        Assert.Equal(1, auditCount);
    }

    [Fact]
    public async Task GetUnknownIdReturnsNotFound()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/access-requests/{Guid.NewGuid()}",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task InvalidPostReturnsBadRequest()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/access-requests",
            new
            {
                intent = "",
                requester = "",
                durationMinutes = 0,
                risk = "Medium",
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostWithWhitespaceListItemsReturnsBadRequest()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/access-requests",
            new
            {
                intent = "Investigate production incident",
                requester = "agent-1",
                targets = new[] { " " },
                requestedCapabilities = new[] { "logs:read" },
                durationMinutes = 30,
                risk = "Low",
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static object CreateValidRequest()
    {
        return new
        {
            intent = "Investigate production incident",
            requester = "agent-1",
            targets = new[] { "prod-api" },
            requestedCapabilities = new[] { "read_logs" },
            durationMinutes = 30,
            risk = 1,
            justification = "Need diagnosis",
            proposedActions = new[] { "tail logs" },
            forbiddenActions = new[] { "restart service" },
            metadata = new Dictionary<string, string> { ["ticket"] = "INC-1" },
        };
    }

    private sealed class AccessRequestApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databasePath;

        public AccessRequestApiFactory()
        {
            _databasePath = Path.Combine(Path.GetTempPath(), $"gatekeeper-{Guid.NewGuid():N}.db");
            Environment.SetEnvironmentVariable("GATEKEEPER_SQLITE_DATA_PATH", _databasePath);
        }

        public async Task MigrateAsync(CancellationToken cancellationToken)
        {
            await using AsyncServiceScope scope = Services.CreateAsyncScope();
            GatekeeperDbContext dbContext =
                scope.ServiceProvider.GetRequiredService<GatekeeperDbContext>();
            await dbContext.Database.MigrateAsync(cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Environment.SetEnvironmentVariable("GATEKEEPER_SQLITE_DATA_PATH", null);
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
    }
}
