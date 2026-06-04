using System.Text.Json;
using Gatekeeper.Application.AccessRequests;
using Gatekeeper.Application.AuditEvents;
using Gatekeeper.Application.Common;
using Gatekeeper.Core.AccessRequests;
using Gatekeeper.Core.Sessions;

namespace Gatekeeper.Application.Sessions;

public sealed class LegacySessionActionExecutor
{
    private readonly ISessionActionAdapter _adapter;
    private readonly ISessionRepository _sessions;
    private readonly IAuditEventRepository _auditEvents;
    private readonly ISessionActionUnitOfWork _unitOfWork;

    public LegacySessionActionExecutor(
        ISessionActionAdapter adapter,
        ISessionRepository sessions,
        IAuditEventRepository auditEvents,
        ISessionActionUnitOfWork unitOfWork
    )
    {
        _adapter = adapter;
        _sessions = sessions;
        _auditEvents = auditEvents;
        _unitOfWork = unitOfWork;
    }

    public async Task<SessionActionResult> ExecuteAsync(
        Session session,
        ExecuteSessionActionCommand command,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        if (!session.AllowedCapabilities.Contains(command.Capability, StringComparer.Ordinal))
        {
            string reason = "Capability is not allowed for this session.";
            await AddDeniedAuditAsync(session, command, now, reason, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return SessionActionResult.Forbidden(reason);
        }

        SessionActionValidationResult validation = _adapter.Validate(
            command.Capability,
            command.Payload
        );
        if (!validation.Succeeded)
        {
            string reason = validation.Error ?? "Invalid action payload.";
            await _auditEvents.AddAsync(
                AuditEvent.CreateSessionActionFailed(
                    session.Id,
                    now,
                    JsonSerializer.Serialize(BuildFullPayload(session, command, reason)),
                    ProjectDetails(session, command, reason)
                ),
                cancellationToken
            );
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return SessionActionResult.ValidationFailed(reason);
        }

        bool reserved = await TryReserveActionSlotAsync(session, command, now, cancellationToken);
        if (!reserved)
        {
            return await HandleReservationFailureAsync(session, command, now, cancellationToken);
        }

        SessionActionAdapterResult adapterResult = await _adapter.ExecuteAsync(
            command.Capability,
            command.Payload,
            cancellationToken
        );
        if (!adapterResult.Succeeded)
        {
            string reason = adapterResult.Error ?? "Session action failed.";
            await _auditEvents.AddAsync(
                AuditEvent.CreateSessionActionFailed(
                    session.Id,
                    now,
                    JsonSerializer.Serialize(BuildFullPayload(session, command, reason)),
                    ProjectDetails(session, command, reason)
                ),
                cancellationToken
            );
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (adapterResult.ValidationFailed)
            {
                return SessionActionResult.ValidationFailed(reason);
            }

            return SessionActionResult.Conflicted(reason);
        }

        await _auditEvents.AddAsync(
            AuditEvent.CreateSessionActionExecuted(
                session.Id,
                now,
                JsonSerializer.Serialize(BuildFullPayload(session, command, null)),
                ProjectDetails(session, command, null)
            ),
            cancellationToken
        );
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return SessionActionResult.Succeeded(
            new SessionActionExecution(
                session.Id,
                command.Capability,
                "succeeded",
                adapterResult.Result!.Value
            )
        );
    }

    private async Task<bool> TryReserveActionSlotAsync(
        Session session,
        ExecuteSessionActionCommand command,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        AuditEvent allowedAuditEvent = AuditEvent.CreateSessionActionAllowed(
            session.Id,
            now,
            JsonSerializer.Serialize(BuildFullPayload(session, command, null)),
            ProjectDetails(session, command, null)
        );

        return await _unitOfWork.TryReserveActionSlotAndSaveChangesAsync(
            session.Id,
            now,
            allowedAuditEvent,
            cancellationToken
        );
    }

    private async Task<SessionActionResult> HandleReservationFailureAsync(
        Session session,
        ExecuteSessionActionCommand command,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        Session? latest = await _sessions.GetByIdAsync(session.Id, cancellationToken);
        if (
            latest is not null
            && latest.Status == SessionStatus.Active
            && latest.ActionCount >= latest.MaxActionCount
        )
        {
            string reason = "Session action count limit exceeded.";
            await _auditEvents.AddAsync(
                AuditEvent.CreateActionCountExceeded(
                    session.Id,
                    now,
                    JsonSerializer.Serialize(BuildFullPayload(latest, command, reason)),
                    ProjectDetails(latest, command, reason)
                ),
                cancellationToken
            );
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return SessionActionResult.Conflicted(reason);
        }

        string inactiveReason = "Session is expired or inactive.";
        await AddDeniedAuditAsync(
            latest ?? session,
            command,
            now,
            inactiveReason,
            cancellationToken
        );
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return SessionActionResult.Conflicted(inactiveReason);
    }

    private Task AddDeniedAuditAsync(
        Session session,
        ExecuteSessionActionCommand command,
        DateTimeOffset now,
        string reason,
        CancellationToken cancellationToken
    )
    {
        return _auditEvents.AddAsync(
            AuditEvent.CreateSessionActionDenied(
                session.Id,
                now,
                JsonSerializer.Serialize(BuildFullPayload(session, command, reason)),
                ProjectDetails(session, command, reason)
            ),
            cancellationToken
        );
    }

    private static object BuildFullPayload(
        Session session,
        ExecuteSessionActionCommand command,
        string? reason
    )
    {
        return new
        {
            SessionId = session.Id,
            session.AccessRequestId,
            Capability = command.Capability,
            Target = (string?)null,
            TargetAlias = (string?)null,
            Action = (string?)null,
            IsMutating = (bool?)null,
            Risk = (string?)null,
            SafeParameters = (IReadOnlyDictionary<string, string>?)null,
            ExitStatus = (int?)null,
            DurationMs = (long?)null,
            TimedOut = (bool?)null,
            StdoutTruncated = (bool?)null,
            StderrTruncated = (bool?)null,
            Output = (object?)null,
            Reason = reason,
            ReasonCode = (string?)null,
            AgentId = command.Agent?.AgentId,
            AuthMethod = command.Agent?.AuthMethod,
        };
    }

    private static IReadOnlyDictionary<string, string> ProjectDetails(
        Session session,
        ExecuteSessionActionCommand command,
        string? reason
    )
    {
        return AuditDetailProjector.Project(
            new Dictionary<string, object?>
            {
                ["sessionId"] = session.Id,
                ["accessRequestId"] = session.AccessRequestId,
                ["capability"] = command.Capability,
                ["reason"] = reason,
                ["agentId"] = command.Agent?.AgentId,
                ["authMethod"] = command.Agent?.AuthMethod,
            }
        );
    }
}
