using System.Text.Json;
using Gatekeeper.Application.AccessRequests;
using Gatekeeper.Application.AuditEvents;
using Gatekeeper.Application.Common;
using Gatekeeper.Core.AccessRequests;
using Gatekeeper.Core.Sessions;

namespace Gatekeeper.Application.Sessions;

public sealed class SessionActionService : ISessionActionService
{
    private readonly ISessionRepository _sessions;
    private readonly IAuditEventRepository _auditEvents;
    private readonly ISessionActionUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly SshSessionActionExecutor _sshExecutor;
    private readonly LegacySessionActionExecutor _legacyExecutor;

    public SessionActionService(
        ISessionRepository sessions,
        IAuditEventRepository auditEvents,
        ISessionActionUnitOfWork unitOfWork,
        IClock clock,
        SshSessionActionExecutor sshExecutor,
        LegacySessionActionExecutor legacyExecutor
    )
    {
        _sessions = sessions;
        _auditEvents = auditEvents;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _sshExecutor = sshExecutor;
        _legacyExecutor = legacyExecutor;
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

        if (!IsCommandShapeValid(command))
        {
            return SessionActionResult.ValidationFailed(
                command.IsSshAction ? "Target and action are required." : "Capability is required."
            );
        }

        Session? session = await _sessions.GetByIdAsync(command.SessionId, cancellationToken);
        if (session is null)
        {
            return SessionActionResult.Missing();
        }

        DateTimeOffset now = _clock.UtcNow;
        await _auditEvents.AddAsync(
            AuditEvent.CreateSessionActionRequested(
                session.Id,
                now,
                JsonSerializer.Serialize(ToFullPayload(session, command, null)),
                ProjectActionDetails(session, command, null)
            ),
            cancellationToken
        );

        if (session.Status != SessionStatus.Active)
        {
            string reason = "Session is expired or inactive.";
            string? reasonCode = command.IsSshAction ? ToInactiveSessionReasonCode(session) : null;
            await AddDeniedAuditAsync(session, command, now, reason, reasonCode, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return SessionActionResult.Conflicted(reason);
        }

        if (session.ExpiresAt <= now)
        {
            session = session.Expire(now);
            await _sessions.UpdateAsync(session, cancellationToken);
            await _auditEvents.AddAsync(
                AuditEvent.CreateSessionExpired(
                    session.Id,
                    now,
                    JsonSerializer.Serialize(ToFullPayload(session, command, "Session expired.")),
                    ProjectActionDetails(session, command, "Session expired.")
                ),
                cancellationToken
            );
            string reason = "Session is expired or inactive.";
            string? reasonCode = command.IsSshAction ? "session_expired" : null;
            await AddDeniedAuditAsync(session, command, now, reason, reasonCode, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return SessionActionResult.Conflicted(reason);
        }

        if (command.IsSshAction)
        {
            return await _sshExecutor.ExecuteAsync(session, command, now, cancellationToken);
        }

        return await _legacyExecutor.ExecuteAsync(session, command, now, cancellationToken);
    }

    private Task AddDeniedAuditAsync(
        Session session,
        ExecuteSessionActionCommand command,
        DateTimeOffset now,
        string reason,
        string? reasonCode,
        CancellationToken cancellationToken
    )
    {
        return _auditEvents.AddAsync(
            AuditEvent.CreateSessionActionDenied(
                session.Id,
                now,
                JsonSerializer.Serialize(ToFullPayload(session, command, reason, reasonCode)),
                ProjectActionDetails(session, command, reason, reasonCode)
            ),
            cancellationToken
        );
    }

    private static bool IsCommandShapeValid(ExecuteSessionActionCommand command)
    {
        if (command.IsSshAction)
        {
            return !string.IsNullOrWhiteSpace(command.Target)
                && !string.IsNullOrWhiteSpace(command.Action);
        }

        return !string.IsNullOrWhiteSpace(command.Capability);
    }

    private static string ToInactiveSessionReasonCode(Session session)
    {
        return session.Status == SessionStatus.Expired ? "session_expired" : "session_inactive";
    }

    internal static object ToFullPayload(
        Session session,
        ExecuteSessionActionCommand command,
        string? reason,
        string? reasonCode = null
    )
    {
        if (command.IsSshAction)
        {
            return new
            {
                SessionId = session.Id,
                session.AccessRequestId,
                TargetAlias = command.Target,
                Action = command.Action,
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
                ReasonCode = reasonCode,
                AgentId = command.Agent?.AgentId,
                AuthMethod = command.Agent?.AuthMethod,
            };
        }

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
            ReasonCode = reasonCode,
            AgentId = command.Agent?.AgentId,
            AuthMethod = command.Agent?.AuthMethod,
        };
    }

    internal static IReadOnlyDictionary<string, string> ProjectActionDetails(
        Session session,
        ExecuteSessionActionCommand command,
        string? reason,
        string? reasonCode = null
    )
    {
        Dictionary<string, object?> source = new()
        {
            ["sessionId"] = session.Id,
            ["accessRequestId"] = session.AccessRequestId,
            ["reason"] = reason,
            ["reasonCode"] = reasonCode,
            ["agentId"] = command.Agent?.AgentId,
            ["authMethod"] = command.Agent?.AuthMethod,
        };

        if (command.IsSshAction)
        {
            source["targetAlias"] = command.Target;
            source["action"] = command.Action;
        }
        else
        {
            source["capability"] = command.Capability;
        }

        return AuditDetailProjector.Project(source);
    }
}
