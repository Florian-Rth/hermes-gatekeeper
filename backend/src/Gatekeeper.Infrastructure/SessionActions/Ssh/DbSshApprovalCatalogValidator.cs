using Gatekeeper.Application.Sessions;
using Gatekeeper.Core.AccessRequests;
using Gatekeeper.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Gatekeeper.Infrastructure.SessionActions.Ssh;

public sealed class DbSshApprovalCatalogValidator : ISshApprovalCatalogValidator
{
    private readonly GatekeeperDbContext _dbContext;

    public DbSshApprovalCatalogValidator(GatekeeperDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> CanCreateSessionForApprovedRequestAsync(
        AccessRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Targets.Count != 1)
        {
            return true;
        }

        string[] profileNames = request
            .RequestedCapabilities.Where(IsSshProfileCapability)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (profileNames.Length == 0)
        {
            return true;
        }

        string targetAlias = request.Targets[0];
        Guid? targetId = await _dbContext
            .SshTargets.AsNoTracking()
            .Where(target => target.Alias == targetAlias)
            .Select(target => (Guid?)target.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (!targetId.HasValue)
        {
            return false;
        }

        int matchingProfiles = await _dbContext
            .SshProfiles.AsNoTracking()
            .Where(profile =>
                profile.TargetId == targetId.Value && profileNames.Contains(profile.Name)
            )
            .Select(profile => profile.Name)
            .Distinct()
            .CountAsync(cancellationToken);

        return matchingProfiles == profileNames.Length;
    }

    private static bool IsSshProfileCapability(string capability)
    {
        return capability.StartsWith("ssh.", StringComparison.Ordinal)
            || capability.StartsWith("remote.", StringComparison.Ordinal);
    }
}
