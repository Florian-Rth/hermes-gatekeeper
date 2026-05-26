using System.Diagnostics;
using System.Text;
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
            await AddDeniedAuditAsync(
                session,
                command,
                now,
                reason,
                command.IsSshAction ? ToInactiveSessionReasonCode(session) : null,
                null,
                cancellationToken
            );
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
            await AddDeniedAuditAsync(
                session,
                command,
                now,
                reason,
                command.IsSshAction ? "session_expired" : null,
                null,
                cancellationToken
            );
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
            await AddDeniedAuditAsync(
                session,
                command,
                now,
                reason,
                "target_not_allowed",
                null,
                cancellationToken
            );
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
            await AddDeniedAuditAsync(
                session,
                command,
                now,
                reason,
                ToReasonCode(policyResult.FailureReason),
                null,
                cancellationToken
            );
            await SaveChangesAsync(cancellationToken);
            return MapPolicyFailure(policyResult.FailureReason, reason);
        }

        SshAuditDetails allowedAuditDetails = CreateSshAllowedAuditDetails(
            policyResult.ResolvedAction!
        );
        bool reserved = await TryReserveActionSlotAsync(
            session,
            command,
            now,
            "none",
            allowedAuditDetails,
            cancellationToken
        );
        if (!reserved)
        {
            return await HandleReservationFailureAsync(
                session,
                command,
                now,
                allowedAuditDetails,
                cancellationToken
            );
        }

        SshCommandExecutionResult executionResult;
        long startedAt = Stopwatch.GetTimestamp();
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

        long durationMilliseconds = (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

        if (!executionResult.Succeeded)
        {
            string reason = SanitizeExecutionFailure(executionResult.FailureReason);
            await AddAuditAsync(
                AuditEvent.CreateSessionActionFailed(
                    session.Id,
                    now,
                    JsonSerializer.Serialize(
                        ToAuditPayload(
                            session,
                            command,
                            reason,
                            ToReasonCode(executionResult.FailureReason),
                            CreateSshAuditDetails(
                                policyResult.ResolvedAction,
                                executionResult,
                                durationMilliseconds
                            )
                        )
                    )
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
                JsonSerializer.Serialize(
                    ToAuditPayload(
                        session,
                        command,
                        null,
                        "none",
                        CreateSshAuditDetails(
                            policyResult.ResolvedAction,
                            executionResult,
                            durationMilliseconds
                        )
                    )
                )
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

        bool reserved = await TryReserveActionSlotAsync(
            session,
            command,
            now,
            null,
            null,
            cancellationToken
        );
        if (!reserved)
        {
            return await HandleReservationFailureAsync(
                session,
                command,
                now,
                null,
                cancellationToken
            );
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
        string? reasonCode,
        SshAuditDetails? sshDetails,
        CancellationToken cancellationToken
    )
    {
        AuditEvent allowedAuditEvent = AuditEvent.CreateSessionActionAllowed(
            session.Id,
            now,
            JsonSerializer.Serialize(ToAuditPayload(session, command, null, reasonCode, sshDetails))
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
        SshAuditDetails? sshDetails,
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
                    JsonSerializer.Serialize(
                        ToAuditPayload(
                            latest,
                            command,
                            reason,
                            command.IsSshAction ? "action_count_exceeded" : null,
                            sshDetails
                        )
                    )
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
            command.IsSshAction ? ToInactiveSessionReasonCode(latest ?? session) : null,
            null,
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
        return AddDeniedAuditAsync(session, command, now, reason, null, null, cancellationToken);
    }

    private Task AddDeniedAuditAsync(
        Session session,
        ExecuteSessionActionCommand command,
        DateTimeOffset now,
        string reason,
        string? reasonCode,
        SshAuditDetails? sshDetails,
        CancellationToken cancellationToken
    )
    {
        return AddAuditAsync(
            AuditEvent.CreateSessionActionDenied(
                session.Id,
                now,
                JsonSerializer.Serialize(
                    ToAuditPayload(session, command, reason, reasonCode, sshDetails)
                )
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

    private static string ToReasonCode(SshActionPolicyFailureReason failureReason)
    {
        return failureReason switch
        {
            SshActionPolicyFailureReason.UnknownTarget => "target_not_allowed",
            SshActionPolicyFailureReason.UnknownAction => "action_not_allowed",
            SshActionPolicyFailureReason.MissingProfileMembership => "profile_not_allowed",
            SshActionPolicyFailureReason.InvalidParameter => "invalid_parameter",
            SshActionPolicyFailureReason.InvalidConfiguration => "invalid_configuration",
            _ => "ssh_policy_denied",
        };
    }

    private static string ToReasonCode(SshCommandExecutionFailureReason failureReason)
    {
        return failureReason switch
        {
            SshCommandExecutionFailureReason.Timeout => "ssh_execution_timeout",
            SshCommandExecutionFailureReason.ConnectionFailed => "ssh_execution_connection_failed",
            SshCommandExecutionFailureReason.AuthenticationFailed =>
                "ssh_execution_authentication_failed",
            SshCommandExecutionFailureReason.UnknownTarget => "ssh_execution_unknown_target",
            SshCommandExecutionFailureReason.InvalidResolvedCommand =>
                "ssh_execution_invalid_resolved_command",
            SshCommandExecutionFailureReason.ClientFailed => "ssh_execution_client_failed",
            _ => "ssh_execution_failed",
        };
    }

    private static string ToInactiveSessionReasonCode(Session session)
    {
        return session.Status == SessionStatus.Expired ? "session_expired" : "session_inactive";
    }

    private static SshAuditDetails CreateSshAllowedAuditDetails(SshResolvedAction resolvedAction)
    {
        return new SshAuditDetails(
            resolvedAction.SafeParameters,
            null,
            null,
            false,
            false,
            false,
            null,
            null
        );
    }

    private static SshAuditDetails CreateSshAuditDetails(
        SshResolvedAction? resolvedAction,
        SshCommandExecutionResult executionResult,
        long durationMilliseconds
    )
    {
        SshCommandOutput? output = executionResult.Output;
        return new SshAuditDetails(
            resolvedAction?.SafeParameters,
            output?.ExitCode,
            durationMilliseconds,
            executionResult.FailureReason == SshCommandExecutionFailureReason.Timeout,
            output?.StdoutTruncated ?? false,
            output?.StderrTruncated ?? false,
            output is null ? null : Encoding.UTF8.GetByteCount(output.Stdout),
            output is null ? null : Encoding.UTF8.GetByteCount(output.Stderr)
        );
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
        string? reason,
        string? reasonCode = null,
        SshAuditDetails? sshDetails = null
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
                SafeParameters = sshDetails?.SafeParameters,
                ExitStatus = sshDetails?.ExitStatus,
                DurationMs = sshDetails?.DurationMilliseconds,
                TimedOut = sshDetails?.TimedOut,
                StdoutTruncated = sshDetails?.StdoutTruncated,
                StderrTruncated = sshDetails?.StderrTruncated,
                Output = sshDetails?.Output,
                Reason = reason,
                ReasonCode = reasonCode,
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
            SafeParameters = (IReadOnlyDictionary<string, string>?)null,
            ExitStatus = (int?)null,
            DurationMs = (long?)null,
            TimedOut = (bool?)null,
            StdoutTruncated = (bool?)null,
            StderrTruncated = (bool?)null,
            Output = (SshOutputAuditMetadata?)null,
            Reason = reason,
            ReasonCode = reasonCode,
        };
    }

    private sealed class SshAuditDetails
    {
        public SshAuditDetails(
            IReadOnlyDictionary<string, string>? safeParameters,
            int? exitStatus,
            long? durationMilliseconds,
            bool timedOut,
            bool stdoutTruncated,
            bool stderrTruncated,
            int? stdoutBytes,
            int? stderrBytes
        )
        {
            SafeParameters = safeParameters is null
                ? null
                : new Dictionary<string, string>(safeParameters, StringComparer.Ordinal);
            ExitStatus = exitStatus;
            DurationMilliseconds = durationMilliseconds;
            TimedOut = timedOut;
            StdoutTruncated = stdoutTruncated;
            StderrTruncated = stderrTruncated;
            Output =
                stdoutBytes.HasValue || stderrBytes.HasValue
                    ? new SshOutputAuditMetadata(stdoutBytes, stderrBytes)
                    : null;
        }

        public IReadOnlyDictionary<string, string>? SafeParameters { get; }

        public int? ExitStatus { get; }

        public long? DurationMilliseconds { get; }

        public bool TimedOut { get; }

        public bool StdoutTruncated { get; }

        public bool StderrTruncated { get; }

        public SshOutputAuditMetadata? Output { get; }
    }

    private sealed class SshOutputAuditMetadata
    {
        public SshOutputAuditMetadata(int? stdoutBytes, int? stderrBytes)
        {
            StdoutBytes = stdoutBytes;
            StderrBytes = stderrBytes;
        }

        public int? StdoutBytes { get; }

        public int? StderrBytes { get; }
    }
}
