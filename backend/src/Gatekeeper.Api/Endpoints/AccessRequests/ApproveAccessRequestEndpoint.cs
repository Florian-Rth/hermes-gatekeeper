using FastEndpoints;
using Gatekeeper.Api.AdminTokens;
using Gatekeeper.Application.AccessRequests;
using Gatekeeper.Core.AccessRequests;

namespace Gatekeeper.Api.Endpoints.AccessRequests;

public sealed class ApproveAccessRequestEndpoint
    : Endpoint<ApproveAccessRequestRequest, ApproveAccessRequestResponse>
{
    private readonly IAccessRequestService _accessRequestService;
    private readonly IAdminTokenValidator _adminTokenValidator;

    public ApproveAccessRequestEndpoint(
        IAccessRequestService accessRequestService,
        IAdminTokenValidator adminTokenValidator
    )
    {
        _accessRequestService = accessRequestService;
        _adminTokenValidator = adminTokenValidator;
    }

    public override void Configure()
    {
        Post("/api/v1/access-requests/{id}/approve");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ApproveAccessRequestRequest req, CancellationToken ct)
    {
        AdminTokenValidationResult tokenResult = _adminTokenValidator.Validate(
            HttpContext.Request.Headers
        );
        if (tokenResult == AdminTokenValidationResult.MissingHeader)
        {
            await Send.StringAsync(
                string.Empty,
                StatusCodes.Status401Unauthorized,
                cancellation: ct
            );
            return;
        }

        if (tokenResult == AdminTokenValidationResult.Forbidden)
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
