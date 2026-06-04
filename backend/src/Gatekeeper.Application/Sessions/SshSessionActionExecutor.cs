using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Gatekeeper.Application.AccessRequests;
using Gatekeeper.Application.Common;
using Gatekeeper.Core.AccessRequests;
using Gatekeeper.Core.Sessions;

namespace Gatekeeper.Application.Sessions;

public sealed class SshSessionActionExecutor
{
    private readonly ISshActionPolicy _sshActionPolicy;
    private readonly ISshCommandExecutor _sshCommandExecutor;
    private readonly ISessionRepository _sessions;
    private readonly IAuditEventRepository _auditEvents;
    private readonly ISessionActionUnitOfWork _unitOfWork;

    public SshSessionActionExecutor(
        ISshActionPolicy sshActionPolicy,
        ISshCommandExecutor sshCommandExecutor,
        ISessionRepository sessions,
        IAuditEventRepository auditEvents,
        ISessionActionUnitOfWork unitOfWork
    )
    {
        _sshActionPolicy = sshActionPolicy;
        _sshCommandExecutor = sshCommandExecutor;
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
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return SessionActionResult.Forbidden(reason);
        }

        IReadOnlyCollection<SshApprovedProfileGrant> grants = session
            .SshProfileGrants.Select(grant => new SshApprovedProfileGrant(
                grant.TargetAlias,
                grant.ProfileName
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
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return MapPolicyFailure(policyResult.FailureReason, reason);
        }

        SshAuditDetails allowedAuditDetails = CreateAllowedAuditDetails(
            policyResult.ResolvedAction!
        );
        bool reserved = await TryReserveActionSlotAsync(
            session,
            command,
            now,
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
            await _auditEvents.AddAsync(
                AuditEvent.CreateSessionActionFailed(
                    session.Id,
                    now,
                    JsonSerializer.Serialize(
                        BuildAuditPayload(
                            session,
                            command,
                            reason,
                            ToReasonCode(executionResult.FailureReason),
                            CreateExecutionAuditDetails(
                                policyResult.ResolvedAction,
                                executionResult,
                                durationMilliseconds
                            )
                        )
                    )
                ),
                cancellationToken
            );
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return SessionActionResult.Conflicted(reason);
        }

        await _auditEvents.AddAsync(
            AuditEvent.CreateSessionActionExecuted(
                session.Id,
                now,
                JsonSerializer.Serialize(
                    BuildAuditPayload(
                        session,
                        command,
                        null,
                        "none",
                        CreateExecutionAuditDetails(
                            policyResult.ResolvedAction,
                            executionResult,
                            durationMilliseconds
                        )
                    )
                )
            ),
            cancellationToken
        );
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return SessionActionResult.Succeeded(
            new SessionActionExecution(
                session.Id,
                command.Action,
                "succeeded",
                JsonSerializer.SerializeToElement(
                    ToSshResult(policyResult.ResolvedAction!, executionResult.Output!)
                )
            )
        );
    }

