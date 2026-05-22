namespace Gatekeeper.Application.Sessions;

public interface ISessionService
{
    Task<SessionDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
