using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gatekeeper.Api.AgentAuthentication;
using Gatekeeper.Application.AccessRequests;
using Gatekeeper.Application.Sessions;
using Gatekeeper.Core.AccessRequests;
using Gatekeeper.Core.Sessions;
using Gatekeeper.Infrastructure.Persistence;
using Gatekeeper.Infrastructure.Persistence.Entities;
using Gatekeeper.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Gatekeeper.Tests;

public sealed class AccessRequestEndpointTests
{
    private const string TestAgentKey = "test-agent-key";

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
    public async Task PostAccessRequestWithoutAgentKeyReturnsUnauthorizedBeforeValidation()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        client.DefaultRequestHeaders.Remove(AgentAuthConstants.HeaderName);

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

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertFailedAgentAuthenticationAuditAsync(
            factory,
            "/api/v1/access-requests",
            "POST",
            AgentAuthConstants.MissingKeyReason
        );
    }

    [Fact]
    public async Task PostAccessRequestWithInvalidAgentKeyReturnsUnauthorized()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        client.DefaultRequestHeaders.Remove(AgentAuthConstants.HeaderName);
        client.DefaultRequestHeaders.Add(AgentAuthConstants.HeaderName, "wrong-agent-key");

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/access-requests",
            CreateValidRequest(),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertFailedAgentAuthenticationAuditAsync(
            factory,
            "/api/v1/access-requests",
            "POST",
            AgentAuthConstants.InvalidKeyReason
        );
    }

    [Fact]
    public async Task PostAccessRequestWithoutAgentKeyReturnsUnauthorized_WhenFailedAuthAuditWriteFails()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory(
            configureServices: services =>
            {
                services.RemoveAll<IAuditEventRepository>();
                services.AddScoped<IAuditEventRepository, ThrowingAuditEventRepository>();
            }
        );
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        client.DefaultRequestHeaders.Remove(AgentAuthConstants.HeaderName);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/access-requests",
            CreateValidRequest(),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostAccessRequestWithAdminCookieOnlyReturnsUnauthorized()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Remove(AgentAuthConstants.HeaderName);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/access-requests",
            CreateValidRequest(),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertFailedAgentAuthenticationAuditAsync(
            factory,
            "/api/v1/access-requests",
            "POST",
            AgentAuthConstants.MissingKeyReason
        );
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
    public async Task AgentKeyOnlyDoesNotAuthorizeAdminMutationEndpoints()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid accessRequestId = await CreateAccessRequestAsync(client);
        Guid sessionId = await ApproveAccessRequestAsync(client, accessRequestId);
        using HttpClient agentOnlyClient = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );

        using HttpResponseMessage approveResponse = await agentOnlyClient.PostAsJsonAsync(
            $"/api/v1/access-requests/{accessRequestId}/approve",
            new { comment = "approved" },
            TestContext.Current.CancellationToken
        );
        using HttpResponseMessage denyResponse = await agentOnlyClient.PostAsJsonAsync(
            $"/api/v1/access-requests/{accessRequestId}/deny",
            new { comment = "denied" },
            TestContext.Current.CancellationToken
        );
        using HttpResponseMessage revokeResponse = await agentOnlyClient.PostAsync(
            $"/api/v1/sessions/{sessionId}/revoke",
            content: null,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Unauthorized, approveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, denyResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, revokeResponse.StatusCode);
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
    public async Task PostSessionActionWithoutAgentKeyReturnsUnauthorized()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid accessRequestId = await CreateAccessRequestAsync(client, ["test.echo"]);
        Guid sessionId = await ApproveAccessRequestAsync(client, accessRequestId);
        client.DefaultRequestHeaders.Remove(AgentAuthConstants.HeaderName);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/sessions/{sessionId}/actions",
            new { capability = "test.echo", payload = new { message = "hello" } },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, await GetSessionActionCountAsync(factory, sessionId));
        await AssertAuditEventDoesNotExistAsync(factory, sessionId, "SessionActionRequested");
        await AssertFailedAgentAuthenticationAuditAsync(
            factory,
            "/api/v1/sessions/{sessionId}/actions",
            "POST",
            AgentAuthConstants.MissingKeyReason
        );
    }

    [Fact]
    public async Task PostSessionActionWithInvalidAgentKeyReturnsUnauthorized()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid accessRequestId = await CreateAccessRequestAsync(client, ["test.echo"]);
        Guid sessionId = await ApproveAccessRequestAsync(client, accessRequestId);
        client.DefaultRequestHeaders.Remove(AgentAuthConstants.HeaderName);
        client.DefaultRequestHeaders.Add(AgentAuthConstants.HeaderName, "wrong-agent-key");

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/sessions/{sessionId}/actions",
            new { capability = "test.echo", payload = new { message = "hello" } },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, await GetSessionActionCountAsync(factory, sessionId));
        await AssertAuditEventDoesNotExistAsync(factory, sessionId, "SessionActionRequested");
        await AssertFailedAgentAuthenticationAuditAsync(
            factory,
            "/api/v1/sessions/{sessionId}/actions",
            "POST",
            AgentAuthConstants.InvalidKeyReason
        );
    }

    [Fact]
    public async Task PostSessionActionWithAdminCookieOnlyReturnsUnauthorized()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid accessRequestId = await CreateAccessRequestAsync(client, ["test.echo"]);
        Guid sessionId = await ApproveAccessRequestAsync(client, accessRequestId);
        client.DefaultRequestHeaders.Remove(AgentAuthConstants.HeaderName);
        await LoginAsAdminAsync(client);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/sessions/{sessionId}/actions",
            new { capability = "test.echo", payload = new { message = "hello" } },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, await GetSessionActionCountAsync(factory, sessionId));
        await AssertFailedAgentAuthenticationAuditAsync(
            factory,
            "/api/v1/sessions/{sessionId}/actions",
            "POST",
            AgentAuthConstants.MissingKeyReason
        );
    }

    [Fact]
    public async Task FailedAgentAuthenticationAuditContainsOnlyBoundedSafeFields()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        client.DefaultRequestHeaders.Remove(AgentAuthConstants.HeaderName);
        client.DefaultRequestHeaders.Add(AgentAuthConstants.HeaderName, "wrong-agent-key");

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/access-requests",
            CreateValidRequest(),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        JsonElement payload = await GetSingleSystemAuditPayloadAsync(
            factory,
            "AgentAuthenticationFailed"
        );
        string payloadJson = payload.GetRawText();

        Assert.Equal(4, payload.EnumerateObject().Count());
        Assert.Equal("/api/v1/access-requests", payload.GetProperty("routeTemplate").GetString());
        Assert.Equal("POST", payload.GetProperty("httpMethod").GetString());
        Assert.Equal(
            AgentAuthConstants.InvalidKeyReason,
            payload.GetProperty("reasonCode").GetString()
        );
        Assert.Equal(
            AgentAuthConstants.ApiKeyAuthMethod,
            payload.GetProperty("authMethod").GetString()
        );
        Assert.DoesNotContain(TestAgentKey, payloadJson);
        Assert.DoesNotContain("wrong-agent-key", payloadJson);
        Assert.DoesNotContain(AgentAuthConstants.HeaderName, payloadJson);
        Assert.DoesNotContain("Cookie", payloadJson);
        Assert.DoesNotContain("cookie", payloadJson);
        Assert.DoesNotContain("Investigate production incident", payloadJson);
        Assert.DoesNotContain("tail logs", payloadJson);
    }

    [Fact]
    public async Task ValidAgentAuthenticationDoesNotWriteFailedAuthenticationAudit()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/access-requests",
            CreateValidRequest(),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await AssertSystemAuditEventCountAsync(factory, "AgentAuthenticationFailed", 0);
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
    public async Task Should_ExecuteSshAction_When_TargetAndProfileAreApproved()
    {
        var policy = new FakeSshActionPolicy();
        var executor = new FakeSshCommandExecutor();
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory(
            configureServices: services =>
            {
                services.RemoveAll<ISshActionPolicy>();
                services.RemoveAll<ISshCommandExecutor>();
                services.AddSingleton<ISshActionPolicy>(policy);
                services.AddSingleton<ISshCommandExecutor>(executor);
            }
        );
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid accessRequestId = await CreateAccessRequestAsync(client, ["ssh.read"]);
        Guid sessionId = await ApproveAccessRequestAsync(client, accessRequestId);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/sessions/{sessionId}/actions",
            new
            {
                target = "prod-api",
                action = "logs.tail",
                parameters = new { lines = "100" },
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Equal(sessionId, document.RootElement.GetProperty("sessionId").GetGuid());
        Assert.Equal("logs.tail", document.RootElement.GetProperty("capability").GetString());
        Assert.Equal("prod-api", document.RootElement.GetProperty("target").GetString());
        Assert.Equal("logs.tail", document.RootElement.GetProperty("action").GetString());
        Assert.Equal(
            0,
            document.RootElement.GetProperty("result").GetProperty("exitCode").GetInt32()
        );
        Assert.Equal(
            "secret stdout",
            document.RootElement.GetProperty("result").GetProperty("stdout").GetString()
        );
        JsonElement auditPayload = await GetSingleAuditPayloadAsync(
            factory,
            sessionId,
            "SessionActionExecuted"
        );
        Assert.Equal("prod-api", auditPayload.GetProperty("TargetAlias").GetString());
        Assert.Equal("logs.tail", auditPayload.GetProperty("Action").GetString());
        AssertNoLegacySshAuditFields(auditPayload);
        Assert.Equal(
            "100",
            auditPayload.GetProperty("SafeParameters").GetProperty("lines").GetString()
        );
        Assert.Equal(0, auditPayload.GetProperty("ExitStatus").GetInt32());
        Assert.Equal(JsonValueKind.Number, auditPayload.GetProperty("DurationMs").ValueKind);
        Assert.False(auditPayload.GetProperty("TimedOut").GetBoolean());
        Assert.True(auditPayload.GetProperty("StdoutTruncated").GetBoolean());
        Assert.False(auditPayload.GetProperty("StderrTruncated").GetBoolean());
        Assert.Equal(13, auditPayload.GetProperty("Output").GetProperty("StdoutBytes").GetInt32());
        Assert.Equal(13, auditPayload.GetProperty("Output").GetProperty("StderrBytes").GetInt32());
        Assert.Equal("none", auditPayload.GetProperty("ReasonCode").GetString());
        string auditPayloadJson = auditPayload.GetRawText();
        Assert.DoesNotContain("secret stdout", auditPayloadJson);
        Assert.DoesNotContain("secret stderr", auditPayloadJson);
        Assert.DoesNotContain("/var/log/app.log", auditPayloadJson);
        JsonElement allowedAuditPayload = await GetSingleAuditPayloadAsync(
            factory,
            sessionId,
            "SessionActionAllowed"
        );
        Assert.Equal("prod-api", allowedAuditPayload.GetProperty("TargetAlias").GetString());
        Assert.Equal("logs.tail", allowedAuditPayload.GetProperty("Action").GetString());
        AssertNoLegacySshAuditFields(allowedAuditPayload);
        Assert.Equal(
            "100",
            allowedAuditPayload.GetProperty("SafeParameters").GetProperty("lines").GetString()
        );
        Assert.Equal("none", allowedAuditPayload.GetProperty("ReasonCode").GetString());
        Assert.Equal(JsonValueKind.Null, allowedAuditPayload.GetProperty("ExitStatus").ValueKind);
        Assert.Equal(JsonValueKind.Null, allowedAuditPayload.GetProperty("DurationMs").ValueKind);
        Assert.Equal(JsonValueKind.Null, allowedAuditPayload.GetProperty("Output").ValueKind);
        Assert.Equal(1, policy.ResolveCount);
        Assert.Equal(1, executor.ExecuteCount);
        Assert.Equal(1, await GetSessionActionCountAsync(factory, sessionId));
        await AssertAuditEventExistsAsync(factory, sessionId, "SessionActionAllowed");
        await AssertAuditEventExistsAsync(factory, sessionId, "SessionActionExecuted");
    }

    [Fact]
    public async Task Should_ExecuteMutatingSshAction_When_MaintenanceProfileIsApproved()
    {
        var policy = new FakeSshActionPolicy();
        var executor = new FakeSshCommandExecutor();
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory(
            configureServices: services =>
            {
                services.RemoveAll<ISshActionPolicy>();
                services.RemoveAll<ISshCommandExecutor>();
                services.AddSingleton<ISshActionPolicy>(policy);
                services.AddSingleton<ISshCommandExecutor>(executor);
            }
        );
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/v1/access-requests",
            new
            {
                intent = "Restart demo app",
                requester = "agent-1",
                targets = new[] { "demo-ssh" },
                requestedCapabilities = new[] { "remote.maintenance.basic" },
                durationMinutes = 15,
                risk = "High",
                justification = "Need to recover the demo app.",
                proposedActions = new[] { "Restart demo app" },
                forbiddenActions = new[] { "Restart other services" },
                metadata = new Dictionary<string, string> { ["ticket"] = "MAINT-1" },
            },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        using JsonDocument createDocument = await JsonDocument.ParseAsync(
            await createResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken
        );
        Guid accessRequestId = createDocument.RootElement.GetProperty("id").GetGuid();
        Guid sessionId = await ApproveAccessRequestAsync(client, accessRequestId);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/sessions/{sessionId}/actions",
            new
            {
                target = "demo-ssh",
                action = "service.restart",
                parameters = new { service = "demo-app" },
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Equal(sessionId, document.RootElement.GetProperty("sessionId").GetGuid());
        Assert.Equal("service.restart", document.RootElement.GetProperty("capability").GetString());
        Assert.Equal("demo-ssh", document.RootElement.GetProperty("target").GetString());
        Assert.Equal("service.restart", document.RootElement.GetProperty("action").GetString());
        Assert.True(
            document.RootElement.GetProperty("result").GetProperty("isMutating").GetBoolean()
        );
        Assert.Equal(
            "High",
            document.RootElement.GetProperty("result").GetProperty("risk").GetString()
        );
        Assert.Equal(
            0,
            document.RootElement.GetProperty("result").GetProperty("exitCode").GetInt32()
        );

        JsonElement executedAuditPayload = await GetSingleAuditPayloadAsync(
            factory,
            sessionId,
            "SessionActionExecuted"
        );
        Assert.Equal("demo-ssh", executedAuditPayload.GetProperty("TargetAlias").GetString());
        Assert.Equal("service.restart", executedAuditPayload.GetProperty("Action").GetString());
        Assert.True(executedAuditPayload.GetProperty("IsMutating").GetBoolean());
        Assert.Equal("High", executedAuditPayload.GetProperty("Risk").GetString());
        Assert.Equal(
            "demo-app",
            executedAuditPayload.GetProperty("SafeParameters").GetProperty("service").GetString()
        );

        JsonElement allowedAuditPayload = await GetSingleAuditPayloadAsync(
            factory,
            sessionId,
            "SessionActionAllowed"
        );
        Assert.Equal("demo-ssh", allowedAuditPayload.GetProperty("TargetAlias").GetString());
        Assert.Equal("service.restart", allowedAuditPayload.GetProperty("Action").GetString());
        Assert.True(allowedAuditPayload.GetProperty("IsMutating").GetBoolean());
        Assert.Equal("High", allowedAuditPayload.GetProperty("Risk").GetString());
        Assert.Equal(
            "demo-app",
            allowedAuditPayload.GetProperty("SafeParameters").GetProperty("service").GetString()
        );
        Assert.Equal(1, policy.ResolveCount);
        Assert.Equal(1, executor.ExecuteCount);
    }

    [Fact]
    public async Task Should_ExecuteReloadAction_AndPersistMutatingAuditMetadata_When_SshMaintenanceGrantIsApproved()
    {
        var policy = new FakeSshActionPolicy();
        var executor = new FakeSshCommandExecutor();
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory(
            configureServices: services =>
            {
                services.RemoveAll<ISshActionPolicy>();
                services.RemoveAll<ISshCommandExecutor>();
                services.AddSingleton<ISshActionPolicy>(policy);
                services.AddSingleton<ISshCommandExecutor>(executor);
            }
        );
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/v1/access-requests",
            new
            {
                intent = "Reload demo app",
                requester = "agent-1",
                targets = new[] { "demo-ssh" },
                requestedCapabilities = new[] { "remote.maintenance.basic" },
                durationMinutes = 15,
                risk = "High",
                justification = "Need to reload the demo app configuration.",
                proposedActions = new[] { "Reload demo app" },
                forbiddenActions = new[] { "Restart other services" },
                metadata = new Dictionary<string, string> { ["ticket"] = "MAINT-2" },
            },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        using JsonDocument createDocument = await JsonDocument.ParseAsync(
            await createResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken
        );
        Guid accessRequestId = createDocument.RootElement.GetProperty("id").GetGuid();
        Guid sessionId = await ApproveAccessRequestAsync(client, accessRequestId);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/sessions/{sessionId}/actions",
            new
            {
                target = "demo-ssh",
                action = "service.reload",
                parameters = new { service = "demo-app" },
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Equal(sessionId, document.RootElement.GetProperty("sessionId").GetGuid());
        Assert.Equal("service.reload", document.RootElement.GetProperty("capability").GetString());
        Assert.Equal("demo-ssh", document.RootElement.GetProperty("target").GetString());
        Assert.Equal("service.reload", document.RootElement.GetProperty("action").GetString());
        Assert.NotEqual("service.restart", document.RootElement.GetProperty("action").GetString());
        Assert.True(
            document.RootElement.GetProperty("result").GetProperty("isMutating").GetBoolean()
        );
        Assert.Equal(
            "High",
            document.RootElement.GetProperty("result").GetProperty("risk").GetString()
        );
        Assert.Equal(
            0,
            document.RootElement.GetProperty("result").GetProperty("exitCode").GetInt32()
        );

        JsonElement executedAuditPayload = await GetSingleAuditPayloadAsync(
            factory,
            sessionId,
            "SessionActionExecuted"
        );
        Assert.Equal("demo-ssh", executedAuditPayload.GetProperty("TargetAlias").GetString());
        Assert.Equal("service.reload", executedAuditPayload.GetProperty("Action").GetString());
        Assert.NotEqual("service.restart", executedAuditPayload.GetProperty("Action").GetString());
        Assert.True(executedAuditPayload.GetProperty("IsMutating").GetBoolean());
        Assert.Equal("High", executedAuditPayload.GetProperty("Risk").GetString());
        Assert.Equal(
            "demo-app",
            executedAuditPayload.GetProperty("SafeParameters").GetProperty("service").GetString()
        );

        JsonElement allowedAuditPayload = await GetSingleAuditPayloadAsync(
            factory,
            sessionId,
            "SessionActionAllowed"
        );
        Assert.Equal("demo-ssh", allowedAuditPayload.GetProperty("TargetAlias").GetString());
        Assert.Equal("service.reload", allowedAuditPayload.GetProperty("Action").GetString());
        Assert.True(allowedAuditPayload.GetProperty("IsMutating").GetBoolean());
        Assert.Equal("High", allowedAuditPayload.GetProperty("Risk").GetString());
        Assert.Equal(
            "demo-app",
            allowedAuditPayload.GetProperty("SafeParameters").GetProperty("service").GetString()
        );
        Assert.Equal(1, policy.ResolveCount);
        Assert.Equal(1, executor.ExecuteCount);
    }

    [Fact]
    public async Task Should_ExecuteBackupTrigger_AndPersistMutatingAuditMetadata_WithDistinctJobSemantics()
    {
        var policy = new FakeSshActionPolicy();
        var executor = new FakeSshCommandExecutor();
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory(
            configureServices: services =>
            {
                services.RemoveAll<ISshActionPolicy>();
                services.RemoveAll<ISshCommandExecutor>();
                services.AddSingleton<ISshActionPolicy>(policy);
                services.AddSingleton<ISshCommandExecutor>(executor);
            }
        );
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/v1/access-requests",
            new
            {
                intent = "Trigger nightly config backup",
                requester = "agent-1",
                targets = new[] { "demo-ssh" },
                requestedCapabilities = new[] { "remote.maintenance.basic" },
                durationMinutes = 15,
                risk = "High",
                justification = "Need a bounded maintenance backup run.",
                proposedActions = new[] { "Trigger nightly-config backup" },
                forbiddenActions = new[] { "Restart services" },
                metadata = new Dictionary<string, string> { ["ticket"] = "MAINT-3" },
            },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        using JsonDocument createDocument = await JsonDocument.ParseAsync(
            await createResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken
        );
        Guid accessRequestId = createDocument.RootElement.GetProperty("id").GetGuid();
        Guid sessionId = await ApproveAccessRequestAsync(client, accessRequestId);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/sessions/{sessionId}/actions",
            new
            {
                target = "demo-ssh",
                action = "backup.trigger",
                parameters = new { job = "nightly-config" },
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Equal(sessionId, document.RootElement.GetProperty("sessionId").GetGuid());
        Assert.Equal("backup.trigger", document.RootElement.GetProperty("capability").GetString());
        Assert.Equal("demo-ssh", document.RootElement.GetProperty("target").GetString());
        Assert.Equal("backup.trigger", document.RootElement.GetProperty("action").GetString());
        Assert.NotEqual("service.reload", document.RootElement.GetProperty("action").GetString());
        Assert.NotEqual("service.restart", document.RootElement.GetProperty("action").GetString());
        Assert.True(
            document.RootElement.GetProperty("result").GetProperty("isMutating").GetBoolean()
        );
        Assert.Equal(
            "High",
            document.RootElement.GetProperty("result").GetProperty("risk").GetString()
        );
        Assert.Equal(
            0,
            document.RootElement.GetProperty("result").GetProperty("exitCode").GetInt32()
        );

        JsonElement executedAuditPayload = await GetSingleAuditPayloadAsync(
            factory,
            sessionId,
            "SessionActionExecuted"
        );
        Assert.Equal("demo-ssh", executedAuditPayload.GetProperty("TargetAlias").GetString());
        Assert.Equal("backup.trigger", executedAuditPayload.GetProperty("Action").GetString());
        Assert.True(executedAuditPayload.GetProperty("IsMutating").GetBoolean());
        Assert.Equal("High", executedAuditPayload.GetProperty("Risk").GetString());
        Assert.Equal(
            "nightly-config",
            executedAuditPayload.GetProperty("SafeParameters").GetProperty("job").GetString()
        );
        Assert.False(
            executedAuditPayload.GetProperty("SafeParameters").TryGetProperty("service", out _)
        );

        JsonElement allowedAuditPayload = await GetSingleAuditPayloadAsync(
            factory,
            sessionId,
            "SessionActionAllowed"
        );
        Assert.Equal("demo-ssh", allowedAuditPayload.GetProperty("TargetAlias").GetString());
        Assert.Equal("backup.trigger", allowedAuditPayload.GetProperty("Action").GetString());
        Assert.True(allowedAuditPayload.GetProperty("IsMutating").GetBoolean());
        Assert.Equal("High", allowedAuditPayload.GetProperty("Risk").GetString());
        Assert.Equal(
            "nightly-config",
            allowedAuditPayload.GetProperty("SafeParameters").GetProperty("job").GetString()
        );
        Assert.False(
            allowedAuditPayload.GetProperty("SafeParameters").TryGetProperty("service", out _)
        );
        Assert.Equal(1, policy.ResolveCount);
        Assert.Equal(1, executor.ExecuteCount);
    }

    [Fact]
    public async Task Should_ReturnForbidden_When_SshGrantDoesNotApplyToRequestedTarget()
    {
        var policy = new FakeSshActionPolicy();
        var executor = new FakeSshCommandExecutor();
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory(
            configureServices: services =>
            {
                services.RemoveAll<ISshActionPolicy>();
                services.RemoveAll<ISshCommandExecutor>();
                services.AddSingleton<ISshActionPolicy>(policy);
                services.AddSingleton<ISshCommandExecutor>(executor);
            }
        );
        await factory.MigrateAsync(TestContext.Current.CancellationToken);

        Session seededSession = Session.Load(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SessionStatus.Active,
            ["prod-api", "prod-db"],
            ["ssh.read"],
            [new SshProfileGrant("prod-api", "ssh.read")],
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(30),
            0,
            Session.DefaultMaxActionCount,
            null,
            null,
            null
        );

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            GatekeeperDbContext dbContext =
                scope.ServiceProvider.GetRequiredService<GatekeeperDbContext>();
            await new EfSessionRepository(dbContext).AddAsync(
                seededSession,
                TestContext.Current.CancellationToken
            );
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/sessions/{seededSession.Id}/actions",
            new
            {
                target = "prod-db",
                action = "logs.tail",
                parameters = new { lines = "100" },
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1, policy.ResolveCount);
        Assert.Equal(0, executor.ExecuteCount);
        Assert.Equal(0, await GetSessionActionCountAsync(factory, seededSession.Id));

        JsonElement auditPayload = await GetSingleAuditPayloadAsync(
            factory,
            seededSession.Id,
            "SessionActionDenied"
        );
        Assert.Equal("prod-db", auditPayload.GetProperty("TargetAlias").GetString());
        Assert.Equal("logs.tail", auditPayload.GetProperty("Action").GetString());
        Assert.Equal("profile_not_allowed", auditPayload.GetProperty("ReasonCode").GetString());
        AssertNoLegacySshAuditFields(auditPayload);
    }

    [Fact]
    public async Task Should_ReturnForbidden_When_SshTargetIsNotApproved()
    {
        var executor = new FakeSshCommandExecutor();
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory(
            configureServices: services =>
            {
                services.RemoveAll<ISshActionPolicy>();
                services.RemoveAll<ISshCommandExecutor>();
                services.AddSingleton<ISshActionPolicy>(new FakeSshActionPolicy());
                services.AddSingleton<ISshCommandExecutor>(executor);
            }
        );
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid accessRequestId = await CreateAccessRequestAsync(client, ["ssh.read"]);
        Guid sessionId = await ApproveAccessRequestAsync(client, accessRequestId);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/sessions/{sessionId}/actions",
            new
            {
                target = "prod-db",
                action = "logs.tail",
                parameters = new { lines = "100" },
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, executor.ExecuteCount);
        Assert.Equal(0, await GetSessionActionCountAsync(factory, sessionId));
        JsonElement auditPayload = await GetSingleAuditPayloadAsync(
            factory,
            sessionId,
            "SessionActionDenied"
        );
        Assert.Equal("prod-db", auditPayload.GetProperty("TargetAlias").GetString());
        Assert.Equal("logs.tail", auditPayload.GetProperty("Action").GetString());
        AssertNoLegacySshAuditFields(auditPayload);
        Assert.Equal("target_not_allowed", auditPayload.GetProperty("ReasonCode").GetString());
        await AssertAuditEventExistsAsync(factory, sessionId, "SessionActionDenied");
    }

    [Fact]
    public async Task Should_ReturnForbidden_When_SshActionIsNotIncludedInApprovedProfile()
    {
        var executor = new FakeSshCommandExecutor();
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory(
            configureServices: services =>
            {
                services.RemoveAll<ISshActionPolicy>();
                services.RemoveAll<ISshCommandExecutor>();
                services.AddSingleton<ISshActionPolicy>(new FakeSshActionPolicy());
                services.AddSingleton<ISshCommandExecutor>(executor);
            }
        );
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid accessRequestId = await CreateAccessRequestAsync(client, ["ssh.write"]);
        Guid sessionId = await ApproveAccessRequestAsync(client, accessRequestId);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/sessions/{sessionId}/actions",
            new
            {
                target = "prod-api",
                action = "logs.tail",
                parameters = new { lines = "100" },
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, executor.ExecuteCount);
        Assert.Equal(0, await GetSessionActionCountAsync(factory, sessionId));
        JsonElement auditPayload = await GetSingleAuditPayloadAsync(
            factory,
            sessionId,
            "SessionActionDenied"
        );
        Assert.Equal("prod-api", auditPayload.GetProperty("TargetAlias").GetString());
        Assert.Equal("logs.tail", auditPayload.GetProperty("Action").GetString());
        AssertNoLegacySshAuditFields(auditPayload);
        Assert.Equal("profile_not_allowed", auditPayload.GetProperty("ReasonCode").GetString());
        await AssertAuditEventExistsAsync(factory, sessionId, "SessionActionDenied");
    }

    [Fact]
    public async Task Should_ReturnBadRequest_When_SshActionParametersAreInvalid()
    {
        var executor = new FakeSshCommandExecutor();
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory(
            configureServices: services =>
            {
                services.RemoveAll<ISshActionPolicy>();
                services.RemoveAll<ISshCommandExecutor>();
                services.AddSingleton<ISshActionPolicy>(new FakeSshActionPolicy());
                services.AddSingleton<ISshCommandExecutor>(executor);
            }
        );
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid accessRequestId = await CreateAccessRequestAsync(client, ["ssh.read"]);
        Guid sessionId = await ApproveAccessRequestAsync(client, accessRequestId);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/sessions/{sessionId}/actions",
            new
            {
                target = "prod-api",
                action = "logs.tail",
                parameters = new { lines = "invalid" },
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, executor.ExecuteCount);
        Assert.Equal(0, await GetSessionActionCountAsync(factory, sessionId));
        JsonElement auditPayload = await GetSingleAuditPayloadAsync(
            factory,
            sessionId,
            "SessionActionDenied"
        );
        Assert.Equal("prod-api", auditPayload.GetProperty("TargetAlias").GetString());
        Assert.Equal("logs.tail", auditPayload.GetProperty("Action").GetString());
        AssertNoLegacySshAuditFields(auditPayload);
        Assert.Equal("invalid_parameter", auditPayload.GetProperty("ReasonCode").GetString());
    }

    [Fact]
    public async Task Should_ReturnForbidden_When_SshActionIsUnknown()
    {
        var executor = new FakeSshCommandExecutor();
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory(
            configureServices: services =>
            {
                services.RemoveAll<ISshActionPolicy>();
                services.RemoveAll<ISshCommandExecutor>();
                services.AddSingleton<ISshActionPolicy>(new FakeSshActionPolicy());
                services.AddSingleton<ISshCommandExecutor>(executor);
            }
        );
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid accessRequestId = await CreateAccessRequestAsync(client, ["ssh.read"]);
        Guid sessionId = await ApproveAccessRequestAsync(client, accessRequestId);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/sessions/{sessionId}/actions",
            new
            {
                target = "prod-api",
                action = "unknown",
                parameters = new { },
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, executor.ExecuteCount);
        Assert.Equal(0, await GetSessionActionCountAsync(factory, sessionId));
        JsonElement auditPayload = await GetSingleAuditPayloadAsync(
            factory,
            sessionId,
            "SessionActionDenied"
        );
        Assert.Equal("prod-api", auditPayload.GetProperty("TargetAlias").GetString());
        Assert.Equal("unknown", auditPayload.GetProperty("Action").GetString());
        AssertNoLegacySshAuditFields(auditPayload);
        Assert.Equal("action_not_allowed", auditPayload.GetProperty("ReasonCode").GetString());
        await AssertAuditEventExistsAsync(factory, sessionId, "SessionActionDenied");
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
    public async Task Should_ReturnConflict_When_CompletedSessionReceivesSshAction()
    {
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid accessRequestId = await CreateAccessRequestAsync(client, ["ssh.read"]);
        Guid sessionId = await ApproveAccessRequestAsync(client, accessRequestId);

        using HttpResponseMessage completeResponse = await client.PostAsync(
            $"/api/v1/sessions/{sessionId}/complete",
            content: null,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/sessions/{sessionId}/actions",
            new
            {
                target = "prod-api",
                action = "logs.tail",
                parameters = new { lines = "100" },
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        JsonElement auditPayload = await GetSingleAuditPayloadAsync(
            factory,
            sessionId,
            "SessionActionDenied"
        );
        Assert.Equal("prod-api", auditPayload.GetProperty("TargetAlias").GetString());
        Assert.Equal("logs.tail", auditPayload.GetProperty("Action").GetString());
        AssertNoLegacySshAuditFields(auditPayload);
        Assert.Equal("session_inactive", auditPayload.GetProperty("ReasonCode").GetString());
        Assert.Equal(0, await GetSessionActionCountAsync(factory, sessionId));
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
    public async Task Should_ReturnConflict_When_ExpiredSessionReceivesSshAction()
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
            new
            {
                target = "prod-api",
                action = "logs.tail",
                parameters = new { lines = "100" },
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        JsonElement auditPayload = await GetSingleAuditPayloadAsync(
            factory,
            sessionId,
            "SessionActionDenied"
        );
        Assert.Equal("prod-api", auditPayload.GetProperty("TargetAlias").GetString());
        Assert.Equal("logs.tail", auditPayload.GetProperty("Action").GetString());
        AssertNoLegacySshAuditFields(auditPayload);
        Assert.Equal("session_expired", auditPayload.GetProperty("ReasonCode").GetString());
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
    public async Task Should_ReturnConflictAndAuditReasonCode_When_SshActionCountLimitIsExceeded()
    {
        var executor = new FakeSshCommandExecutor();
        await using AccessRequestApiFactory factory = new AccessRequestApiFactory(
            maxActionCount: 1,
            configureServices: services =>
            {
                services.RemoveAll<ISshActionPolicy>();
                services.RemoveAll<ISshCommandExecutor>();
                services.AddSingleton<ISshActionPolicy>(new FakeSshActionPolicy());
                services.AddSingleton<ISshCommandExecutor>(executor);
            }
        );
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        Guid accessRequestId = await CreateAccessRequestAsync(client, ["ssh.read"]);
        Guid sessionId = await ApproveAccessRequestAsync(client, accessRequestId);
        var request = new
        {
            target = "prod-api",
            action = "logs.tail",
            parameters = new { lines = "100" },
        };

        using HttpResponseMessage firstResponse = await client.PostAsJsonAsync(
            $"/api/v1/sessions/{sessionId}/actions",
            request,
            TestContext.Current.CancellationToken
        );
        using HttpResponseMessage secondResponse = await client.PostAsJsonAsync(
            $"/api/v1/sessions/{sessionId}/actions",
            request,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        Assert.Equal(1, executor.ExecuteCount);
        Assert.Equal(1, await GetSessionActionCountAsync(factory, sessionId));
        JsonElement auditPayload = await GetSingleAuditPayloadAsync(
            factory,
            sessionId,
            "ActionCountExceeded"
        );
        Assert.Equal("action_count_exceeded", auditPayload.GetProperty("ReasonCode").GetString());
        Assert.Equal("prod-api", auditPayload.GetProperty("TargetAlias").GetString());
        Assert.Equal("logs.tail", auditPayload.GetProperty("Action").GetString());
        AssertNoLegacySshAuditFields(auditPayload);
        Assert.Equal(
            "100",
            auditPayload.GetProperty("SafeParameters").GetProperty("lines").GetString()
        );
        string auditPayloadJson = auditPayload.GetRawText();
        Assert.DoesNotContain("secret stdout", auditPayloadJson);
        Assert.DoesNotContain("secret stderr", auditPayloadJson);
        Assert.DoesNotContain("/var/log/app.log", auditPayloadJson);
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

    private static async Task AssertAuditEventCountAsync(
        AccessRequestApiFactory factory,
        int expectedCount
    )
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        GatekeeperDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<GatekeeperDbContext>();
        int auditCount = await dbContext.AuditEvents.CountAsync(
            TestContext.Current.CancellationToken
        );
        Assert.Equal(expectedCount, auditCount);
    }

    private static async Task AssertSystemAuditEventCountAsync(
        AccessRequestApiFactory factory,
        string eventType,
        int expectedCount
    )
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        GatekeeperDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<GatekeeperDbContext>();
        int auditCount = await dbContext.AuditEvents.CountAsync(
            auditEvent => auditEvent.AggregateId == null && auditEvent.EventType == eventType,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(expectedCount, auditCount);
    }

    private static async Task AssertFailedAgentAuthenticationAuditAsync(
        AccessRequestApiFactory factory,
        string routeTemplate,
        string httpMethod,
        string reasonCode
    )
    {
        await AssertSystemAuditEventCountAsync(factory, "AgentAuthenticationFailed", 1);
        JsonElement payload = await GetSingleSystemAuditPayloadAsync(
            factory,
            "AgentAuthenticationFailed"
        );
        Assert.Equal(routeTemplate, payload.GetProperty("routeTemplate").GetString());
        Assert.Equal(httpMethod, payload.GetProperty("httpMethod").GetString());
        Assert.Equal(reasonCode, payload.GetProperty("reasonCode").GetString());
        Assert.Equal(
            AgentAuthConstants.ApiKeyAuthMethod,
            payload.GetProperty("authMethod").GetString()
        );
    }

    private static async Task<JsonElement> GetSingleAuditPayloadAsync(
        AccessRequestApiFactory factory,
        Guid aggregateId,
        string eventType
    )
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        GatekeeperDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<GatekeeperDbContext>();
        string payloadJson = await dbContext
            .AuditEvents.Where(auditEvent =>
                auditEvent.AggregateId == aggregateId && auditEvent.EventType == eventType
            )
            .Select(auditEvent => auditEvent.PayloadJson)
            .SingleAsync(TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(payloadJson);
        return document.RootElement.Clone();
    }

    private static async Task<JsonElement> GetSingleSystemAuditPayloadAsync(
        AccessRequestApiFactory factory,
        string eventType
    )
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        GatekeeperDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<GatekeeperDbContext>();
        string payloadJson = await dbContext
            .AuditEvents.Where(auditEvent =>
                auditEvent.AggregateId == null && auditEvent.EventType == eventType
            )
            .Select(auditEvent => auditEvent.PayloadJson)
            .SingleAsync(TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(payloadJson);
        return document.RootElement.Clone();
    }

    private static void AssertNoLegacySshAuditFields(JsonElement auditPayload)
    {
        Assert.False(auditPayload.TryGetProperty("Target", out _));
        Assert.False(auditPayload.TryGetProperty("Capability", out _));
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

    private sealed class FakeSshActionPolicy : ISshActionPolicy
    {
        public int ResolveCount { get; private set; }

        public SshActionPolicyResult Resolve(
            string targetAlias,
            string actionName,
            IReadOnlyCollection<SshApprovedProfileGrant> approvedProfileGrants,
            JsonElement? parameters
        )
        {
            ResolveCount++;

            if (
                (
                    string.Equals(targetAlias, "prod-api", StringComparison.Ordinal)
                    || string.Equals(targetAlias, "prod-db", StringComparison.Ordinal)
                ) && string.Equals(actionName, "logs.tail", StringComparison.Ordinal)
            )
            {
                bool hasApprovedGrant = approvedProfileGrants.Any(grant =>
                    string.Equals(grant.TargetAlias, targetAlias, StringComparison.Ordinal)
                    && string.Equals(grant.ProfileName, "ssh.read", StringComparison.Ordinal)
                );
                if (!hasApprovedGrant)
                {
                    return SshActionPolicyResult.Failed(
                        SshActionPolicyFailureReason.MissingProfileMembership,
                        "No approved SSH profile permits the requested action."
                    );
                }

                if (
                    parameters.HasValue
                    && parameters.Value.TryGetProperty("lines", out JsonElement lines)
                    && string.Equals(lines.GetString(), "invalid", StringComparison.Ordinal)
                )
                {
                    return SshActionPolicyResult.Failed(
                        SshActionPolicyFailureReason.InvalidParameter,
                        "Invalid SSH action parameter."
                    );
                }

                return SshActionPolicyResult.Success(
                    new SshResolvedAction(
                        targetAlias,
                        actionName,
                        ["tail", "-n", "100", "/var/log/app.log"],
                        new Dictionary<string, string> { ["lines"] = "100" },
                        TimeSpan.FromSeconds(5),
                        4096,
                        false,
                        RiskLevel.Low,
                        host: "demo-ssh",
                        port: 22,
                        username: "gatekeeper-readonly",
                        privateKeyPath: "/run/secrets/demo-key",
                        knownHostsPath: "/app/config/known_hosts"
                    )
                );
            }

            if (
                string.Equals(targetAlias, "demo-ssh", StringComparison.Ordinal)
                && (
                    string.Equals(actionName, "service.restart", StringComparison.Ordinal)
                    || string.Equals(actionName, "service.reload", StringComparison.Ordinal)
                    || string.Equals(actionName, "backup.trigger", StringComparison.Ordinal)
                )
            )
            {
                bool hasApprovedGrant = approvedProfileGrants.Any(grant =>
                    string.Equals(grant.TargetAlias, "demo-ssh", StringComparison.Ordinal)
                    && string.Equals(
                        grant.ProfileName,
                        "remote.maintenance.basic",
                        StringComparison.Ordinal
                    )
                );
                if (!hasApprovedGrant)
                {
                    return SshActionPolicyResult.Failed(
                        SshActionPolicyFailureReason.MissingProfileMembership,
                        "No approved SSH profile permits the requested action."
                    );
                }

                if (string.Equals(actionName, "backup.trigger", StringComparison.Ordinal))
                {
                    if (
                        !parameters.HasValue
                        || !parameters.Value.TryGetProperty("job", out JsonElement job)
                        || !string.Equals(
                            job.GetString(),
                            "nightly-config",
                            StringComparison.Ordinal
                        )
                    )
                    {
                        return SshActionPolicyResult.Failed(
                            SshActionPolicyFailureReason.InvalidParameter,
                            "Invalid SSH action parameter."
                        );
                    }

                    return SshActionPolicyResult.Success(
                        new SshResolvedAction(
                            targetAlias,
                            actionName,
                            ["backup-job", "trigger", "nightly-config"],
                            new Dictionary<string, string> { ["job"] = "nightly-config" },
                            TimeSpan.FromSeconds(15),
                            4096,
                            true,
                            RiskLevel.High,
                            host: "demo-ssh",
                            port: 22,
                            username: "gatekeeper-readonly",
                            privateKeyPath: "/run/secrets/demo-key",
                            knownHostsPath: "/app/config/known_hosts"
                        )
                    );
                }

                if (
                    !parameters.HasValue
                    || !parameters.Value.TryGetProperty("service", out JsonElement service)
                    || !string.Equals(service.GetString(), "demo-app", StringComparison.Ordinal)
                )
                {
                    return SshActionPolicyResult.Failed(
                        SshActionPolicyFailureReason.InvalidParameter,
                        "Invalid SSH action parameter."
                    );
                }

                string verb = string.Equals(actionName, "service.reload", StringComparison.Ordinal)
                    ? "reload"
                    : "restart";

                return SshActionPolicyResult.Success(
                    new SshResolvedAction(
                        targetAlias,
                        actionName,
                        ["systemctl", verb, "demo-app"],
                        new Dictionary<string, string> { ["service"] = "demo-app" },
                        TimeSpan.FromSeconds(15),
                        4096,
                        true,
                        RiskLevel.High,
                        host: "demo-ssh",
                        port: 22,
                        username: "gatekeeper-readonly",
                        privateKeyPath: "/run/secrets/demo-key",
                        knownHostsPath: "/app/config/known_hosts"
                    )
                );
            }

            if (
                string.Equals(targetAlias, "prod-api", StringComparison.Ordinal)
                || string.Equals(targetAlias, "demo-ssh", StringComparison.Ordinal)
            )
            {
                return SshActionPolicyResult.Failed(
                    SshActionPolicyFailureReason.UnknownAction,
                    "Unknown SSH action."
                );
            }

            return SshActionPolicyResult.Failed(
                SshActionPolicyFailureReason.UnknownTarget,
                "Unknown SSH target."
            );
        }
    }

    private sealed class FakeSshCommandExecutor : ISshCommandExecutor
    {
        public int ExecuteCount { get; private set; }

        public Task<SshCommandExecutionResult> ExecuteAsync(
            SshResolvedAction resolvedAction,
            CancellationToken cancellationToken
        )
        {
            ExecuteCount++;
            return Task.FromResult(
                SshCommandExecutionResult.Completed(
                    new SshCommandOutput(0, "secret stdout", "secret stderr", true, false)
                )
            );
        }
    }

    private sealed class ThrowingAuditEventRepository : IAuditEventRepository
    {
        public Task AddAsync(
            Gatekeeper.Core.AccessRequests.AuditEvent auditEvent,
            CancellationToken cancellationToken
        )
        {
            throw new InvalidOperationException("Simulated audit repository failure.");
        }
    }

    private sealed class AccessRequestApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databasePath;
        private readonly Action<IServiceCollection>? _configureServices;

        public AccessRequestApiFactory(
            string? adminToken = "test-admin-token",
            int? maxActionCount = null,
            Action<IServiceCollection>? configureServices = null
        )
        {
            _configureServices = configureServices;
            _databasePath = Path.Combine(Path.GetTempPath(), $"gatekeeper-{Guid.NewGuid():N}.db");
            Environment.SetEnvironmentVariable("GATEKEEPER_SQLITE_DATA_PATH", _databasePath);
            Environment.SetEnvironmentVariable("GATEKEEPER_ADMIN_TOKEN", adminToken);
            Environment.SetEnvironmentVariable("GATEKEEPER_ADMIN_USERNAME", "admin");
            Environment.SetEnvironmentVariable("GATEKEEPER_ADMIN_PASSWORD", "correct-password");
            Environment.SetEnvironmentVariable("GATEKEEPER_ADMIN_COOKIE_SECURE", "false");
            Environment.SetEnvironmentVariable("AgentAuthentication__Enabled", "true");
            Environment.SetEnvironmentVariable(
                "AgentAuthentication__ApiKeys__0__AgentId",
                "agent-1"
            );
            Environment.SetEnvironmentVariable(
                "AgentAuthentication__ApiKeys__0__Key",
                TestAgentKey
            );
            Environment.SetEnvironmentVariable(
                "GATEKEEPER_SESSION_MAX_ACTION_COUNT",
                maxActionCount?.ToString()
            );
            ConfigureCatalogSeedTarget(
                "prod-api",
                host: "prod-api.example.test",
                username: "gatekeeper-readonly"
            );
            ConfigureCatalogSeedTarget(
                "prod-db",
                host: "prod-db.example.test",
                username: "gatekeeper-readonly"
            );
            ConfigureMaintenanceSeedTarget(
                "demo-ssh",
                host: "demo-ssh.example.test",
                username: "gatekeeper-maintenance"
            );
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            if (_configureServices is null)
            {
                return;
            }

            builder.ConfigureServices(_configureServices);
        }

        protected override void ConfigureClient(HttpClient client)
        {
            client.DefaultRequestHeaders.Add(AgentAuthConstants.HeaderName, TestAgentKey);
        }

        public async Task MigrateAsync(CancellationToken cancellationToken)
        {
            await using AsyncServiceScope scope = Services.CreateAsyncScope();
            GatekeeperDbContext dbContext =
                scope.ServiceProvider.GetRequiredService<GatekeeperDbContext>();
            await dbContext.Database.MigrateAsync(cancellationToken);
        }

        private static void ConfigureCatalogSeedTarget(string alias, string host, string username)
        {
            Environment.SetEnvironmentVariable($"SshConnector__Targets__{alias}__Host", host);
            Environment.SetEnvironmentVariable($"SshConnector__Targets__{alias}__Port", "22");
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__Username",
                username
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__PrivateKeyPath",
                $"/run/secrets/{alias}-key"
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__KnownHostsPath",
                $"/run/secrets/{alias}-known-hosts"
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__Profiles__ssh.read__Actions__0",
                "logs.tail"
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__Profiles__ssh.write__Actions__0",
                "logs.tail"
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__Actions__logs.tail__Command__0",
                "tail"
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__Actions__logs.tail__Command__1",
                "-n"
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__Actions__logs.tail__Command__2",
                "100"
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__Actions__logs.tail__Command__3",
                "/var/log/app.log"
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__Actions__logs.tail__AllowedParameters__lines__0",
                "100"
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__Actions__logs.tail__IsMutating",
                "false"
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__Actions__logs.tail__Risk",
                "Low"
            );
        }

        private static void ConfigureMaintenanceSeedTarget(
            string alias,
            string host,
            string username
        )
        {
            Environment.SetEnvironmentVariable($"SshConnector__Targets__{alias}__Host", host);
            Environment.SetEnvironmentVariable($"SshConnector__Targets__{alias}__Port", "22");
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__Username",
                username
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__PrivateKeyPath",
                $"/run/secrets/{alias}-key"
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__KnownHostsPath",
                $"/run/secrets/{alias}-known-hosts"
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__Profiles__remote.maintenance.basic__Actions__0",
                "service.restart"
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__Profiles__remote.maintenance.basic__Actions__1",
                "service.reload"
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__Profiles__remote.maintenance.basic__Actions__2",
                "backup.trigger"
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__Actions__service.restart__Command__0",
                "systemctl"
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__Actions__service.restart__Command__1",
                "restart"
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__Actions__service.restart__IsMutating",
                "true"
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__Actions__service.restart__Risk",
                "High"
            );
        }

        private static void ClearCatalogSeedTarget(string alias)
        {
            Environment.SetEnvironmentVariable($"SshConnector__Targets__{alias}__Host", null);
            Environment.SetEnvironmentVariable($"SshConnector__Targets__{alias}__Port", null);
            Environment.SetEnvironmentVariable($"SshConnector__Targets__{alias}__Username", null);
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__PrivateKeyPath",
                null
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__KnownHostsPath",
                null
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__Profiles__ssh.read__Actions__0",
                null
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__Profiles__ssh.write__Actions__0",
                null
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__Actions__logs.tail__Command__0",
                null
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__Actions__logs.tail__Command__1",
                null
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__Actions__logs.tail__Command__2",
                null
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__Actions__logs.tail__Command__3",
                null
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__Actions__logs.tail__AllowedParameters__lines__0",
                null
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__Actions__logs.tail__IsMutating",
                null
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__Actions__logs.tail__Risk",
                null
            );
        }

        private static void ClearMaintenanceSeedTarget(string alias)
        {
            Environment.SetEnvironmentVariable($"SshConnector__Targets__{alias}__Host", null);
            Environment.SetEnvironmentVariable($"SshConnector__Targets__{alias}__Port", null);
            Environment.SetEnvironmentVariable($"SshConnector__Targets__{alias}__Username", null);
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__PrivateKeyPath",
                null
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__KnownHostsPath",
                null
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__Profiles__remote.maintenance.basic__Actions__0",
                null
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__Profiles__remote.maintenance.basic__Actions__1",
                null
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__Profiles__remote.maintenance.basic__Actions__2",
                null
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__Actions__service.restart__Command__0",
                null
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__Actions__service.restart__Command__1",
                null
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__Actions__service.restart__IsMutating",
                null
            );
            Environment.SetEnvironmentVariable(
                $"SshConnector__Targets__{alias}__Actions__service.restart__Risk",
                null
            );
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Environment.SetEnvironmentVariable("GATEKEEPER_SQLITE_DATA_PATH", null);
            Environment.SetEnvironmentVariable("GATEKEEPER_ADMIN_TOKEN", null);
            Environment.SetEnvironmentVariable("GATEKEEPER_ADMIN_USERNAME", null);
            Environment.SetEnvironmentVariable("GATEKEEPER_ADMIN_PASSWORD", null);
            Environment.SetEnvironmentVariable("GATEKEEPER_ADMIN_COOKIE_SECURE", null);
            Environment.SetEnvironmentVariable("AgentAuthentication__Enabled", null);
            Environment.SetEnvironmentVariable("AgentAuthentication__ApiKeys__0__AgentId", null);
            Environment.SetEnvironmentVariable("AgentAuthentication__ApiKeys__0__Key", null);
            Environment.SetEnvironmentVariable("GATEKEEPER_SESSION_MAX_ACTION_COUNT", null);
            ClearCatalogSeedTarget("prod-api");
            ClearCatalogSeedTarget("prod-db");
            ClearMaintenanceSeedTarget("demo-ssh");
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
    }
}