    private async Task<bool> TryReserveActionSlotAsync(
        Session session,
        ExecuteSessionActionCommand command,
        DateTimeOffset now,
        SshAuditDetails sshDetails,
        CancellationToken cancellationToken
    )
    {
        AuditEvent allowedAuditEvent = AuditEvent.CreateSessionActionAllowed(
            session.Id,
            now,
            JsonSerializer.Serialize(BuildAuditPayload(session, command, null, "none", sshDetails))
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
        SshAuditDetails sshDetails,
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
                    JsonSerializer.Serialize(
                        BuildAuditPayload(
                            latest,
                            command,
                            reason,
                            "action_count_exceeded",
                            sshDetails
                        )
                    )
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
            ToInactiveSessionReasonCode(latest ?? session),
            null,
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
        string? reasonCode,
        SshAuditDetails? sshDetails,
        CancellationToken cancellationToken
    )
    {
        return _auditEvents.AddAsync(
            AuditEvent.CreateSessionActionDenied(
                session.Id,
                now,
                JsonSerializer.Serialize(
                    BuildAuditPayload(session, command, reason, reasonCode, sshDetails)
                )
            ),
            cancellationToken
        );
    }

    private static object BuildAuditPayload(
        Session session,
        ExecuteSessionActionCommand command,
        string? reason,
        string? reasonCode = null,
        SshAuditDetails? sshDetails = null
    )
    {
        return new
        {
            SessionId = session.Id,
            session.AccessRequestId,
            TargetAlias = command.Target,
            Action = command.Action,
            IsMutating = sshDetails?.IsMutating,
            Risk = sshDetails?.Risk,
            SafeParameters = sshDetails?.SafeParameters,
            ExitStatus = sshDetails?.ExitStatus,
            DurationMs = sshDetails?.DurationMilliseconds,
            TimedOut = sshDetails?.TimedOut,
            StdoutTruncated = sshDetails?.StdoutTruncated,
            StderrTruncated = sshDetails?.StderrTruncated,
            Output = sshDetails?.Output,
            Reason = reason,
            ReasonCode = reasonCode,
            AgentId = command.Agent?.AgentId,
            AuthMethod = command.Agent?.AuthMethod,
        };
    }

    internal static SessionActionResult MapPolicyFailure(
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

    internal static string SanitizePolicyFailure(SshActionPolicyFailureReason failureReason)
    {
        return failureReason switch
        {
            SshActionPolicyFailureReason.InvalidParameter => "SSH action parameters are invalid.",
            SshActionPolicyFailureReason.InvalidConfiguration =>
                "SSH action is not configured correctly.",
            _ => "SSH action is not allowed for this session.",
        };
    }

    internal static string SanitizeExecutionFailure(SshCommandExecutionFailureReason failureReason)
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

    internal static string ToReasonCode(SshActionPolicyFailureReason failureReason)
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

    internal static string ToReasonCode(SshCommandExecutionFailureReason failureReason)
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

    private static SshAuditDetails CreateAllowedAuditDetails(SshResolvedAction resolvedAction)
    {
        return new SshAuditDetails(
            resolvedAction.SafeParameters,
            null,
            null,
            false,
            false,
            false,
            null,
            null,
            resolvedAction.IsMutating,
            resolvedAction.Risk.ToString()
        );
    }

    private static SshAuditDetails CreateExecutionAuditDetails(
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
            output is null ? null : Encoding.UTF8.GetByteCount(output.Stderr),
            resolvedAction?.IsMutating,
            resolvedAction?.Risk.ToString()
        );
    }

    private static object ToSshResult(SshResolvedAction resolvedAction, SshCommandOutput output)
    {
        return new
        {
            exitCode = output.ExitCode,
            stdout = output.Stdout,
            stderr = output.Stderr,
            stdoutTruncated = output.StdoutTruncated,
            stderrTruncated = output.StderrTruncated,
            isMutating = resolvedAction.IsMutating,
            risk = resolvedAction.Risk.ToString(),
        };
    }

    internal sealed class SshAuditDetails
    {
        public SshAuditDetails(
            IReadOnlyDictionary<string, string>? safeParameters,
            int? exitStatus,
            long? durationMilliseconds,
            bool timedOut,
            bool stdoutTruncated,
            bool stderrTruncated,
            int? stdoutBytes,
            int? stderrBytes,
            bool? isMutating,
            string? risk
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
            IsMutating = isMutating;
            Risk = risk;
        }

        public IReadOnlyDictionary<string, string>? SafeParameters { get; }

        public int? ExitStatus { get; }

        public long? DurationMilliseconds { get; }

        public bool TimedOut { get; }

        public bool StdoutTruncated { get; }

        public bool StderrTruncated { get; }

        public SshOutputAuditMetadata? Output { get; }

        public bool? IsMutating { get; }

        public string? Risk { get; }
    }

    internal sealed class SshOutputAuditMetadata
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
