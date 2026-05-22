using Gatekeeper.Application.AccessRequests;
using Gatekeeper.Application.Common;
using Microsoft.EntityFrameworkCore;

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
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new PersistenceConflictException(
                "The access request was modified by another operation.",
                exception
            );
        }
        catch (DbUpdateException exception) when (IsSessionAccessRequestUniqueConstraint(exception))
        {
            throw new PersistenceConflictException(
                "A session already exists for this access request.",
                exception
            );
        }
    }

    private static bool IsSessionAccessRequestUniqueConstraint(DbUpdateException exception)
    {
        string? message = exception.InnerException?.Message;
        return message?.Contains("IX_Sessions_AccessRequestId", StringComparison.Ordinal) == true
            || message?.Contains("Sessions.AccessRequestId", StringComparison.Ordinal) == true;
    }
}
