using FastEndpoints;
using Gatekeeper.Api.AdminTokens;
using Gatekeeper.Application.AccessRequests;
using Gatekeeper.Core.AccessRequests;

namespace Gatekeeper.Api.Endpoints.AccessRequests;

public sealed class DenyAccessRequestEndpoint
    : Endpoint<DenyAccessRequestRequest, DenyAccessRequestResponse>
{
    private readonly IAccessRequestService _accessRequestService;
    private readonly IAdminTokenValidator _adminTokenValidator;

    public DenyAccessRequestEndpoint(
        IAccessRequestService accessRequestService,
        IAdminTokenValidator adminTokenValidator
    )
    {
        _accessRequestService = accessRequestService;
        _adminTokenValidator = adminTokenValidator;
    }

    public override void Configure()
    {
        Post("/api/v1/access-requests/{id}/deny");
        AllowAnonymous();
    }

    public override async Task HandleAsync(DenyAccessRequestRequest req, CancellationToken ct)
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
