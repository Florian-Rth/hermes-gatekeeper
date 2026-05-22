using FastEndpoints;
using Gatekeeper.Application.AccessRequests;
using Gatekeeper.Core.AccessRequests;

namespace Gatekeeper.Api.Endpoints.AccessRequests;

public sealed class GetAccessRequestEndpoint
    : Endpoint<GetAccessRequestRequest, GetAccessRequestResponse>
{
    private readonly IAccessRequestService _accessRequestService;

    public GetAccessRequestEndpoint(IAccessRequestService accessRequestService)
    {
        _accessRequestService = accessRequestService;
    }

    public override void Configure()
    {
        Get("/api/v1/access-requests/{id}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetAccessRequestRequest req, CancellationToken ct)
    {
        AccessRequestDetails? accessRequest = await _accessRequestService.GetByIdAsync(req.Id, ct);
        if (accessRequest is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(GetAccessRequestResponse.FromDetails(accessRequest), ct);
    }
}

public sealed class GetAccessRequestRequest
{
    public Guid Id { get; set; }
}

public sealed class GetAccessRequestResponse
{
    public Guid Id { get; set; }

    public string Intent { get; set; } = string.Empty;

    public string Requester { get; set; } = string.Empty;

    public IReadOnlyList<string> Targets { get; set; } = [];

    public IReadOnlyList<string> RequestedCapabilities { get; set; } = [];

    public int DurationMinutes { get; set; }

    public RiskLevel Risk { get; set; }

    public string? Justification { get; set; }

    public IReadOnlyList<string> ProposedActions { get; set; } = [];

    public IReadOnlyList<string> ForbiddenActions { get; set; } = [];

    public IReadOnlyDictionary<string, string> Metadata { get; set; } =
        new Dictionary<string, string>();

    public AccessRequestStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public static GetAccessRequestResponse FromDetails(AccessRequestDetails accessRequest)
    {
        return new GetAccessRequestResponse
        {
            Id = accessRequest.Id,
            Intent = accessRequest.Intent,
            Requester = accessRequest.Requester,
            Targets = accessRequest.Targets,
            RequestedCapabilities = accessRequest.RequestedCapabilities,
            DurationMinutes = accessRequest.DurationMinutes,
            Risk = accessRequest.Risk,
            Justification = accessRequest.Justification,
            ProposedActions = accessRequest.ProposedActions,
            ForbiddenActions = accessRequest.ForbiddenActions,
            Metadata = accessRequest.Metadata,
            Status = accessRequest.Status,
            CreatedAt = accessRequest.CreatedAt,
            UpdatedAt = accessRequest.UpdatedAt,
        };
    }
}
