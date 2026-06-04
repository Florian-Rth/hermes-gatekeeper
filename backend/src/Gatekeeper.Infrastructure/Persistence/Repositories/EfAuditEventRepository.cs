using System.Text.Json;
using Gatekeeper.Application.AccessRequests;
using Gatekeeper.Application.AuditEvents;
using Gatekeeper.Core.AccessRequests;
using Gatekeeper.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatekeeper.Infrastructure.Persistence.Repositories;

public sealed class EfAuditEventRepository : IAuditEventRepository, IAuditEventQueryRepository
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

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new InvalidOperationException("Audit event persistence conflict.", exception);
        }
    }

    public async Task<IReadOnlyList<AuditEventSummary>> ListAsync(
        AuditEventQueryCriteria criteria,
        CancellationToken cancellationToken
    )
    {
        IQueryable<AuditEventEntity> query = _dbContext.AuditEvents.AsNoTracking();

        if (criteria.AggregateId.HasValue)
        {
            Guid aggregateId = criteria.AggregateId.Value;
            query = query.Where(entity => entity.AggregateId == aggregateId);
        }

        if (!string.IsNullOrWhiteSpace(criteria.EventType))
        {
            string eventType = criteria.EventType;
            query = query.Where(entity => entity.EventType == eventType);
        }

        AuditEventEntity[] orderedEntities = (await query.ToArrayAsync(cancellationToken))
            .Where(entity => !criteria.From.HasValue || entity.OccurredAt >= criteria.From.Value)
            .Where(entity => !criteria.To.HasValue || entity.OccurredAt <= criteria.To.Value)
            .OrderBy(entity => entity.OccurredAt)
            .ThenBy(entity => entity.Id)
            .ToArray();

        if (criteria.Cursor is not null)
        {
            DateTimeOffset cursorOccurredAt = criteria.Cursor.OccurredAt;
            Guid cursorId = criteria.Cursor.Id;

            orderedEntities = orderedEntities
                .Where(entity =>
                    entity.OccurredAt > cursorOccurredAt
                    || (entity.OccurredAt == cursorOccurredAt && entity.Id.CompareTo(cursorId) > 0)
                )
                .ToArray();
        }

        return orderedEntities.Take(criteria.Take).Select(ToSummary).ToArray();
    }

    internal static AuditEventEntity ToEntity(AuditEvent auditEvent)
    {
        return new AuditEventEntity
        {
            Id = auditEvent.Id,
            EventType = auditEvent.EventType,
            AggregateId = auditEvent.AggregateId,
            OccurredAt = auditEvent.OccurredAt,
            PayloadJson = auditEvent.PayloadJson,
            DetailsJson = auditEvent.BoundedDetails is not null
                ? JsonSerializer.Serialize(auditEvent.BoundedDetails)
                : null,
        };
    }

    private static AuditEventSummary ToSummary(AuditEventEntity entity)
    {
        return new AuditEventSummary(
            entity.Id,
            entity.EventType,
            entity.AggregateId,
            entity.OccurredAt,
            DeserializeDetailsOrFallback(entity)
        );
    }

    private static IReadOnlyDictionary<string, string> DeserializeDetailsOrFallback(
        AuditEventEntity entity
    )
    {
        if (!string.IsNullOrWhiteSpace(entity.DetailsJson))
        {
            try
            {
                Dictionary<string, string>? details = JsonSerializer.Deserialize<
                    Dictionary<string, string>
                >(entity.DetailsJson);
                if (details is not null)
                {
                    return details;
                }
            }
            catch (JsonException) { }
        }

        return AuditDetailProjector.ProjectFromSerializedPayload(entity.PayloadJson);
    }
}
