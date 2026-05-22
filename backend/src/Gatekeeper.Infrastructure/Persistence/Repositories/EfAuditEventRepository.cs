using Gatekeeper.Application.AccessRequests;
using Gatekeeper.Core.AccessRequests;
using Gatekeeper.Infrastructure.Persistence.Entities;

namespace Gatekeeper.Infrastructure.Persistence.Repositories;

public sealed class EfAuditEventRepository : IAuditEventRepository
{
    private readonly GatekeeperDbContext _dbContext;

    public EfAuditEventRepository(GatekeeperDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        await _dbContext.AuditEvents.AddAsync(ToEntity(auditEvent), cancellationToken);
    }

    private static AuditEventEntity ToEntity(AuditEvent auditEvent)
    {
        return new AuditEventEntity
        {
            Id = auditEvent.Id,
            EventType = auditEvent.EventType,
            AggregateId = auditEvent.AggregateId,
            OccurredAt = auditEvent.OccurredAt,
            PayloadJson = auditEvent.PayloadJson,
        };
    }
}
