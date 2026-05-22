namespace Gatekeeper.Application.AccessRequests;

public interface IAccessRequestUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
