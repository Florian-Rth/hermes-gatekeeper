using Gatekeeper.Core.AccessRequests;

namespace Gatekeeper.Application.AccessRequests;

public interface IAuditEventRepository
{
    Task AddAsync(AuditEvent auditEvent, CancellationToken cancellationToken);
}
