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
        Assert.Equal("pending", createDocument.RootElement.GetProperty("status").GetString());

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
        Assert.Equal("pending", getDocument.RootElement.GetProperty("status").GetString());

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
    public async Task ApproveUnknownIdWithCorrectTokenReturnsNotFound()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Gatekeeper-Admin-Token", "test-admin-token");

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/access-requests/{Guid.NewGuid()}/approve",
            new { comment = "approved" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DenyUnknownIdWithCorrectTokenReturnsNotFound()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Gatekeeper-Admin-Token", "test-admin-token");

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/access-requests/{Guid.NewGuid()}/deny",
            new { comment = "denied" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ApproveWithoutTokenReturnsUnauthorized()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient();
        Guid accessRequestId = await CreateAccessRequestAsync(client);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/access-requests/{accessRequestId}/approve",
            new { comment = "approved" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ApproveWithWrongTokenReturnsForbidden()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient();
        Guid accessRequestId = await CreateAccessRequestAsync(client);
        client.DefaultRequestHeaders.Add("X-Gatekeeper-Admin-Token", "wrong-token");

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/access-requests/{accessRequestId}/approve",
            new { comment = "approved" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ApproveWithMultipleTokenHeaderValuesReturnsForbidden()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient();
        Guid accessRequestId = await CreateAccessRequestAsync(client);
        client.DefaultRequestHeaders.Add(
            "X-Gatekeeper-Admin-Token",
            ["test-admin-token", "test-admin-token"]
        );

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/access-requests/{accessRequestId}/approve",
            new { comment = "approved" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ApproveWithNoConfiguredServerTokenReturnsForbidden()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory(null);
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient();
        Guid accessRequestId = await CreateAccessRequestAsync(client);
        client.DefaultRequestHeaders.Add("X-Gatekeeper-Admin-Token", "test-admin-token");

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/access-requests/{accessRequestId}/approve",
            new { comment = "approved" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ApproveWithCorrectTokenApprovesAndReturnsSessionId()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient();
        Guid accessRequestId = await CreateAccessRequestAsync(client);
        client.DefaultRequestHeaders.Add("X-Gatekeeper-Admin-Token", "test-admin-token");

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/access-requests/{accessRequestId}/approve",
            new { comment = "approved" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Equal(
            accessRequestId,
            document.RootElement.GetProperty("accessRequestId").GetGuid()
        );
        Assert.Equal("approved", document.RootElement.GetProperty("status").GetString());
        Assert.NotEqual(Guid.Empty, document.RootElement.GetProperty("sessionId").GetGuid());
        Assert.True(document.RootElement.TryGetProperty("expiresAt", out JsonElement expiresAt));
        Assert.Equal(JsonValueKind.String, expiresAt.ValueKind);
    }

    [Fact]
    public async Task DenyWithCorrectTokenSetsDenied()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient();
        Guid accessRequestId = await CreateAccessRequestAsync(client);
        client.DefaultRequestHeaders.Add("X-Gatekeeper-Admin-Token", "test-admin-token");

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/access-requests/{accessRequestId}/deny",
            new { comment = "denied" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Equal(
            accessRequestId,
            document.RootElement.GetProperty("accessRequestId").GetGuid()
        );
        Assert.Equal("denied", document.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task DenyWithoutTokenReturnsUnauthorized()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient();
        Guid accessRequestId = await CreateAccessRequestAsync(client);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/access-requests/{accessRequestId}/deny",
            new { comment = "denied" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DenyWithWrongTokenReturnsForbidden()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient();
        Guid accessRequestId = await CreateAccessRequestAsync(client);
        client.DefaultRequestHeaders.Add("X-Gatekeeper-Admin-Token", "wrong-token");

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/access-requests/{accessRequestId}/deny",
            new { comment = "denied" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DenyWithMultipleTokenHeaderValuesReturnsForbidden()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient();
        Guid accessRequestId = await CreateAccessRequestAsync(client);
        client.DefaultRequestHeaders.Add(
            "X-Gatekeeper-Admin-Token",
            ["test-admin-token", "test-admin-token"]
        );

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/access-requests/{accessRequestId}/deny",
            new { comment = "denied" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DenyWithNoConfiguredServerTokenReturnsForbidden()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory(null);
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient();
        Guid accessRequestId = await CreateAccessRequestAsync(client);
        client.DefaultRequestHeaders.Add("X-Gatekeeper-Admin-Token", "test-admin-token");

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/access-requests/{accessRequestId}/deny",
            new { comment = "denied" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SecondApproveOrDenyReturnsConflict()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient();
        Guid accessRequestId = await CreateAccessRequestAsync(client);
        client.DefaultRequestHeaders.Add("X-Gatekeeper-Admin-Token", "test-admin-token");

        using HttpResponseMessage approveResponse = await client.PostAsJsonAsync(
            $"/api/v1/access-requests/{accessRequestId}/approve",
            new { comment = "approved" },
            TestContext.Current.CancellationToken
        );
        using HttpResponseMessage secondApproveResponse = await client.PostAsJsonAsync(
            $"/api/v1/access-requests/{accessRequestId}/approve",
            new { comment = "approved again" },
            TestContext.Current.CancellationToken
        );
        using HttpResponseMessage denyResponse = await client.PostAsJsonAsync(
            $"/api/v1/access-requests/{accessRequestId}/deny",
            new { comment = "deny after approve" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondApproveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, denyResponse.StatusCode);
    }

    [Fact]
    public async Task GetSessionReturnsSessionDetails()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient();
        Guid accessRequestId = await CreateAccessRequestAsync(client);
        client.DefaultRequestHeaders.Add("X-Gatekeeper-Admin-Token", "test-admin-token");
        using HttpResponseMessage approveResponse = await client.PostAsJsonAsync(
            $"/api/v1/access-requests/{accessRequestId}/approve",
            new { comment = "approved" },
            TestContext.Current.CancellationToken
        );
        using JsonDocument approveDocument = await JsonDocument.ParseAsync(
            await approveResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken
        );
        Guid sessionId = approveDocument.RootElement.GetProperty("sessionId").GetGuid();
        client.DefaultRequestHeaders.Remove("X-Gatekeeper-Admin-Token");

        using HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/sessions/{sessionId}",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Equal(sessionId, document.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(
            accessRequestId,
            document.RootElement.GetProperty("accessRequestId").GetGuid()
        );
        Assert.Equal("active", document.RootElement.GetProperty("status").GetString());
        Assert.Equal(
            "prod-api",
            Assert
                .Single(document.RootElement.GetProperty("allowedTargets").EnumerateArray())
                .GetString()
        );
        Assert.Equal(
            "read_logs",
            Assert
                .Single(document.RootElement.GetProperty("allowedCapabilities").EnumerateArray())
                .GetString()
        );
        Assert.True(document.RootElement.TryGetProperty("createdAt", out JsonElement createdAt));
        Assert.True(
            document.RootElement.TryGetProperty("expiresAt", out JsonElement sessionExpiresAt)
        );
        Assert.Equal(JsonValueKind.String, createdAt.ValueKind);
        Assert.Equal(JsonValueKind.String, sessionExpiresAt.ValueKind);
    }

    [Fact]
    public async Task GetUnknownSessionReturnsNotFound()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/sessions/{Guid.NewGuid()}",
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

    private static async Task<Guid> CreateAccessRequestAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/access-requests",
            CreateValidRequest(),
            TestContext.Current.CancellationToken
        );
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken
        );

        return document.RootElement.GetProperty("id").GetGuid();
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

        public AccessRequestApiFactory(string? adminToken = "test-admin-token")
        {
            _databasePath = Path.Combine(Path.GetTempPath(), $"gatekeeper-{Guid.NewGuid():N}.db");
            Environment.SetEnvironmentVariable("GATEKEEPER_SQLITE_DATA_PATH", _databasePath);
            Environment.SetEnvironmentVariable("GATEKEEPER_ADMIN_TOKEN", adminToken);
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
            Environment.SetEnvironmentVariable("GATEKEEPER_ADMIN_TOKEN", null);
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
    }
}
