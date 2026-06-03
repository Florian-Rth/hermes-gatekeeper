using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gatekeeper.Api.AgentAuthentication;
using Gatekeeper.Application.Sessions;
using Gatekeeper.Core.Sessions;
using Gatekeeper.Infrastructure.Persistence;
using Gatekeeper.Infrastructure.Persistence.Entities;
using Gatekeeper.Infrastructure.Persistence.Repositories;
using Gatekeeper.Infrastructure.SessionActions.Ssh;
using Gatekeeper.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Gatekeeper.Tests;

public sealed class SshRuntimeResolveIntegrationTests
{
    private const string TestAgentKey = "test-agent-key";

    [Fact]
    public async Task ApprovedTargetProfileAndSeededAction_ExecutesViaDbBackedResolvePath()
    {
        var clientDouble = new RecordingSshCommandClient(
            SshCommandClientResult.Completed(0, "linux\n", string.Empty)
        );

        await using PostgresGatekeeperDatabase database = new();
        await database.InitializeAsync(TestContext.Current.CancellationToken);
        await using GatekeeperApiFactory factory = CreateFactory(
            database.ConnectionString,
            clientDouble
        );
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        client.DefaultRequestHeaders.Add(AgentAuthConstants.HeaderName, TestAgentKey);

        Guid sessionId = await SeedApprovedSessionAsync(factory, "remote.readonly.inspect");

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/sessions/{sessionId}/actions",
            new
            {
                target = "demo-ssh",
                action = "service.status.read",
                parameters = new { service = "sshd" },
            },
            TestContext.Current.CancellationToken
        );

