using Gatekeeper.Application.AccessRequests;

namespace Gatekeeper.Infrastructure.Persistence.Repositories;

public sealed class EfAccessRequestUnitOfWork : IAccessRequestUnitOfWork
{
    private readonly GatekeeperDbContext _dbContext;

    public EfAccessRequestUnitOfWork(GatekeeperDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
