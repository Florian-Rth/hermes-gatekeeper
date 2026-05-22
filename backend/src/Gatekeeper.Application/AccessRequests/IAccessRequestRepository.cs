using Gatekeeper.Core.AccessRequests;

namespace Gatekeeper.Application.AccessRequests;

public interface IAccessRequestRepository
{
    Task AddAsync(AccessRequest accessRequest, CancellationToken cancellationToken);

    Task UpdateAsync(AccessRequest accessRequest, CancellationToken cancellationToken);

    Task<AccessRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<AccessRequest>> ListAsync(CancellationToken cancellationToken);
}
