using Gatekeeper.Application.Sessions;
using Gatekeeper.Core.AccessRequests;
using Gatekeeper.Core.Sessions;
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

    public async Task<IReadOnlyList<SshProfileGrant>> ResolveGrantsAsync(
        AccessRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        string[] capabilities = request
            .RequestedCapabilities.Distinct(StringComparer.Ordinal)
            .ToArray();
        if (capabilities.Length == 0)
        {
            return [];
        }

        List<SshProfileGrant> grants = [];

        foreach (string targetAlias in request.Targets)
        {
            Guid? targetId = await _dbContext
                .SshTargets.AsNoTracking()
                .Where(target => target.Alias == targetAlias)
                .Select(target => (Guid?)target.Id)
                .SingleOrDefaultAsync(cancellationToken);
            if (!targetId.HasValue)
            {
                continue;
            }

            List<string> matchedProfiles = await _dbContext
                .SshProfiles.AsNoTracking()
                .Where(profile =>
                    profile.TargetId == targetId.Value && capabilities.Contains(profile.Name)
                )
                .Select(profile => profile.Name)
                .Distinct()
                .ToListAsync(cancellationToken);

            foreach (string profileName in matchedProfiles)
            {
                grants.Add(new SshProfileGrant(targetAlias, profileName));
            }
        }

        return grants;
    }
}
