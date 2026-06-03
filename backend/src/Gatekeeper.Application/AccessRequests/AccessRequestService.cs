using System.Text.Json;
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

        var auditEvent = AuditEvent.CreateAccessRequestCreated(
            accessRequest.Id,
            now,
            JsonSerializer.Serialize(ToAuditPayload(ToDetails(accessRequest), null, command.Agent))
        );
        await _auditEvents.AddAsync(auditEvent, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDetails(accessRequest);
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
        if (
            _sshApprovalCatalogValidator is not null
            && !await _sshApprovalCatalogValidator.CanCreateSessionForApprovedRequestAsync(
                approved,
                cancellationToken
            )
        )
        {
            return ApprovalResult.Conflicted();
        }

        var session = Session.CreateFromApprovedAccessRequest(
            approved,
            now,
            _sessionLifecycleOptions.MaxActionCount
        );

        await _accessRequests.UpdateAsync(approved, cancellationToken);
        await _sessions.AddAsync(session, cancellationToken);
        await _auditEvents.AddAsync(
            AuditEvent.CreateAccessRequestApproved(
                approved.Id,
                now,
                JsonSerializer.Serialize(ToAuditPayload(ToDetails(approved), command.Comment))
            ),
            cancellationToken
        );
        await _auditEvents.AddAsync(
            AuditEvent.CreateSessionCreated(
                session.Id,
                approved.Id,
                now,
                JsonSerializer.Serialize(ToAuditPayload(ToSessionDetails(session), command.Comment))
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

        return ApprovalResult.Succeeded(ToDetails(approved), ToSessionDetails(session));
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
        await _auditEvents.AddAsync(
            AuditEvent.CreateAccessRequestDenied(
                denied.Id,
                now,
                JsonSerializer.Serialize(ToAuditPayload(ToDetails(denied), command.Comment))
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

        return DenialResult.Succeeded(ToDetails(denied));
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
