using Gatekeeper.Core.AccessRequests;
using Gatekeeper.Core.Sessions;

namespace Gatekeeper.Application.Sessions;

public interface ISshApprovalCatalogValidator
{
    Task<IReadOnlyList<SshProfileGrant>> ResolveGrantsAsync(
        AccessRequest request,
        CancellationToken cancellationToken
    );
}
