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
            CreatedAt = session.CreatedAt,
            ExpiresAt = session.ExpiresAt,
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
            entity.CreatedAt,
            entity.ExpiresAt
        );
    }
}