        string responseBody = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken
        );
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK but got {(int)response.StatusCode} {response.StatusCode}. Body: {responseBody}"
        );
        using JsonDocument document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Equal("demo-ssh", document.RootElement.GetProperty("target").GetString());
        Assert.Equal("service.status.read", document.RootElement.GetProperty("action").GetString());
        Assert.Equal(
            0,
            document.RootElement.GetProperty("result").GetProperty("exitCode").GetInt32()
        );
        Assert.False(
            document.RootElement.GetProperty("result").GetProperty("isMutating").GetBoolean()
        );
        Assert.Equal(
            "Low",
            document.RootElement.GetProperty("result").GetProperty("risk").GetString()
        );

        Assert.NotNull(clientDouble.LastRequest);
        Assert.Equal("demo-ssh", clientDouble.LastRequest.Host);
        Assert.Equal(22, clientDouble.LastRequest.Port);
        Assert.Equal("gatekeeper-readonly", clientDouble.LastRequest.Username);
        Assert.Equal("pgrep", clientDouble.LastRequest.Executable);
        Assert.Equal(new[] { "-x", "sshd" }, clientDouble.LastRequest.Arguments);

        JsonElement executedPayload = await GetAuditPayloadAsync(
            factory,
            sessionId,
            "SessionActionExecuted"
        );
        Assert.False(executedPayload.GetProperty("IsMutating").GetBoolean());
        Assert.Equal("Low", executedPayload.GetProperty("Risk").GetString());
        Assert.Equal(
            "sshd",
            executedPayload.GetProperty("SafeParameters").GetProperty("service").GetString()
        );
    }

    [Fact]
    public async Task RemovedCatalogAction_FailsClosed()
    {
        var clientDouble = new RecordingSshCommandClient(
            SshCommandClientResult.Completed(0, string.Empty, string.Empty)
        );

        await using PostgresGatekeeperDatabase database = new();
        await database.InitializeAsync(TestContext.Current.CancellationToken);
        await using GatekeeperApiFactory factory = CreateFactory(
            database.ConnectionString,
            clientDouble
        );
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        client.DefaultRequestHeaders.Add(AgentAuthConstants.HeaderName, TestAgentKey);

        Guid sessionId = await SeedApprovedSessionAsync(factory, "remote.readonly.inspect");

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            GatekeeperDbContext dbContext =
                scope.ServiceProvider.GetRequiredService<GatekeeperDbContext>();
            SshActionEntity action = await dbContext.SshActions.SingleAsync(
                candidate => candidate.Name == "service.status.read",
                TestContext.Current.CancellationToken
            );
            dbContext.SshActions.Remove(action);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/sessions/{sessionId}/actions",
            new
            {
                target = "demo-ssh",
                action = "service.status.read",
                parameters = new { service = "sshd" },
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(clientDouble.LastRequest);

        JsonElement deniedPayload = await GetAuditPayloadAsync(
            factory,
            sessionId,
            "SessionActionDenied"
        );
        Assert.Equal("action_not_allowed", deniedPayload.GetProperty("ReasonCode").GetString());
    }

    [Fact]
    public async Task ParameterAllowlist_IsEnforcedFromDatabase()
    {
        var clientDouble = new RecordingSshCommandClient(
            SshCommandClientResult.Completed(0, string.Empty, string.Empty)
        );

        await using PostgresGatekeeperDatabase database = new();
        await database.InitializeAsync(TestContext.Current.CancellationToken);
        await using GatekeeperApiFactory factory = CreateFactory(
            database.ConnectionString,
            clientDouble
        );
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        client.DefaultRequestHeaders.Add(AgentAuthConstants.HeaderName, TestAgentKey);

        Guid sessionId = await SeedApprovedSessionAsync(factory, "remote.readonly.inspect");

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            GatekeeperDbContext dbContext =
                scope.ServiceProvider.GetRequiredService<GatekeeperDbContext>();
            SshActionAllowedParameterValueEntity allowedValue =
                await dbContext.SshActionAllowedParameterValues.SingleAsync(
                    value => value.Value == "sshd",
                    TestContext.Current.CancellationToken
                );
            allowedValue.Value = "nginx";
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using HttpResponseMessage deniedResponse = await client.PostAsJsonAsync(
            $"/api/v1/sessions/{sessionId}/actions",
            new
            {
                target = "demo-ssh",
                action = "service.status.read",
                parameters = new { service = "sshd" },
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.BadRequest, deniedResponse.StatusCode);
        Assert.Null(clientDouble.LastRequest);

        using HttpResponseMessage allowedResponse = await client.PostAsJsonAsync(
            $"/api/v1/sessions/{sessionId}/actions",
            new
            {
                target = "demo-ssh",
                action = "service.status.read",
                parameters = new { service = "nginx" },
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, allowedResponse.StatusCode);
        Assert.NotNull(clientDouble.LastRequest);
        Assert.Equal(new[] { "-x", "nginx" }, clientDouble.LastRequest.Arguments);
    }

    [Fact]
    public async Task MutatingRiskMetadata_ComesFromDatabaseIntoResultAndAudit()
    {
        var clientDouble = new RecordingSshCommandClient(
            SshCommandClientResult.Completed(0, string.Empty, string.Empty)
        );

        await using PostgresGatekeeperDatabase database = new();
        await database.InitializeAsync(TestContext.Current.CancellationToken);
        await using GatekeeperApiFactory factory = CreateFactory(
            database.ConnectionString,
            clientDouble
        );
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        client.DefaultRequestHeaders.Add(AgentAuthConstants.HeaderName, TestAgentKey);

        Guid sessionId = await SeedApprovedSessionAsync(factory, "remote.maintenance.basic");

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

        JsonElement result = document.RootElement.GetProperty("result");
        Assert.True(result.GetProperty("isMutating").GetBoolean());
        Assert.Equal("High", result.GetProperty("risk").GetString());

        JsonElement allowedPayload = await GetAuditPayloadAsync(
            factory,
            sessionId,
            "SessionActionAllowed"
        );
        Assert.True(allowedPayload.GetProperty("IsMutating").GetBoolean());
        Assert.Equal("High", allowedPayload.GetProperty("Risk").GetString());

        JsonElement executedPayload = await GetAuditPayloadAsync(
            factory,
            sessionId,
            "SessionActionExecuted"
        );
        Assert.True(executedPayload.GetProperty("IsMutating").GetBoolean());
        Assert.Equal("High", executedPayload.GetProperty("Risk").GetString());
    }

    private static GatekeeperApiFactory CreateFactory(
        string connectionString,
        RecordingSshCommandClient clientDouble
    )
    {
        return new GatekeeperApiFactory(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                ["ConnectionStrings__Gatekeeper"] = connectionString,
                ["GATEKEEPER_ADMIN_TOKEN"] = "test-admin-token",
                ["GATEKEEPER_ADMIN_USERNAME"] = "admin",
                ["GATEKEEPER_ADMIN_PASSWORD"] = "correct-password",
                ["GATEKEEPER_ADMIN_COOKIE_SECURE"] = "false",
                ["AgentAuthentication__Enabled"] = "true",
                ["AgentAuthentication__ApiKeys__0__AgentId"] = "agent-1",
                ["AgentAuthentication__ApiKeys__0__Key"] = TestAgentKey,
            },
            services =>
            {
                services.RemoveAll<ISshCommandClient>();
                services.AddSingleton<ISshCommandClient>(clientDouble);
            }
        );
    }

    private static async Task<Guid> SeedApprovedSessionAsync(
        GatekeeperApiFactory factory,
        string profileName
    )
    {
        Guid sessionId = Guid.NewGuid();

        Session session = Session.Load(
            sessionId,
            Guid.NewGuid(),
            SessionStatus.Active,
            ["demo-ssh"],
            [profileName],
            [new SshProfileGrant("demo-ssh", profileName)],
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(15),
            0,
            Session.DefaultMaxActionCount,
            null,
            null,
            null
        );

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        GatekeeperDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<GatekeeperDbContext>();
        await new EfSessionRepository(dbContext).AddAsync(
            session,
            TestContext.Current.CancellationToken
        );
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return sessionId;
    }

    private static async Task<JsonElement> GetAuditPayloadAsync(
        GatekeeperApiFactory factory,
        Guid sessionId,
        string eventType
    )
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        GatekeeperDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<GatekeeperDbContext>();
        AuditEventEntity entity = await dbContext.AuditEvents.SingleAsync(
            auditEvent => auditEvent.AggregateId == sessionId && auditEvent.EventType == eventType,
            TestContext.Current.CancellationToken
        );

        using JsonDocument document = JsonDocument.Parse(entity.PayloadJson);
        return document.RootElement.Clone();
    }

    private sealed class RecordingSshCommandClient : ISshCommandClient
    {
        private readonly SshCommandClientResult _result;

        public RecordingSshCommandClient(SshCommandClientResult result)
        {
            _result = result;
        }

        public SshCommandClientRequest? LastRequest { get; private set; }

        public Task<SshCommandClientResult> ExecuteAsync(
            SshCommandClientRequest request,
            CancellationToken cancellationToken
        )
        {
            LastRequest = request;
            return Task.FromResult(_result);
        }
    }
}
