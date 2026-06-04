using System.Text.Json;
using Gatekeeper.Application.AuditEvents;
using Gatekeeper.Application.Common;
using Gatekeeper.Application.Sessions;
using Gatekeeper.Core.AccessRequests;
using Gatekeeper.Core.Sessions;

namespace Gatekeeper.Application.AccessRequests;

public sealed class AccessRequestService : IAccessRequestService
{
    private readonly IAccessRequestRepository _accessRequests;
    private readonly ISessionRepository _sessions;
    private readonly IAccessRequestUnitOfWork _unitOfWork;
    private readonly IAuditEventRepository _auditEvents;
    private readonly IClock _clock;
    private readonly SessionLifecycleOptions _sessionLifecycleOptions;
    private readonly ISshApprovalCatalogValidator? _sshApprovalCatalogValidator;

    public AccessRequestService(
        IAccessRequestRepository accessRequests,
        ISessionRepository sessions,
        IAccessRequestUnitOfWork unitOfWork,
        IAuditEventRepository auditEvents,
        IClock clock,
        SessionLifecycleOptions? sessionLifecycleOptions = null,
        ISshApprovalCatalogValidator? sshApprovalCatalogValidator = null
    )
    {
        _accessRequests = accessRequests;
        _sessions = sessions;
        _unitOfWork = unitOfWork;
        _auditEvents = auditEvents;
        _clock = clock;
        _sessionLifecycleOptions = sessionLifecycleOptions ?? SessionLifecycleOptions.Default;
        _sshApprovalCatalogValidator = sshApprovalCatalogValidator;
    }

    public async Task<AccessRequestDetails> CreateAsync(
        CreateAccessRequestCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = _clock.UtcNow;
        var accessRequest = AccessRequest.Create(
            command.Intent,
            command.Requester,
            command.Targets,
            command.RequestedCapabilities,
            command.DurationMinutes,
            command.Risk,
            command.Justification,
            command.ProposedActions,
            command.ForbiddenActions,
            command.Metadata,
            now
        );

        await _accessRequests.AddAsync(accessRequest, cancellationToken);

        AccessRequestDetails details = ToDetails(accessRequest);
        object fullPayload = ToAuditPayload(details, null, command.Agent);
        IReadOnlyDictionary<string, string> boundedDetails = ProjectAccessRequestDetails(
            details,
            null,
            command.Agent
        );
        var auditEvent = AuditEvent.CreateAccessRequestCreated(
            accessRequest.Id,
            now,
            JsonSerializer.Serialize(fullPayload),
            boundedDetails
        );
        await _auditEvents.AddAsync(auditEvent, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return details;
    }

    public async Task<ApprovalResult> ApproveAsync(
        ApproveAccessRequestCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var accessRequest = await _accessRequests.GetByIdAsync(
            command.AccessRequestId,
            cancellationToken
        );
        if (accessRequest is null)
        {
            return ApprovalResult.Missing();
        }

        if (accessRequest.Status != AccessRequestStatus.Pending)
        {
            return ApprovalResult.Conflicted();
        }

        var now = _clock.UtcNow;
        var approved = accessRequest.Approve(now);

        IReadOnlyList<SshProfileGrant> sshGrants = _sshApprovalCatalogValidator is not null
            ? await _sshApprovalCatalogValidator.ResolveGrantsAsync(approved, cancellationToken)
            : [];

        var session = Session.CreateFromApprovedAccessRequest(
            approved,
            now,
            _sessionLifecycleOptions.MaxActionCount,
            sshGrants
        );

        await _accessRequests.UpdateAsync(approved, cancellationToken);
        await _sessions.AddAsync(session, cancellationToken);

        AccessRequestDetails approvedDetails = ToDetails(approved);
        await _auditEvents.AddAsync(
            AuditEvent.CreateAccessRequestApproved(
                approved.Id,
                now,
                JsonSerializer.Serialize(ToAuditPayload(approvedDetails, command.Comment)),
                ProjectAccessRequestDetails(approvedDetails, command.Comment)
            ),
            cancellationToken
        );

        SessionDetails sessionDetails = ToSessionDetails(session);
        await _auditEvents.AddAsync(
            AuditEvent.CreateSessionCreated(
                session.Id,
                approved.Id,
                now,
                JsonSerializer.Serialize(ToAuditPayload(sessionDetails, command.Comment)),
                ProjectSessionDetails(sessionDetails, command.Comment)
            ),
            cancellationToken
        );
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (PersistenceConflictException)
        {
            return ApprovalResult.Conflicted();
        }

        return ApprovalResult.Succeeded(approvedDetails, sessionDetails);
    }

    public async Task<DenialResult> DenyAsync(
        DenyAccessRequestCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var accessRequest = await _accessRequests.GetByIdAsync(
            command.AccessRequestId,
            cancellationToken
        );
        if (accessRequest is null)
        {
            return DenialResult.Missing();
        }

        if (accessRequest.Status != AccessRequestStatus.Pending)
        {
            return DenialResult.Conflicted();
        }

        var now = _clock.UtcNow;
        var denied = accessRequest.Deny(now);

        await _accessRequests.UpdateAsync(denied, cancellationToken);

        AccessRequestDetails deniedDetails = ToDetails(denied);
        await _auditEvents.AddAsync(
            AuditEvent.CreateAccessRequestDenied(
                denied.Id,
                now,
                JsonSerializer.Serialize(ToAuditPayload(deniedDetails, command.Comment)),
                ProjectAccessRequestDetails(deniedDetails, command.Comment)
            ),
            cancellationToken
        );
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (PersistenceConflictException)
        {
            return DenialResult.Conflicted();
        }

        return DenialResult.Succeeded(deniedDetails);
    }

    public async Task<AccessRequestDetails?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken
    )
    {
        var accessRequest = await _accessRequests.GetByIdAsync(id, cancellationToken);
        if (accessRequest is null)
        {
            return null;
        }

        return ToDetails(accessRequest);
    }

