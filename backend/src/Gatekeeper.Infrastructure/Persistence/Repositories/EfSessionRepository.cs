using Gatekeeper.Application.Sessions;
using Gatekeeper.Core.Sessions;
using Gatekeeper.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatekeeper.Infrastructure.Persistence.Repositories;

public sealed class EfSessionRepository : ISessionRepository
{
    private readonly GatekeeperDbContext _dbContext;

    public EfSessionRepository(GatekeeperDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Session session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        await _dbContext.Sessions.AddAsync(ToEntity(session), cancellationToken);
    }

    public async Task<Session?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        SessionEntity? entity = await _dbContext
            .Sessions.AsNoTracking()
            .SingleOrDefaultAsync(session => session.Id == id, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public Task UpdateAsync(Session session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        SessionEntity entity = ToEntity(session);
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<SessionEntity> entry =
            _dbContext.Sessions.Attach(entity);
        entry.Property(updatedSession => updatedSession.Status).IsModified = true;
        entry.Property(updatedSession => updatedSession.CompletedAt).IsModified = true;
        entry.Property(updatedSession => updatedSession.RevokedAt).IsModified = true;
        entry.Property(updatedSession => updatedSession.ExpiredAt).IsModified = true;
        if (
            session.Status
            is SessionStatus.Completed
                or SessionStatus.Revoked
                or SessionStatus.Expired
        )
        {
            entry.Property(updatedSession => updatedSession.Status).OriginalValue =
                SessionStatus.Active;
        }

        return Task.CompletedTask;
    }

    private static SessionEntity ToEntity(Session session)
    {
        return new SessionEntity
        {
            Id = session.Id,
            AccessRequestId = session.AccessRequestId,
            Status = session.Status,
            AllowedTargetsJson = JsonColumnSerializer.SerializeStringList(session.AllowedTargets),
            AllowedCapabilitiesJson = JsonColumnSerializer.SerializeStringList(
                session.AllowedCapabilities
            ),
            SshProfileGrantsJson = JsonColumnSerializer.SerializeSshProfileGrantList(
                session.SshProfileGrants
            ),
            CreatedAt = session.CreatedAt,
            ExpiresAt = session.ExpiresAt,
            ActionCount = session.ActionCount,
            MaxActionCount = session.MaxActionCount,
            CompletedAt = session.CompletedAt,
            RevokedAt = session.RevokedAt,
            ExpiredAt = session.ExpiredAt,
        };
    }

    private static Session ToDomain(SessionEntity entity)
    {
        return Session.Load(
            entity.Id,
            entity.AccessRequestId,
            entity.Status,
            JsonColumnSerializer.DeserializeStringList(entity.AllowedTargetsJson),
            JsonColumnSerializer.DeserializeStringList(entity.AllowedCapabilitiesJson),
            JsonColumnSerializer.DeserializeSshProfileGrantList(entity.SshProfileGrantsJson),
            entity.CreatedAt,
            entity.ExpiresAt,
            entity.ActionCount,
            entity.MaxActionCount,
            entity.CompletedAt,
            entity.RevokedAt,
            entity.ExpiredAt
        );
    }
}
