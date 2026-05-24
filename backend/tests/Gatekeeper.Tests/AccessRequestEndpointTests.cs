using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gatekeeper.Core.Sessions;
using Gatekeeper.Infrastructure.Persistence;
using Gatekeeper.Infrastructure.Persistence.Entities;
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
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );

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
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );

        using HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/access-requests/{Guid.NewGuid()}",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ApproveUnknownIdWithLoginCookieReturnsNotFound()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        await LoginAsAdminAsync(client);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/access-requests/{Guid.NewGuid()}/approve",
            new { comment = "approved" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DenyUnknownIdWithLoginCookieReturnsNotFound()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        await LoginAsAdminAsync(client);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/access-requests/{Guid.NewGuid()}/deny",
            new { comment = "denied" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ApproveWithoutCookieReturnsUnauthorized()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid accessRequestId = await CreateAccessRequestAsync(client);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/access-requests/{accessRequestId}/approve",
            new { comment = "approved" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ApproveWithLegacyTokenOnlyReturnsUnauthorized()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid accessRequestId = await CreateAccessRequestAsync(client);
        client.DefaultRequestHeaders.Add("X-Gatekeeper-Admin-Token", "test-admin-token");

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/access-requests/{accessRequestId}/approve",
            new { comment = "approved" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ApproveWithMultipleLegacyTokenHeaderValuesReturnsUnauthorized()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
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

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ApproveWithLegacyTokenOnlyAndNoConfiguredServerTokenReturnsUnauthorized()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory(null);
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid accessRequestId = await CreateAccessRequestAsync(client);
        client.DefaultRequestHeaders.Add("X-Gatekeeper-Admin-Token", "test-admin-token");

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/access-requests/{accessRequestId}/approve",
            new { comment = "approved" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ApproveWithLoginCookieApprovesAndReturnsSessionId()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid accessRequestId = await CreateAccessRequestAsync(client);
        await LoginAsAdminAsync(client);

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
    public async Task ApproveWithLoginCookieAndCrossSiteOriginReturnsForbidden()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid accessRequestId = await CreateAccessRequestAsync(client);
        await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Remove("Origin");
        client.DefaultRequestHeaders.Add("Origin", "https://attacker.example");

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/access-requests/{accessRequestId}/approve",
            new { comment = "approved" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ApproveWithLoginCookieAndMissingSameOriginSignalsReturnsForbidden()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid accessRequestId = await CreateAccessRequestAsync(client);
        await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Remove("Origin");

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/access-requests/{accessRequestId}/approve",
            new { comment = "approved" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DenyWithLoginCookieSetsDenied()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid accessRequestId = await CreateAccessRequestAsync(client);
        await LoginAsAdminAsync(client);

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
    public async Task DenyWithoutCookieReturnsUnauthorized()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid accessRequestId = await CreateAccessRequestAsync(client);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/access-requests/{accessRequestId}/deny",
            new { comment = "denied" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DenyWithLegacyTokenOnlyReturnsUnauthorized()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid accessRequestId = await CreateAccessRequestAsync(client);
        client.DefaultRequestHeaders.Add("X-Gatekeeper-Admin-Token", "test-admin-token");

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/access-requests/{accessRequestId}/deny",
            new { comment = "denied" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DenyWithMultipleLegacyTokenHeaderValuesReturnsUnauthorized()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
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

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DenyWithLegacyTokenOnlyAndNoConfiguredServerTokenReturnsUnauthorized()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory(null);
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid accessRequestId = await CreateAccessRequestAsync(client);
        client.DefaultRequestHeaders.Add("X-Gatekeeper-Admin-Token", "test-admin-token");

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/access-requests/{accessRequestId}/deny",
            new { comment = "denied" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SecondApproveOrDenyReturnsConflict()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid accessRequestId = await CreateAccessRequestAsync(client);
        await LoginAsAdminAsync(client);

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
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid accessRequestId = await CreateAccessRequestAsync(client);
        await LoginAsAdminAsync(client);
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
    public async Task CompleteSessionCompletesActiveSessionWithoutAdminToken()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid accessRequestId = await CreateAccessRequestAsync(client);
        Guid sessionId = await ApproveAccessRequestAsync(client, accessRequestId);

        using HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/sessions/{sessionId}/complete",
            content: null,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Equal(sessionId, document.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("completed", document.RootElement.GetProperty("status").GetString());
        Assert.Equal(
            JsonValueKind.String,
            document.RootElement.GetProperty("completedAt").ValueKind
        );
        await AssertAuditEventExistsAsync(factory, sessionId, "SessionCompleted");
    }

    [Fact]
    public async Task RevokeSessionRequiresLoginCookieAndRevokesActiveSession()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid accessRequestId = await CreateAccessRequestAsync(client);
        Guid sessionId = await ApproveAccessRequestAsync(client, accessRequestId);

        using HttpClient anonymousClient = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        using HttpResponseMessage missingTokenResponse = await anonymousClient.PostAsync(
            $"/api/v1/sessions/{sessionId}/revoke",
            content: null,
            TestContext.Current.CancellationToken
        );
        using HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/sessions/{sessionId}/revoke",
            content: null,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Unauthorized, missingTokenResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Equal(sessionId, document.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("revoked", document.RootElement.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.String, document.RootElement.GetProperty("revokedAt").ValueKind);
        await AssertAuditEventExistsAsync(factory, sessionId, "SessionRevoked");
    }

    [Fact]
    public async Task CompleteSessionReturnsConflictForTerminalSession()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid accessRequestId = await CreateAccessRequestAsync(client);
        Guid sessionId = await ApproveAccessRequestAsync(client, accessRequestId);

        using HttpResponseMessage firstResponse = await client.PostAsync(
            $"/api/v1/sessions/{sessionId}/complete",
            content: null,
            TestContext.Current.CancellationToken
        );
        using HttpResponseMessage secondResponse = await client.PostAsync(
            $"/api/v1/sessions/{sessionId}/complete",
            content: null,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task GetAndLifecycleMutationMaterializeExpiredActiveSessionOnce()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid sessionId = Guid.NewGuid();
        await SeedExpiredSessionAsync(factory, sessionId);

        using HttpResponseMessage getResponse = await client.GetAsync(
            $"/api/v1/sessions/{sessionId}",
            TestContext.Current.CancellationToken
        );
        using HttpResponseMessage completeResponse = await client.PostAsync(
            $"/api/v1/sessions/{sessionId}/complete",
            content: null,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        using JsonDocument document = await JsonDocument.ParseAsync(
            await getResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Equal("expired", document.RootElement.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, completeResponse.StatusCode);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        GatekeeperDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<GatekeeperDbContext>();
        int expiredAuditCount = await dbContext.AuditEvents.CountAsync(
            auditEvent =>
                auditEvent.AggregateId == sessionId && auditEvent.EventType == "SessionExpired",
            TestContext.Current.CancellationToken
        );
        Assert.Equal(1, expiredAuditCount);
    }

    [Fact]
    public async Task GetUnknownSessionReturnsNotFound()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );

        using HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/sessions/{Guid.NewGuid()}",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CompleteSession_Should_ReturnBadRequest_When_RouteIdIsMalformed()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );

        using HttpResponseMessage response = await client.PostAsync(
            "/api/v1/sessions/not-a-guid/complete",
            content: null,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RevokeSession_Should_ReturnBadRequest_When_RouteIdIsMalformed()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        await LoginAsAdminAsync(client);

        using HttpResponseMessage response = await client.PostAsync(
            "/api/v1/sessions/not-a-guid/revoke",
            content: null,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Should_ExecuteAllowedDummyAction_When_RequestApprovedSessionAllowsCapability()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid accessRequestId = await CreateAccessRequestAsync(client, ["test.echo"]);
        Guid sessionId = await ApproveAccessRequestAsync(client, accessRequestId);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/sessions/{sessionId}/actions",
            new { capability = "test.echo", payload = new { message = "hello" } },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Equal(sessionId, document.RootElement.GetProperty("sessionId").GetGuid());
        Assert.Equal("test.echo", document.RootElement.GetProperty("capability").GetString());
        Assert.Equal("succeeded", document.RootElement.GetProperty("status").GetString());
        Assert.Equal(
            "hello",
            document.RootElement.GetProperty("result").GetProperty("message").GetString()
        );

        await AssertAuditEventExistsAsync(factory, accessRequestId, "AccessRequestCreated");
        await AssertAuditEventExistsAsync(factory, accessRequestId, "AccessRequestApproved");
        await AssertAuditEventExistsAsync(factory, sessionId, "SessionCreated");
        await AssertAuditEventExistsAsync(factory, sessionId, "SessionActionRequested");
        await AssertAuditEventExistsAsync(factory, sessionId, "SessionActionAllowed");
        await AssertAuditEventExistsAsync(factory, sessionId, "SessionActionExecuted");
    }

    [Fact]
    public async Task Should_ExecuteStatusRead_When_RequestApprovedSessionAllowsCapability()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid accessRequestId = await CreateAccessRequestAsync(client, ["test.status.read"]);
        Guid sessionId = await ApproveAccessRequestAsync(client, accessRequestId);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/sessions/{sessionId}/actions",
            new { capability = "test.status.read", payload = new { } },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Equal(sessionId, document.RootElement.GetProperty("sessionId").GetGuid());
        Assert.Equal(
            "test.status.read",
            document.RootElement.GetProperty("capability").GetString()
        );
        Assert.Equal(
            "ok",
            document.RootElement.GetProperty("result").GetProperty("status").GetString()
        );
    }

    [Fact]
    public async Task Should_ReturnBadRequest_When_DummyActionPayloadIsInvalid()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid accessRequestId = await CreateAccessRequestAsync(client, ["test.echo"]);
        Guid sessionId = await ApproveAccessRequestAsync(client, accessRequestId);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/sessions/{sessionId}/actions",
            new { capability = "test.echo", payload = new { } },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertAuditEventExistsAsync(factory, sessionId, "SessionActionRequested");
        await AssertAuditEventExistsAsync(factory, sessionId, "SessionActionFailed");
        await AssertAuditEventDoesNotExistAsync(factory, sessionId, "SessionActionAllowed");
        Assert.Equal(0, await GetSessionActionCountAsync(factory, sessionId));
    }

    [Fact]
    public async Task Should_ReturnForbidden_When_ActionCapabilityIsNotAllowed()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid accessRequestId = await CreateAccessRequestAsync(client, ["test.status.read"]);
        Guid sessionId = await ApproveAccessRequestAsync(client, accessRequestId);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/sessions/{sessionId}/actions",
            new { capability = "test.echo", payload = new { message = "hello" } },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertAuditEventExistsAsync(factory, sessionId, "SessionActionDenied");
        Assert.Equal(0, await GetSessionActionCountAsync(factory, sessionId));
    }

    [Fact]
    public async Task Should_ReturnConflictWithoutConsumingBudget_When_SessionIsCompleted()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid accessRequestId = await CreateAccessRequestAsync(client, ["test.echo"]);
        Guid sessionId = await ApproveAccessRequestAsync(client, accessRequestId);

        using HttpResponseMessage completeResponse = await client.PostAsync(
            $"/api/v1/sessions/{sessionId}/complete",
            content: null,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/sessions/{sessionId}/actions",
            new { capability = "test.echo", payload = new { message = "hello" } },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(0, await GetSessionActionCountAsync(factory, sessionId));
        await AssertAuditEventExistsAsync(factory, sessionId, "SessionActionDenied");
    }

    [Fact]
    public async Task Should_ReturnConflictWithoutConsumingBudget_When_SessionIsRevoked()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid accessRequestId = await CreateAccessRequestAsync(client, ["test.echo"]);
        Guid sessionId = await ApproveAccessRequestAsync(client, accessRequestId);

        using HttpResponseMessage revokeResponse = await client.PostAsync(
            $"/api/v1/sessions/{sessionId}/revoke",
            content: null,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/sessions/{sessionId}/actions",
            new { capability = "test.echo", payload = new { message = "hello" } },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(0, await GetSessionActionCountAsync(factory, sessionId));
        await AssertAuditEventExistsAsync(factory, sessionId, "SessionActionDenied");
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_SessionDoesNotExist()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/sessions/{Guid.NewGuid()}/actions",
            new { capability = "test.echo", payload = new { message = "hello" } },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Should_ReturnConflict_When_SessionIsExpired()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid sessionId = Guid.NewGuid();
        await SeedExpiredSessionAsync(factory, sessionId);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/sessions/{sessionId}/actions",
            new { capability = "test.echo", payload = new { message = "hello" } },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertAuditEventExistsAsync(factory, sessionId, "SessionActionDenied");
        Assert.Equal(0, await GetSessionActionCountAsync(factory, sessionId));
    }

    [Fact]
    public async Task Should_ReturnConflict_When_DummyAdapterFails()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid accessRequestId = await CreateAccessRequestAsync(client, ["test.fail"]);
        Guid sessionId = await ApproveAccessRequestAsync(client, accessRequestId);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/sessions/{sessionId}/actions",
            new { capability = "test.fail" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertAuditEventExistsAsync(factory, sessionId, "SessionActionFailed");
        Assert.Equal(1, await GetSessionActionCountAsync(factory, sessionId));
    }

    [Fact]
    public async Task Should_ReturnConflictAndAudit_When_ActionCountLimitIsExceeded()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory(
            maxActionCount: 1
        );
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid accessRequestId = await CreateAccessRequestAsync(client, ["test.echo"]);
        Guid sessionId = await ApproveAccessRequestAsync(client, accessRequestId);

        using HttpResponseMessage firstResponse = await client.PostAsJsonAsync(
            $"/api/v1/sessions/{sessionId}/actions",
            new { capability = "test.echo", payload = new { message = "hello" } },
            TestContext.Current.CancellationToken
        );
        using HttpResponseMessage secondResponse = await client.PostAsJsonAsync(
            $"/api/v1/sessions/{sessionId}/actions",
            new { capability = "test.echo", payload = new { message = "again" } },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        Assert.Equal(1, await GetSessionActionCountAsync(factory, sessionId));
        await AssertAuditEventExistsAsync(factory, sessionId, "ActionCountExceeded");
    }

    [Fact]
    public async Task Should_NotExceedActionBudget_When_ActionsRunInParallel()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory(
            maxActionCount: 1
        );
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid accessRequestId = await CreateAccessRequestAsync(client, ["test.echo"]);
        Guid sessionId = await ApproveAccessRequestAsync(client, accessRequestId);

        Task<HttpResponseMessage>[] requests = Enumerable
            .Range(0, 10)
            .Select(index =>
                client.PostAsJsonAsync(
                    $"/api/v1/sessions/{sessionId}/actions",
                    new { capability = "test.echo", payload = new { message = $"hello-{index}" } },
                    TestContext.Current.CancellationToken
                )
            )
            .ToArray();
        HttpResponseMessage[] responses = await Task.WhenAll(requests);

        try
        {
            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
            Assert.Equal(
                9,
                responses.Count(response => response.StatusCode == HttpStatusCode.Conflict)
            );
            Assert.Equal(1, await GetSessionActionCountAsync(factory, sessionId));
        }
        finally
        {
            foreach (HttpResponseMessage response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task InvalidPostReturnsBadRequest()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );

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
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );

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

    private static async Task LoginAsAdminAsync(HttpClient client)
    {
        using HttpResponseMessage loginResponse = await client.PostAsJsonAsync(
            "/api/v1/admin/login",
            new { username = "admin", password = "correct-password" },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        if (!client.DefaultRequestHeaders.Contains("Origin"))
        {
            client.DefaultRequestHeaders.Add("Origin", "http://localhost");
        }
    }

    private static async Task<Guid> CreateAccessRequestAsync(HttpClient client)
    {
        return await CreateAccessRequestAsync(client, ["read_logs"]);
    }

    private static async Task<Guid> CreateAccessRequestAsync(
        HttpClient client,
        IReadOnlyList<string> requestedCapabilities
    )
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/access-requests",
            CreateValidRequest(requestedCapabilities),
            TestContext.Current.CancellationToken
        );
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken
        );

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> ApproveAccessRequestAsync(
        HttpClient client,
        Guid accessRequestId
    )
    {
        await LoginAsAdminAsync(client);
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

        return document.RootElement.GetProperty("sessionId").GetGuid();
    }

    private static async Task AssertAuditEventExistsAsync(
        AccessRequestApiFactory factory,
        Guid aggregateId,
        string eventType
    )
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        GatekeeperDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<GatekeeperDbContext>();
        bool exists = await dbContext.AuditEvents.AnyAsync(
            auditEvent =>
                auditEvent.AggregateId == aggregateId && auditEvent.EventType == eventType,
            TestContext.Current.CancellationToken
        );
        Assert.True(exists, $"Expected audit event {eventType} for aggregate {aggregateId}.");
    }

    private static async Task AssertAuditEventDoesNotExistAsync(
        AccessRequestApiFactory factory,
        Guid aggregateId,
        string eventType
    )
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        GatekeeperDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<GatekeeperDbContext>();
        bool exists = await dbContext.AuditEvents.AnyAsync(
            auditEvent =>
                auditEvent.AggregateId == aggregateId && auditEvent.EventType == eventType,
            TestContext.Current.CancellationToken
        );
        Assert.False(
            exists,
            $"Did not expect audit event {eventType} for aggregate {aggregateId}."
        );
    }

    private static async Task<int> GetSessionActionCountAsync(
        AccessRequestApiFactory factory,
        Guid sessionId
    )
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        GatekeeperDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<GatekeeperDbContext>();
        return await dbContext
            .Sessions.Where(session => session.Id == sessionId)
            .Select(session => session.ActionCount)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private static async Task SeedExpiredSessionAsync(
        AccessRequestApiFactory factory,
        Guid sessionId
    )
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        GatekeeperDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<GatekeeperDbContext>();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await dbContext.Sessions.AddAsync(
            new SessionEntity
            {
                Id = sessionId,
                AccessRequestId = Guid.NewGuid(),
                Status = SessionStatus.Active,
                AllowedTargetsJson = JsonSerializer.Serialize(new[] { "prod-api" }),
                AllowedCapabilitiesJson = JsonSerializer.Serialize(new[] { "test.echo" }),
                CreatedAt = now.AddHours(-2),
                ExpiresAt = now.AddHours(-1),
                ActionCount = 0,
                MaxActionCount = 10,
            },
            TestContext.Current.CancellationToken
        );
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static object CreateValidRequest()
    {
        return CreateValidRequest(["read_logs"]);
    }

    private static object CreateValidRequest(IReadOnlyList<string> requestedCapabilities)
    {
        return new
        {
            intent = "Investigate production incident",
            requester = "agent-1",
            targets = new[] { "prod-api" },
            requestedCapabilities = requestedCapabilities,
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

        public AccessRequestApiFactory(
            string? adminToken = "test-admin-token",
            int? maxActionCount = null
        )
        {
            _databasePath = Path.Combine(Path.GetTempPath(), $"gatekeeper-{Guid.NewGuid():N}.db");
            Environment.SetEnvironmentVariable("GATEKEEPER_SQLITE_DATA_PATH", _databasePath);
            Environment.SetEnvironmentVariable("GATEKEEPER_ADMIN_TOKEN", adminToken);
            Environment.SetEnvironmentVariable("GATEKEEPER_ADMIN_USERNAME", "admin");
            Environment.SetEnvironmentVariable("GATEKEEPER_ADMIN_PASSWORD", "correct-password");
            Environment.SetEnvironmentVariable("GATEKEEPER_ADMIN_COOKIE_SECURE", "false");
            Environment.SetEnvironmentVariable(
                "GATEKEEPER_SESSION_MAX_ACTION_COUNT",
                maxActionCount?.ToString()
            );
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
            Environment.SetEnvironmentVariable("GATEKEEPER_ADMIN_USERNAME", null);
            Environment.SetEnvironmentVariable("GATEKEEPER_ADMIN_PASSWORD", null);
            Environment.SetEnvironmentVariable("GATEKEEPER_ADMIN_COOKIE_SECURE", null);
            Environment.SetEnvironmentVariable("GATEKEEPER_SESSION_MAX_ACTION_COUNT", null);
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
    }
}
