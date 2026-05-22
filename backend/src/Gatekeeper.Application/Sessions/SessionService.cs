using Gatekeeper.Core.Sessions;

namespace Gatekeeper.Application.Sessions;

public sealed class SessionService : ISessionService
{
    private readonly ISessionRepository _sessions;

    public SessionService(ISessionRepository sessions)
    {
        _sessions = sessions;
    }

    public async Task<SessionDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        Session? session = await _sessions.GetByIdAsync(id, cancellationToken);
        return session is null ? null : ToDetails(session);
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
            session.ExpiresAt
        );
    }
}
