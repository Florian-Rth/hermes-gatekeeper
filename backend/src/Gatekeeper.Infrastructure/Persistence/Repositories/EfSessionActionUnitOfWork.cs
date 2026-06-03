using Gatekeeper.Application.Common;
using Gatekeeper.Application.Sessions;
using Gatekeeper.Core.AccessRequests;
using Gatekeeper.Core.Sessions;
using Gatekeeper.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatekeeper.Infrastructure.Persistence.Repositories;

public sealed class EfSessionActionUnitOfWork : ISessionActionUnitOfWork
{
    private readonly GatekeeperDbContext _dbContext;

    public EfSessionActionUnitOfWork(GatekeeperDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> TryReserveActionSlotAndSaveChangesAsync(
        Guid sessionId,
        DateTimeOffset reservationTime,
        AuditEvent auditEvent,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            cancellationToken
        );

        SessionReservationSnapshot? snapshot = await _dbContext
            .Sessions.Where(session => session.Id == sessionId)
            .Select(session => new SessionReservationSnapshot(
                session.Status,
                session.ExpiresAt,
                session.ActionCount,
                session.MaxActionCount
            ))
            .SingleOrDefaultAsync(cancellationToken);

        if (snapshot is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        if (
            snapshot.Status != SessionStatus.Active
            || snapshot.ExpiresAt <= reservationTime
            || snapshot.ActionCount >= snapshot.MaxActionCount
        )
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        int updatedRows = await _dbContext
            .Sessions.Where(session =>
                session.Id == sessionId
                && session.Status == snapshot.Status
                && session.ExpiresAt == snapshot.ExpiresAt
                && session.ActionCount == snapshot.ActionCount
                && session.MaxActionCount == snapshot.MaxActionCount
            )
            .ExecuteUpdateAsync(
                setters =>
                    setters.SetProperty(
                        session => session.ActionCount,
                        session => session.ActionCount + 1
                    ),
                cancellationToken
            );

        if (updatedRows != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await _dbContext.AuditEvents.AddAsync(ToEntity(auditEvent), cancellationToken);
        await SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return true;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new PersistenceConflictException(
                "The session was modified by another operation.",
                exception
            );
        }
    }

    private sealed record SessionReservationSnapshot(
        SessionStatus Status,
        DateTimeOffset ExpiresAt,
        int ActionCount,
        int MaxActionCount
    );

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
