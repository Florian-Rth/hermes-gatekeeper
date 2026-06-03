using Gatekeeper.Core.AccessRequests;

namespace Gatekeeper.Application.Sessions;

public interface ISshApprovalCatalogValidator
{
    Task<bool> CanCreateSessionForApprovedRequestAsync(
        AccessRequest request,
        CancellationToken cancellationToken
    );
}
