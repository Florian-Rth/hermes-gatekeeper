using FastEndpoints;
using Gatekeeper.Application.AccessRequests;
using Gatekeeper.Core.AccessRequests;

namespace Gatekeeper.Api.Endpoints.AccessRequests;

public sealed class ListAccessRequestsEndpoint : EndpointWithoutRequest<ListAccessRequestsResponse>
{
    private readonly IAccessRequestService _accessRequestService;

    public ListAccessRequestsEndpoint(IAccessRequestService accessRequestService)
    {
        _accessRequestService = accessRequestService;
    }

    public override void Configure()
    {
        Get("/api/v1/access-requests");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        IReadOnlyList<AccessRequestSummary> accessRequests = await _accessRequestService.ListAsync(
            ct
        );
        ListAccessRequestsResponse response = new ListAccessRequestsResponse
        {
            Items = accessRequests.Select(ListAccessRequestItemResponse.FromSummary).ToArray(),
        };

        await Send.OkAsync(response, ct);
    }
}

public sealed class ListAccessRequestsResponse
{
    public IReadOnlyList<ListAccessRequestItemResponse> Items { get; set; } = [];
}

public sealed class ListAccessRequestItemResponse
{
    public Guid Id { get; set; }

    public string Intent { get; set; } = string.Empty;

    public string Requester { get; set; } = string.Empty;

    public IReadOnlyList<string> Targets { get; set; } = [];

    public IReadOnlyList<string> RequestedCapabilities { get; set; } = [];

    public int DurationMinutes { get; set; }

    public RiskLevel Risk { get; set; }

    public AccessRequestStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public static ListAccessRequestItemResponse FromSummary(AccessRequestSummary accessRequest)
    {
        return new ListAccessRequestItemResponse
        {
            Id = accessRequest.Id,
            Intent = accessRequest.Intent,
            Requester = accessRequest.Requester,
            Targets = accessRequest.Targets,
            RequestedCapabilities = accessRequest.RequestedCapabilities,
            DurationMinutes = accessRequest.DurationMinutes,
            Risk = accessRequest.Risk,
            Status = accessRequest.Status,
            CreatedAt = accessRequest.CreatedAt,
            UpdatedAt = accessRequest.UpdatedAt,
        };
    }
}
