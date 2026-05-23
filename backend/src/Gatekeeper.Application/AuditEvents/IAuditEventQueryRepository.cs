namespace Gatekeeper.Application.AuditEvents;

public interface IAuditEventQueryRepository
{
    Task<IReadOnlyList<AuditEventSummary>> ListAsync(
        AuditEventQueryCriteria criteria,
        CancellationToken cancellationToken
    );
}
