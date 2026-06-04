using FastEndpoints;
using FluentValidation;
using Gatekeeper.Api.AgentAuthentication;
using Gatekeeper.Application.AccessRequests;
using Gatekeeper.Application.Common;
using Gatekeeper.Core.AccessRequests;

namespace Gatekeeper.Api.Endpoints.AccessRequests;

public sealed class CreateAccessRequestEndpoint
    : Endpoint<CreateAccessRequestRequest, CreateAccessRequestResponse>
{
    private readonly IAccessRequestService _accessRequestService;

    public CreateAccessRequestEndpoint(IAccessRequestService accessRequestService)
    {
        _accessRequestService = accessRequestService;
    }

    public override void Configure()
    {
        Post("/api/v1/access-requests");
        AuthSchemes(AgentAuthConstants.Scheme);
    }

    public override async Task HandleAsync(CreateAccessRequestRequest req, CancellationToken ct)
    {
        AgentIdentity agentIdentity = AgentIdentity.FromPrincipal(HttpContext.User)!;

        CreateAccessRequestCommand command = new CreateAccessRequestCommand(
            req.Intent,
            req.Requester,
            req.Targets,
            req.RequestedCapabilities,
            req.DurationMinutes,
            req.Risk,
            req.Justification,
            req.ProposedActions,
            req.ForbiddenActions,
            req.Metadata,
            new AuthenticatedAgent(agentIdentity.AgentId, agentIdentity.AuthMethod)
        );

        AccessRequestDetails accessRequest = await _accessRequestService.CreateAsync(command, ct);
        CreateAccessRequestResponse response = new CreateAccessRequestResponse
        {
            Id = accessRequest.Id,
            Status = accessRequest.Status,
            ApprovalUrl = null,
        };

        HttpContext.Response.Headers.Location = $"/api/v1/access-requests/{accessRequest.Id}";
        await Send.ResponseAsync(response, StatusCodes.Status201Created, ct);
    }
}

public sealed class CreateAccessRequestRequest
{
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
}

public sealed class CreateAccessRequestValidator : Validator<CreateAccessRequestRequest>
{
    public CreateAccessRequestValidator()
    {
        RuleFor(request => request.Intent).NotEmpty();
        RuleFor(request => request.Requester).NotEmpty();
        RuleFor(request => request.Targets).NotEmpty();
        RuleForEach(request => request.Targets).Must(value => !string.IsNullOrWhiteSpace(value));
        RuleFor(request => request.RequestedCapabilities).NotEmpty();
        RuleForEach(request => request.RequestedCapabilities)
            .Must(value => !string.IsNullOrWhiteSpace(value));
        RuleFor(request => request.DurationMinutes).GreaterThan(0);
        RuleFor(request => request.Risk).IsInEnum();
    }
}

public sealed class CreateAccessRequestResponse
{
    public Guid Id { get; set; }

    public AccessRequestStatus Status { get; set; }

    public string? ApprovalUrl { get; set; }
}
