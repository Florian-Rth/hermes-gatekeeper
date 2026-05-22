using System.Text.Json;
using Gatekeeper.Application.Common;
using Gatekeeper.Core.AccessRequests;

namespace Gatekeeper.Application.AccessRequests;

public sealed class AccessRequestService : IAccessRequestService
{
    private readonly IAccessRequestRepository _accessRequests;
    private readonly IAccessRequestUnitOfWork _unitOfWork;
    private readonly IAuditEventRepository _auditEvents;
    private readonly IClock _clock;

    public AccessRequestService(
        IAccessRequestRepository accessRequests,
        IAccessRequestUnitOfWork unitOfWork,
        IAuditEventRepository auditEvents,
        IClock clock
    )
    {
        _accessRequests = accessRequests;
        _unitOfWork = unitOfWork;
        _auditEvents = auditEvents;
        _clock = clock;
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
            JsonSerializer.Serialize(ToDetails(accessRequest))
        );
        await _auditEvents.AddAsync(auditEvent, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDetails(accessRequest);
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
