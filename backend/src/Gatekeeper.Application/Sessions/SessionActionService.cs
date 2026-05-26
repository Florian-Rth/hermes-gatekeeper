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
    private readonly ISshActionPolicy _sshActionPolicy;
    private readonly ISshCommandExecutor _sshCommandExecutor;
    private readonly IAuditEventRepository _auditEvents;
    private readonly ISessionActionUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public SessionActionService(
        ISessionRepository sessions,
        ISessionActionAdapter adapter,
        ISshActionPolicy sshActionPolicy,
        ISshCommandExecutor sshCommandExecutor,
        IAuditEventRepository auditEvents,
        ISessionActionUnitOfWork unitOfWork,
        IClock clock
    )
    {
        _sessions = sessions;
        _adapter = adapter;
        _sshActionPolicy = sshActionPolicy;
        _sshCommandExecutor = sshCommandExecutor;
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
        await AddAuditAsync(
            AuditEvent.CreateSessionActionRequested(
                session.Id,
                now,
                JsonSerializer.Serialize(ToAuditPayload(session, command, null))
            ),
            cancellationToken
        );

        if (session.Status != SessionStatus.Active)
        {
            string reason = "Session is expired or inactive.";
            await AddDeniedAuditAsync(session, command, now, reason, cancellationToken);
            await SaveChangesAsync(cancellationToken);
            return SessionActionResult.Conflicted(reason);
        }

        if (session.ExpiresAt <= now)
        {
            session = session.Expire(now);
            await _sessions.UpdateAsync(session, cancellationToken);
            await AddAuditAsync(
                AuditEvent.CreateSessionExpired(
                    session.Id,
                    now,
                    JsonSerializer.Serialize(ToAuditPayload(session, command, "Session expired."))
                ),
                cancellationToken
            );
            string reason = "Session is expired or inactive.";
            await AddDeniedAuditAsync(session, command, now, reason, cancellationToken);
            await SaveChangesAsync(cancellationToken);
            return SessionActionResult.Conflicted(reason);
        }

        if (command.IsSshAction)
        {
            return await ExecuteSshAsync(session, command, now, cancellationToken);
        }

        return await ExecuteLegacyAdapterAsync(session, command, now, cancellationToken);
    }

    private async Task<SessionActionResult> ExecuteSshAsync(
        Session session,
        ExecuteSessionActionCommand command,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        if (!session.AllowedTargets.Contains(command.Target, StringComparer.Ordinal))
        {
            string reason = "Target is not allowed for this session.";
            await AddDeniedAuditAsync(session, command, now, reason, cancellationToken);
            await SaveChangesAsync(cancellationToken);
            return SessionActionResult.Forbidden(reason);
        }

        IReadOnlyCollection<SshApprovedProfileGrant> grants = session
            .AllowedCapabilities.Select(profile => new SshApprovedProfileGrant(
                command.Target,
                profile
            ))
            .ToArray();
        SshActionPolicyResult policyResult = _sshActionPolicy.Resolve(
            command.Target,
            command.Action,
            grants,
            command.Parameters
        );
        if (!policyResult.Succeeded)
        {
            string reason = SanitizePolicyFailure(policyResult.FailureReason);
            await AddDeniedAuditAsync(session, command, now, reason, cancellationToken);
            await SaveChangesAsync(cancellationToken);
            return MapPolicyFailure(policyResult.FailureReason, reason);
        }

        bool reserved = await TryReserveActionSlotAsync(session, command, now, cancellationToken);
        if (!reserved)
        {
            return await HandleReservationFailureAsync(session, command, now, cancellationToken);
        }

        SshCommandExecutionResult executionResult;
        try
        {
            executionResult = await _sshCommandExecutor.ExecuteAsync(
                policyResult.ResolvedAction!,
                cancellationToken
            );
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            executionResult = SshCommandExecutionResult.Failed(
                SshCommandExecutionFailureReason.ClientFailed,
                "SSH action execution failed."
            );
        }

        if (!executionResult.Succeeded)
        {
            string reason = SanitizeExecutionFailure(executionResult.FailureReason);
            await AddAuditAsync(
                AuditEvent.CreateSessionActionFailed(
                    session.Id,
                    now,
                    JsonSerializer.Serialize(ToAuditPayload(session, command, reason))
                ),
                cancellationToken
            );
            await SaveChangesAsync(cancellationToken);
            return SessionActionResult.Conflicted(reason);
        }

        await AddAuditAsync(
            AuditEvent.CreateSessionActionExecuted(
                session.Id,
                now,
                JsonSerializer.Serialize(ToAuditPayload(session, command, null))
            ),
            cancellationToken
        );
        await SaveChangesAsync(cancellationToken);

        return SessionActionResult.Succeeded(
            new SessionActionExecution(
                session.Id,
                command.Action,
                "succeeded",
                JsonSerializer.SerializeToElement(ToSshResult(executionResult.Output!))
            )
        );
    }

    private async Task<SessionActionResult> ExecuteLegacyAdapterAsync(
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
            await SaveChangesAsync(cancellationToken);
            return SessionActionResult.Forbidden(reason);
        }

        SessionActionValidationResult validation = _adapter.Validate(
            command.Capability,
            command.Payload
        );
        if (!validation.Succeeded)
        {
            string reason = validation.Error ?? "Invalid action payload.";
            await AddAuditAsync(
                AuditEvent.CreateSessionActionFailed(
                    session.Id,
                    now,
                    JsonSerializer.Serialize(ToAuditPayload(session, command, reason))
                ),
                cancellationToken
            );
            await SaveChangesAsync(cancellationToken);
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
            await AddAuditAsync(
                AuditEvent.CreateSessionActionFailed(
                    session.Id,
                    now,
                    JsonSerializer.Serialize(ToAuditPayload(session, command, reason))
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
                JsonSerializer.Serialize(ToAuditPayload(session, command, null))
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
            JsonSerializer.Serialize(ToAuditPayload(session, command, null))
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
            await AddAuditAsync(
                AuditEvent.CreateActionCountExceeded(
                    session.Id,
                    now,
                    JsonSerializer.Serialize(ToAuditPayload(latest, command, reason))
                ),
                cancellationToken
            );
            await SaveChangesAsync(cancellationToken);
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
        await SaveChangesAsync(cancellationToken);
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
        return AddAuditAsync(
            AuditEvent.CreateSessionActionDenied(
                session.Id,
                now,
                JsonSerializer.Serialize(ToAuditPayload(session, command, reason))
            ),
            cancellationToken
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

    private static bool IsCommandShapeValid(ExecuteSessionActionCommand command)
    {
        if (command.IsSshAction)
        {
            return !string.IsNullOrWhiteSpace(command.Target)
                && !string.IsNullOrWhiteSpace(command.Action);
        }

        return !string.IsNullOrWhiteSpace(command.Capability);
    }

    private static SessionActionResult MapPolicyFailure(
        SshActionPolicyFailureReason failureReason,
        string reason
    )
    {
        return failureReason switch
        {
            SshActionPolicyFailureReason.InvalidParameter => SessionActionResult.ValidationFailed(
                reason
            ),
            SshActionPolicyFailureReason.InvalidConfiguration => SessionActionResult.Conflicted(
                reason
            ),
            _ => SessionActionResult.Forbidden(reason),
        };
    }

    private static string SanitizePolicyFailure(SshActionPolicyFailureReason failureReason)
    {
        return failureReason switch
        {
            SshActionPolicyFailureReason.InvalidParameter => "SSH action parameters are invalid.",
            SshActionPolicyFailureReason.InvalidConfiguration =>
                "SSH action is not configured correctly.",
            _ => "SSH action is not allowed for this session.",
        };
    }

    private static string SanitizeExecutionFailure(SshCommandExecutionFailureReason failureReason)
    {
        return failureReason switch
        {
            SshCommandExecutionFailureReason.Timeout => "SSH command timed out.",
            SshCommandExecutionFailureReason.ConnectionFailed => "SSH connection failed.",
            SshCommandExecutionFailureReason.AuthenticationFailed => "SSH authentication failed.",
            SshCommandExecutionFailureReason.UnknownTarget => "SSH target is not configured.",
            SshCommandExecutionFailureReason.InvalidResolvedCommand =>
                "SSH command is not configured correctly.",
            _ => "SSH action execution failed.",
        };
    }

    private static object ToSshResult(SshCommandOutput output)
    {
        return new
        {
            exitCode = output.ExitCode,
            stdout = output.Stdout,
            stderr = output.Stderr,
            stdoutTruncated = output.StdoutTruncated,
            stderrTruncated = output.StderrTruncated,
        };
    }

    private static object ToAuditPayload(
        Session session,
        ExecuteSessionActionCommand command,
        string? reason
    )
    {
        return new
        {
            SessionId = session.Id,
            session.AccessRequestId,
            Capability = command.IsSshAction ? command.Action : command.Capability,
            Target = command.IsSshAction ? command.Target : null,
            Action = command.IsSshAction ? command.Action : null,
            Reason = reason,
        };
    }
}
