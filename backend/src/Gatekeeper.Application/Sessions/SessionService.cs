using System.Text.Json;
using Gatekeeper.Application.AccessRequests;
using Gatekeeper.Application.Common;
using Gatekeeper.Core.AccessRequests;
using Gatekeeper.Core.Sessions;

namespace Gatekeeper.Application.Sessions;

public sealed class SessionService : ISessionService
{
    private readonly ISessionRepository _sessions;
    private readonly IAuditEventRepository _auditEvents;
    private readonly ISessionActionUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public SessionService(
        ISessionRepository sessions,
        IAuditEventRepository auditEvents,
        ISessionActionUnitOfWork unitOfWork,
        IClock clock
    )
    {
        _sessions = sessions;
        _auditEvents = auditEvents;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<SessionDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        Session? session = await _sessions.GetByIdAsync(id, cancellationToken);
        if (session is null)
        {
            return null;
        }

        Session materialized = await MaterializeExpiryAsync(session, cancellationToken);
        return ToDetails(materialized);
    }

    public async Task<SessionLifecycleResult> CompleteAsync(
        Guid id,
        CancellationToken cancellationToken
    )
    {
        Session? session = await _sessions.GetByIdAsync(id, cancellationToken);
        if (session is null)
        {
            return SessionLifecycleResult.Missing();
        }

        Session materialized = await MaterializeExpiryAsync(session, cancellationToken);
        if (materialized.Status != SessionStatus.Active)
        {
            return SessionLifecycleResult.Conflicted(ToDetails(materialized));
        }

        DateTimeOffset now = _clock.UtcNow;
        Session completed = materialized.Complete(now);
        await _sessions.UpdateAsync(completed, cancellationToken);
        await _auditEvents.AddAsync(
            AuditEvent.CreateSessionCompleted(
                completed.Id,
                now,
                JsonSerializer.Serialize(ToAuditPayload(completed))
            ),
            cancellationToken
        );
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (PersistenceConflictException)
        {
            return SessionLifecycleResult.Conflicted(ToDetails(materialized));
        }

        return SessionLifecycleResult.Succeeded(ToDetails(completed));
    }

    public async Task<SessionLifecycleResult> RevokeAsync(
        Guid id,
        CancellationToken cancellationToken
    )
    {
        Session? session = await _sessions.GetByIdAsync(id, cancellationToken);
        if (session is null)
        {
            return SessionLifecycleResult.Missing();
        }

        Session materialized = await MaterializeExpiryAsync(session, cancellationToken);
        if (materialized.Status != SessionStatus.Active)
        {
            return SessionLifecycleResult.Conflicted(ToDetails(materialized));
        }

        DateTimeOffset now = _clock.UtcNow;
        Session revoked = materialized.Revoke(now);
        await _sessions.UpdateAsync(revoked, cancellationToken);
        await _auditEvents.AddAsync(
            AuditEvent.CreateSessionRevoked(
                revoked.Id,
                now,
                JsonSerializer.Serialize(ToAuditPayload(revoked))
            ),
            cancellationToken
        );
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (PersistenceConflictException)
        {
            return SessionLifecycleResult.Conflicted(ToDetails(materialized));
        }

        return SessionLifecycleResult.Succeeded(ToDetails(revoked));
    }

    private async Task<Session> MaterializeExpiryAsync(
        Session session,
        CancellationToken cancellationToken
    )
    {
        DateTimeOffset now = _clock.UtcNow;
        if (session.Status != SessionStatus.Active || session.ExpiresAt > now)
        {
            return session;
        }

        Session expired = session.Expire(now);
        await _sessions.UpdateAsync(expired, cancellationToken);
        await _auditEvents.AddAsync(
            AuditEvent.CreateSessionExpired(
                expired.Id,
                now,
                JsonSerializer.Serialize(ToAuditPayload(expired))
            ),
            cancellationToken
        );
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (PersistenceConflictException)
        {
            return session;
        }

        return expired;
    }

    private static SessionDetails ToDetails(Session session)
    {
        return new SessionDetails(
            session.Id,
            session.AccessRequestId,
            session.Status,
            session.AllowedTargets,
            session.AllowedCapabilities,
            session.CreatedAt,
            session.ExpiresAt,
            session.ActionCount,
            session.MaxActionCount,
            session.CompletedAt,
            session.RevokedAt,
            session.ExpiredAt
        );
    }

    private static object ToAuditPayload(Session session)
    {
        return new
        {
            SessionId = session.Id,
            session.AccessRequestId,
            Status = session.Status.ToString(),
            session.ExpiresAt,
            session.CompletedAt,
            session.RevokedAt,
            session.ExpiredAt,
        };
    }
}
