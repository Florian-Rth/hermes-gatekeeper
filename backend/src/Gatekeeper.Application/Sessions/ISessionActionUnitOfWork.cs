using Gatekeeper.Core.AccessRequests;

namespace Gatekeeper.Application.Sessions;

public interface ISessionActionUnitOfWork
{
    Task<bool> TryReserveActionSlotAndSaveChangesAsync(
        Guid sessionId,
        DateTimeOffset reservationTime,
        AuditEvent auditEvent,
        CancellationToken cancellationToken
    );

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
