using Gatekeeper.Application.Sessions;

namespace Gatekeeper.Infrastructure.Persistence.Repositories;

public sealed class EfSessionActionUnitOfWork : ISessionActionUnitOfWork
{
    private readonly GatekeeperDbContext _dbContext;

    public EfSessionActionUnitOfWork(GatekeeperDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
