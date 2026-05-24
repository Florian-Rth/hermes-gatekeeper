using FastEndpoints;
using Gatekeeper.Api.AdminAuthentication;
using Gatekeeper.Application.AccessRequests;
using Gatekeeper.Core.AccessRequests;

namespace Gatekeeper.Api.Endpoints.AccessRequests;

public sealed class DenyAccessRequestEndpoint
    : Endpoint<DenyAccessRequestRequest, DenyAccessRequestResponse>
{
    private readonly IAccessRequestService _accessRequestService;
    private readonly AdminSessionGuard _adminSessionGuard;

    public DenyAccessRequestEndpoint(
        IAccessRequestService accessRequestService,
        AdminSessionGuard adminSessionGuard
    )
    {
        _accessRequestService = accessRequestService;
        _adminSessionGuard = adminSessionGuard;
    }

    public override void Configure()
    {
        Post("/api/v1/access-requests/{id}/deny");
        AllowAnonymous();
    }

    public override async Task HandleAsync(DenyAccessRequestRequest req, CancellationToken ct)
    {
        if (!_adminSessionGuard.IsAuthenticated(HttpContext))
        {
            await Send.StringAsync(
                string.Empty,
                StatusCodes.Status401Unauthorized,
                cancellation: ct
            );
            return;
        }
        if (!_adminSessionGuard.HasValidUnsafeRequestOrigin(HttpContext))
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        DenialResult result = await _accessRequestService.DenyAsync(
            new DenyAccessRequestCommand(req.Id, req.Comment),
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

        if (result.AccessRequest is null)
        {
            await Send.StringAsync(string.Empty, StatusCodes.Status409Conflict, cancellation: ct);
            return;
        }

        DenyAccessRequestResponse response = new DenyAccessRequestResponse
        {
            AccessRequestId = result.AccessRequest.Id,
            Status = result.AccessRequest.Status,
        };
        await Send.OkAsync(response, ct);
    }
}

public sealed class DenyAccessRequestRequest
{
    public Guid Id { get; set; }

    public string? Comment { get; set; }
}

public sealed class DenyAccessRequestResponse
{
    public Guid AccessRequestId { get; set; }

    public AccessRequestStatus Status { get; set; }
}
