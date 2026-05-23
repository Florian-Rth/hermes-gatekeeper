using System.Net;
using System.Text.Json;
using Gatekeeper.Infrastructure.Persistence;
using Gatekeeper.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gatekeeper.Tests;

public sealed class AuditEventEndpointTests
{
    [Fact]
    public async Task ListWithoutTokenReturnsUnauthorized()
    {
        await using AuditEventApiFactory factory = new();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            "/api/v1/audit-events",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListWithWrongTokenReturnsForbidden()
    {
        await using AuditEventApiFactory factory = new();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Gatekeeper-Admin-Token", "wrong-token");

        using HttpResponseMessage response = await client.GetAsync(
            "/api/v1/audit-events",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListWithCorrectTokenReturnsBoundedEvents()
    {
        await using AuditEventApiFactory factory = new();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        Guid aggregateId = Guid.NewGuid();
        Guid eventId = Guid.NewGuid();
        await factory.SeedAsync(
            new AuditEventEntity
            {
                Id = eventId,
                EventType = "SessionActionExecuted",
                AggregateId = aggregateId,
                OccurredAt = DateTimeOffset.Parse("2026-05-23T10:00:00Z"),
                PayloadJson = JsonSerializer.Serialize(
                    new
                    {
                        Action = "restart service",
                        Outcome = "allowed",
                        RawOutput = "secret log dump",
                    }
                ),
            },
            TestContext.Current.CancellationToken
        );
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Gatekeeper-Admin-Token", "test-admin-token");

        using HttpResponseMessage response = await client.GetAsync(
            "/api/v1/audit-events",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ParseAsync(response);
        JsonElement item = Assert.Single(
            document.RootElement.GetProperty("items").EnumerateArray()
        );
        Assert.Equal(eventId, item.GetProperty("id").GetGuid());
        Assert.Equal("SessionActionExecuted", item.GetProperty("eventType").GetString());
        Assert.Equal(aggregateId, item.GetProperty("aggregateId").GetGuid());
        Assert.Equal("2026-05-23T10:00:00+00:00", item.GetProperty("occurredAt").GetString());
        JsonElement details = item.GetProperty("details");
        Assert.Equal("restart service", details.GetProperty("action").GetString());
        Assert.Equal("allowed", details.GetProperty("outcome").GetString());
        Assert.False(details.TryGetProperty("rawOutput", out _));
        Assert.False(item.TryGetProperty("payloadJson", out _));
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("nextCursor").ValueKind);
    }

    [Fact]
    public async Task ListExtractsBoundedDetailsFromRealPascalCaseNestedPayload()
    {
        await using AuditEventApiFactory factory = new();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        Guid aggregateId = Guid.NewGuid();
        await factory.SeedAsync(
            new AuditEventEntity
            {
                Id = Guid.NewGuid(),
                EventType = "AccessRequestApproved",
                AggregateId = aggregateId,
                OccurredAt = DateTimeOffset.Parse("2026-05-23T10:00:00Z"),
                PayloadJson = JsonSerializer.Serialize(
                    new
                    {
                        Details = new
                        {
                            Requester = "florian",
                            Risk = "medium",
                            Status = "Approved",
                            ForbiddenActionsJson = "secret",
                        },
                        Comment = "looks good",
                    }
                ),
            },
            TestContext.Current.CancellationToken
        );
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Gatekeeper-Admin-Token", "test-admin-token");

        using HttpResponseMessage response = await client.GetAsync(
            "/api/v1/audit-events",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ParseAsync(response);
        JsonElement details = Assert
            .Single(document.RootElement.GetProperty("items").EnumerateArray())
            .GetProperty("details");
        Assert.Equal("florian", details.GetProperty("requester").GetString());
        Assert.Equal("medium", details.GetProperty("risk").GetString());
        Assert.Equal("Approved", details.GetProperty("status").GetString());
        Assert.Equal("looks good", details.GetProperty("comment").GetString());
        Assert.False(details.TryGetProperty("forbiddenActionsJson", out _));
    }

    [Fact]
    public async Task ListAppliesEventTypeAggregateAndTimeFilters()
    {
        await using AuditEventApiFactory factory = new();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        Guid matchingAggregateId = Guid.NewGuid();
        Guid otherAggregateId = Guid.NewGuid();
        Guid matchingId = Guid.NewGuid();
        await factory.SeedAsync(
            new AuditEventEntity
            {
                Id = Guid.NewGuid(),
                EventType = "SessionCreated",
                AggregateId = matchingAggregateId,
                OccurredAt = DateTimeOffset.Parse("2026-05-23T09:59:59Z"),
                PayloadJson = "{}",
            },
            new AuditEventEntity
            {
                Id = matchingId,
                EventType = "SessionActionDenied",
                AggregateId = matchingAggregateId,
                OccurredAt = DateTimeOffset.Parse("2026-05-23T10:30:00Z"),
                PayloadJson = "{\"reason\":\"not allowed\"}",
            },
            new AuditEventEntity
            {
                Id = Guid.NewGuid(),
                EventType = "SessionActionDenied",
                AggregateId = otherAggregateId,
                OccurredAt = DateTimeOffset.Parse("2026-05-23T10:30:00Z"),
                PayloadJson = "{}",
            },
            new AuditEventEntity
            {
                Id = Guid.NewGuid(),
                EventType = "SessionActionDenied",
                AggregateId = matchingAggregateId,
                OccurredAt = DateTimeOffset.Parse("2026-05-23T11:00:01Z"),
                PayloadJson = "{}",
            },
            TestContext.Current.CancellationToken
        );
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Gatekeeper-Admin-Token", "test-admin-token");

        using HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/audit-events?eventType=SessionActionDenied&aggregateId={matchingAggregateId}&from=2026-05-23T10:00:00Z&to=2026-05-23T11:00:00Z",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ParseAsync(response);
        JsonElement item = Assert.Single(
            document.RootElement.GetProperty("items").EnumerateArray()
        );
        Assert.Equal(matchingId, item.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task ListPaginatesWithDefaultMaxAndOpaqueCursor()
    {
        await using AuditEventApiFactory factory = new();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        DateTimeOffset occurredAt = DateTimeOffset.Parse("2026-05-23T10:00:00Z");
        AuditEventEntity[] events = Enumerable
            .Range(0, 105)
            .Select(index => new AuditEventEntity
            {
                Id = Guid.Parse($"00000000-0000-0000-0000-{index + 1:000000000000}"),
                EventType = "SessionActionRequested",
                AggregateId = Guid.NewGuid(),
                OccurredAt = occurredAt.AddMinutes(index),
                PayloadJson = "{}",
            })
            .ToArray();
        await factory.SeedAsync(events, TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Gatekeeper-Admin-Token", "test-admin-token");

        using HttpResponseMessage defaultResponse = await client.GetAsync(
            "/api/v1/audit-events",
            TestContext.Current.CancellationToken
        );
        using JsonDocument defaultDocument = await ParseAsync(defaultResponse);
        Assert.Equal(50, defaultDocument.RootElement.GetProperty("items").GetArrayLength());
        string defaultCursor = defaultDocument.RootElement.GetProperty("nextCursor").GetString()!;
        Assert.False(string.IsNullOrWhiteSpace(defaultCursor));
        Assert.DoesNotContain("2026-05-23", defaultCursor, StringComparison.OrdinalIgnoreCase);

        using HttpResponseMessage maxResponse = await client.GetAsync(
            "/api/v1/audit-events?limit=500",
            TestContext.Current.CancellationToken
        );
        using JsonDocument maxDocument = await ParseAsync(maxResponse);
        Assert.Equal(100, maxDocument.RootElement.GetProperty("items").GetArrayLength());

        using HttpResponseMessage nextResponse = await client.GetAsync(
            $"/api/v1/audit-events?limit=50&cursor={Uri.EscapeDataString(defaultCursor)}",
            TestContext.Current.CancellationToken
        );
        using JsonDocument nextDocument = await ParseAsync(nextResponse);
        JsonElement firstNextItem = nextDocument.RootElement.GetProperty("items")[0];
        Assert.Equal(events[50].Id, firstNextItem.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task ListRejectsInvalidCursorAndDates()
    {
        await using AuditEventApiFactory factory = new();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Gatekeeper-Admin-Token", "test-admin-token");

        foreach (
            string path in new[]
            {
                "/api/v1/audit-events?cursor=not-a-cursor",
                "/api/v1/audit-events?from=not-a-date",
                "/api/v1/audit-events?to=not-a-date",
                "/api/v1/audit-events?from=2026-05-24T00:00:00Z&to=2026-05-23T00:00:00Z",
            }
        )
        {
            using HttpResponseMessage response = await client.GetAsync(
                path,
                TestContext.Current.CancellationToken
            );
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    private static async Task<JsonDocument> ParseAsync(HttpResponseMessage response)
    {
        return await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken
        );
    }

    private sealed class AuditEventApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databasePath;

        public AuditEventApiFactory(string? adminToken = "test-admin-token")
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

        public async Task SeedAsync(
            AuditEventEntity auditEvent,
            CancellationToken cancellationToken
        )
        {
            await SeedAsync([auditEvent], cancellationToken);
        }

        public async Task SeedAsync(
            AuditEventEntity first,
            AuditEventEntity second,
            AuditEventEntity third,
            AuditEventEntity fourth,
            CancellationToken cancellationToken
        )
        {
            await SeedAsync([first, second, third, fourth], cancellationToken);
        }

        public async Task SeedAsync(
            IReadOnlyCollection<AuditEventEntity> auditEvents,
            CancellationToken cancellationToken
        )
        {
            await using AsyncServiceScope scope = Services.CreateAsyncScope();
            GatekeeperDbContext dbContext =
                scope.ServiceProvider.GetRequiredService<GatekeeperDbContext>();
            await dbContext.AuditEvents.AddRangeAsync(auditEvents, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
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
