namespace Gatekeeper.Application.AuditEvents;

public interface IAuditEventQueryService
{
    Task<AuditEventPage> ListAsync(AuditEventQuery query, CancellationToken cancellationToken);
}
