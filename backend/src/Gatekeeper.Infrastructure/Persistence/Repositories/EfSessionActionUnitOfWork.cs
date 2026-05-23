using System.Globalization;
using Gatekeeper.Application.Common;
using Gatekeeper.Application.Sessions;
using Gatekeeper.Core.AccessRequests;
using Gatekeeper.Core.Sessions;
using Gatekeeper.Infrastructure.Persistence.Entities;
using Microsoft.Data.Sqlite;
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

        int updatedRows = await _dbContext.Database.ExecuteSqlRawAsync(
            """
            UPDATE Sessions
            SET ActionCount = ActionCount + 1
            WHERE Id = @sessionId
              AND Status = @activeStatus
              AND ExpiresAt > @reservationTime
              AND ActionCount < MaxActionCount
            """,
            [
                new SqliteParameter
                {
                    ParameterName = "@sessionId",
                    Value = ToSqliteGuid(sessionId),
                },
                new SqliteParameter
                {
                    ParameterName = "@activeStatus",
                    Value = (int)SessionStatus.Active,
                },
                new SqliteParameter
                {
                    ParameterName = "@reservationTime",
                    Value = ToSqliteDateTimeOffset(reservationTime),
                },
            ],
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

    private static string ToSqliteDateTimeOffset(DateTimeOffset value)
    {
        return value
            .ToUniversalTime()
            .ToString("yyyy-MM-dd HH:mm:ss.FFFFFFFzzz", CultureInfo.InvariantCulture);
    }

    private static string ToSqliteGuid(Guid value)
    {
        return value.ToString("D").ToUpperInvariant();
    }
}
