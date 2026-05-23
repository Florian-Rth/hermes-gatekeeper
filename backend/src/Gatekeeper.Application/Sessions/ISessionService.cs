namespace Gatekeeper.Application.Sessions;

public interface ISessionService
{
    Task<SessionDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<SessionLifecycleResult> CompleteAsync(Guid id, CancellationToken cancellationToken);

    Task<SessionLifecycleResult> RevokeAsync(Guid id, CancellationToken cancellationToken);
}
