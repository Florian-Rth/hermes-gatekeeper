using Gatekeeper.Api.AgentAuthentication;
using Gatekeeper.Application.AccessRequests;
using Gatekeeper.Application.Common;
using Gatekeeper.Core.AccessRequests;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gatekeeper.Tests;

public sealed class AgentAuthAuditWriterTests
{
    [Fact]
    public async Task WriteFailedAuthenticationAsync_DoesNotThrow_WhenRepositoryWriteFails()
    {
        AgentAuthAuditWriter writer = new AgentAuthAuditWriter(
            new ThrowingAuditEventRepository(),
            new StubClock(),
            NullLogger<AgentAuthAuditWriter>.Instance
        );

        await writer.WriteFailedAuthenticationAsync(
            "/api/v1/access-requests",
            "POST",
            AgentAuthConstants.InvalidKeyReason,
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task WriteFailedAuthenticationAsync_DoesNotThrow_WhenSaveChangesFails()
    {
        AgentAuthAuditWriter writer = new AgentAuthAuditWriter(
            new CapturingAuditEventRepository { ThrowOnSaveChanges = true },
            new StubClock(),
            NullLogger<AgentAuthAuditWriter>.Instance
        );

        await writer.WriteFailedAuthenticationAsync(
            "/api/v1/access-requests",
            "POST",
            AgentAuthConstants.InvalidKeyReason,
            TestContext.Current.CancellationToken
        );
    }

    private sealed class StubClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.Parse("2026-05-31T12:00:00Z");
    }

    private sealed class ThrowingAuditEventRepository : IAuditEventRepository
    {
        public Task AddAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Simulated audit repository failure.");
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingAuditEventRepository : IAuditEventRepository
    {
        public AuditEvent? Captured { get; private set; }

        public bool ThrowOnSaveChanges { get; set; }

        public Task AddAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
        {
            Captured = auditEvent;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (ThrowOnSaveChanges)
            {
                throw new InvalidOperationException("Simulated save failure.");
            }

            return Task.CompletedTask;
        }
    }
}
