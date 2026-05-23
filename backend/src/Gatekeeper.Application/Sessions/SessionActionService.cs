using System.Text.Json;
using Gatekeeper.Application.AccessRequests;
using Gatekeeper.Application.Common;
using Gatekeeper.Core.AccessRequests;
using Gatekeeper.Core.Sessions;

namespace Gatekeeper.Application.Sessions;

public sealed class SessionActionService : ISessionActionService
{
    private readonly ISessionRepository _sessions;
    private readonly ISessionActionAdapter _adapter;
    private readonly IAuditEventRepository _auditEvents;
    private readonly ISessionActionUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public SessionActionService(
        ISessionRepository sessions,
        ISessionActionAdapter adapter,
        IAuditEventRepository auditEvents,
        ISessionActionUnitOfWork unitOfWork,
        IClock clock
    )
    {
        _sessions = sessions;
        _adapter = adapter;
        _auditEvents = auditEvents;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<SessionActionResult> ExecuteAsync(
        ExecuteSessionActionCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.SessionId == Guid.Empty)
        {
            return SessionActionResult.ValidationFailed("Session id is required.");
        }

        if (string.IsNullOrWhiteSpace(command.Capability))
        {
            return SessionActionResult.ValidationFailed("Capability is required.");
        }

        Session? session = await _sessions.GetByIdAsync(command.SessionId, cancellationToken);
        if (session is null)
        {
            return SessionActionResult.Missing();
        }

        DateTimeOffset now = _clock.UtcNow;
        await AddAuditAsync(
            AuditEvent.CreateSessionActionRequested(
                session.Id,
                now,
                JsonSerializer.Serialize(ToAuditPayload(session, command.Capability, null))
            ),
            cancellationToken
        );

        if (session.Status != SessionStatus.Active || session.ExpiresAt <= now)
        {
            string reason = "Session is expired or inactive.";
            await AddAuditAsync(
                AuditEvent.CreateSessionActionDenied(
                    session.Id,
                    now,
                    JsonSerializer.Serialize(ToAuditPayload(session, command.Capability, reason))
                ),
                cancellationToken
            );
            await SaveChangesAsync(cancellationToken);
            return SessionActionResult.Conflicted(reason);
        }

        if (!session.AllowedCapabilities.Contains(command.Capability, StringComparer.Ordinal))
        {
            string reason = "Capability is not allowed for this session.";
            await AddAuditAsync(
                AuditEvent.CreateSessionActionDenied(
                    session.Id,
                    now,
                    JsonSerializer.Serialize(ToAuditPayload(session, command.Capability, reason))
                ),
                cancellationToken
            );
            await SaveChangesAsync(cancellationToken);
            return SessionActionResult.Forbidden(reason);
        }

        await AddAuditAsync(
            AuditEvent.CreateSessionActionAllowed(
                session.Id,
                now,
                JsonSerializer.Serialize(ToAuditPayload(session, command.Capability, null))
            ),
            cancellationToken
        );

        SessionActionAdapterResult adapterResult = await _adapter.ExecuteAsync(
            command.Capability,
            command.Payload,
            cancellationToken
        );
        if (!adapterResult.Succeeded)
        {
            string reason = adapterResult.Error ?? "Session action failed.";
            await AddAuditAsync(
                AuditEvent.CreateSessionActionFailed(
                    session.Id,
                    now,
                    JsonSerializer.Serialize(ToAuditPayload(session, command.Capability, reason))
                ),
                cancellationToken
            );
            await SaveChangesAsync(cancellationToken);
            if (adapterResult.ValidationFailed)
            {
                return SessionActionResult.ValidationFailed(reason);
            }

            return SessionActionResult.Conflicted(reason);
        }

        await AddAuditAsync(
            AuditEvent.CreateSessionActionExecuted(
                session.Id,
                now,
                JsonSerializer.Serialize(ToAuditPayload(session, command.Capability, null))
            ),
            cancellationToken
        );
        await SaveChangesAsync(cancellationToken);

        return SessionActionResult.Succeeded(
            new SessionActionExecution(
                session.Id,
                command.Capability,
                "succeeded",
                adapterResult.Result!.Value
            )
        );
    }

    private Task AddAuditAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        return _auditEvents.AddAsync(auditEvent, cancellationToken);
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static object ToAuditPayload(Session session, string capability, string? reason)
    {
        return new
        {
            SessionId = session.Id,
            session.AccessRequestId,
            Capability = capability,
            Reason = reason,
        };
    }
}
