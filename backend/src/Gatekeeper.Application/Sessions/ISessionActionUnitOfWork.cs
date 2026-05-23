namespace Gatekeeper.Application.Sessions;

public interface ISessionActionUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
