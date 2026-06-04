using FastEndpoints;
using Gatekeeper.Api.AdminAuthentication;
using Gatekeeper.Application.AccessRequests;
using Gatekeeper.Core.AccessRequests;

namespace Gatekeeper.Api.Endpoints.AccessRequests;

public sealed class ApproveAccessRequestEndpoint
    : Endpoint<ApproveAccessRequestRequest, ApproveAccessRequestResponse>
{
    private readonly IAccessRequestService _accessRequestService;
    private readonly AdminSessionGuard _adminSessionGuard;

    public ApproveAccessRequestEndpoint(
        IAccessRequestService accessRequestService,
        AdminSessionGuard adminSessionGuard
    )
    {
        _accessRequestService = accessRequestService;
        _adminSessionGuard = adminSessionGuard;
    }

    public override void Configure()
    {
        Post("/api/v1/access-requests/{id}/approve");
        AuthSchemes(AdminAuthConstants.Scheme);
    }

    public override async Task HandleAsync(ApproveAccessRequestRequest req, CancellationToken ct)
    {
        if (!_adminSessionGuard.HasValidUnsafeRequestOrigin(HttpContext))
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        ApprovalResult result = await _accessRequestService.ApproveAsync(
            new ApproveAccessRequestCommand(req.Id, req.Comment),
            ct
        );
        if (result.NotFound)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (result.Conflict)
        {
            await Send.StringAsync(string.Empty, StatusCodes.Status409Conflict, cancellation: ct);
            return;
        }

        if (result.AccessRequest is null || result.Session is null)
        {
            await Send.StringAsync(string.Empty, StatusCodes.Status409Conflict, cancellation: ct);
            return;
        }

        ApproveAccessRequestResponse response = new ApproveAccessRequestResponse
        {
            AccessRequestId = result.AccessRequest.Id,
            Status = result.AccessRequest.Status,
            SessionId = result.Session.Id,
            ExpiresAt = result.Session.ExpiresAt,
        };
        await Send.OkAsync(response, ct);
    }
}

public sealed class ApproveAccessRequestRequest
{
    public Guid Id { get; set; }

    public string? Comment { get; set; }
}

public sealed class ApproveAccessRequestResponse
{
    public Guid AccessRequestId { get; set; }

    public AccessRequestStatus Status { get; set; }

    public Guid SessionId { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
}
