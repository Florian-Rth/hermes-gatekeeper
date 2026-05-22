using Gatekeeper.Core.Sessions;

namespace Gatekeeper.Application.Sessions;

public interface ISessionRepository
{
    Task AddAsync(Session session, CancellationToken cancellationToken);

    Task<Session?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