    public async Task<IReadOnlyList<AccessRequestSummary>> ListAsync(
        CancellationToken cancellationToken
    )
    {
        var accessRequests = await _accessRequests.ListAsync(cancellationToken);
        return accessRequests
            .OrderByDescending(accessRequest => accessRequest.CreatedAt)
            .Select(ToSummary)
            .ToArray();
    }

    private static AccessRequestDetails ToDetails(AccessRequest accessRequest)
    {
        return new AccessRequestDetails(
            accessRequest.Id,
            accessRequest.Intent,
            accessRequest.Requester,
            accessRequest.Targets,
            accessRequest.RequestedCapabilities,
            accessRequest.DurationMinutes,
            accessRequest.Risk,
            accessRequest.Justification,
            accessRequest.ProposedActions,
            accessRequest.ForbiddenActions,
            accessRequest.Metadata,
            accessRequest.Status,
            accessRequest.CreatedAt,
            accessRequest.UpdatedAt
        );
    }

    private static SessionDetails ToSessionDetails(Session session)
    {
        return new SessionDetails(
            session.Id,
            session.AccessRequestId,
            session.Status,
            session.AllowedTargets,
            session.AllowedCapabilities,
            session.CreatedAt,
            session.ExpiresAt,
            session.ActionCount,
            session.MaxActionCount,
            session.CompletedAt,
            session.RevokedAt,
            session.ExpiredAt
        );
    }

    private static object ToAuditPayload(
        object details,
        string? comment,
        AuthenticatedAgent? agent = null
    )
    {
        return new
        {
            Details = details,
            Comment = comment,
            AgentId = agent?.AgentId,
            AuthMethod = agent?.AuthMethod,
        };
    }

    private static IReadOnlyDictionary<string, string> ProjectAccessRequestDetails(
        AccessRequestDetails details,
        string? comment,
        AuthenticatedAgent? agent = null
    )
    {
        return AuditDetailProjector.Project(
            new Dictionary<string, object?>
            {
                ["requester"] = details.Requester,
                ["status"] = details.Status.ToString(),
                ["risk"] = details.Risk.ToString(),
                ["comment"] = comment,
                ["agentId"] = agent?.AgentId,
                ["authMethod"] = agent?.AuthMethod,
            }
        );
    }

    private static IReadOnlyDictionary<string, string> ProjectSessionDetails(
        SessionDetails details,
        string? comment
    )
    {
        return AuditDetailProjector.Project(
            new Dictionary<string, object?>
            {
                ["sessionId"] = details.Id,
                ["accessRequestId"] = details.AccessRequestId,
                ["status"] = details.Status.ToString(),
                ["comment"] = comment,
            }
        );
    }

    private static AccessRequestSummary ToSummary(AccessRequest accessRequest)
    {
        return new AccessRequestSummary(
            accessRequest.Id,
            accessRequest.Intent,
            accessRequest.Requester,
            accessRequest.Targets,
            accessRequest.RequestedCapabilities,
            accessRequest.DurationMinutes,
            accessRequest.Risk,
            accessRequest.Status,
            accessRequest.CreatedAt,
            accessRequest.UpdatedAt
        );
    }
}
