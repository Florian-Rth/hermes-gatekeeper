using System.Text.Json;
using FastEndpoints;
using FluentValidation;
using Gatekeeper.Api.AgentAuthentication;
using Gatekeeper.Application.Common;
using Gatekeeper.Application.Sessions;

namespace Gatekeeper.Api.Endpoints.Sessions;

public sealed class ExecuteSessionActionEndpoint
    : Endpoint<ExecuteSessionActionRequest, ExecuteSessionActionResponse>
{
    private readonly ISessionActionService _sessionActions;

    public ExecuteSessionActionEndpoint(ISessionActionService sessionActions)
    {
        _sessionActions = sessionActions;
    }

    public override void Configure()
    {
        Post("/api/v1/sessions/{sessionId}/actions");
        AuthSchemes(AgentAuthConstants.Scheme);
    }

    public override async Task HandleAsync(ExecuteSessionActionRequest req, CancellationToken ct)
    {
        AgentIdentity agentIdentity = AgentIdentity.FromPrincipal(HttpContext.User)!;

        if (req.SessionId == Guid.Empty || !HasValidActionShape(req))
        {
            await Send.StringAsync(string.Empty, StatusCodes.Status400BadRequest, cancellation: ct);
            return;
        }

        SessionActionResult result = await _sessionActions.ExecuteAsync(
            new ExecuteSessionActionCommand(
                req.SessionId,
                req.Target,
                req.Action,
                req.Parameters,
                req.Capability,
                req.Payload,
                new AuthenticatedAgent(agentIdentity.AgentId, agentIdentity.AuthMethod)
            ),
            ct
        );

        if (result.Outcome == SessionActionOutcome.NotFound)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (result.Outcome == SessionActionOutcome.Forbidden)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        if (result.Outcome == SessionActionOutcome.Conflict)
        {
            await Send.StringAsync(string.Empty, StatusCodes.Status409Conflict, cancellation: ct);
            return;
        }

        if (result.Outcome == SessionActionOutcome.ValidationFailed)
        {
            await Send.StringAsync(string.Empty, StatusCodes.Status400BadRequest, cancellation: ct);
            return;
        }

        if (result.Execution is null)
        {
            await Send.StringAsync(string.Empty, StatusCodes.Status409Conflict, cancellation: ct);
            return;
        }

        ExecuteSessionActionResponse response = new ExecuteSessionActionResponse
        {
            SessionId = result.Execution.SessionId,
            Capability = result.Execution.Capability,
            Status = result.Execution.Status,
            Result = result.Execution.Result,
            Target = req.Target,
            Action = req.Action,
        };
        await Send.OkAsync(response, ct);
    }

    private static bool HasValidActionShape(ExecuteSessionActionRequest req)
    {
        bool hasSshShape =
            !string.IsNullOrWhiteSpace(req.Target) || !string.IsNullOrWhiteSpace(req.Action);
        if (hasSshShape)
        {
            return !string.IsNullOrWhiteSpace(req.Target) && !string.IsNullOrWhiteSpace(req.Action);
        }

        return !string.IsNullOrWhiteSpace(req.Capability);
    }
}

public sealed class ExecuteSessionActionRequest
{
    public Guid SessionId { get; set; }

    public string Target { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public JsonElement? Parameters { get; set; }

    public string Capability { get; set; } = string.Empty;

    public JsonElement? Payload { get; set; }
}

public sealed class ExecuteSessionActionValidator : Validator<ExecuteSessionActionRequest>
{
    public ExecuteSessionActionValidator()
    {
        RuleFor(request => request.SessionId).NotEmpty();
        RuleFor(request => request.Target).MaximumLength(200);
        RuleFor(request => request.Action).MaximumLength(200);
        RuleFor(request => request.Capability).MaximumLength(200);
        RuleFor(request => request)
            .Must(HasValidActionShape)
            .WithMessage("Either target/action or capability is required.");
    }

    private static bool HasValidActionShape(ExecuteSessionActionRequest request)
    {
        bool hasSshShape =
            !string.IsNullOrWhiteSpace(request.Target)
            || !string.IsNullOrWhiteSpace(request.Action);
        if (hasSshShape)
        {
            return !string.IsNullOrWhiteSpace(request.Target)
                && !string.IsNullOrWhiteSpace(request.Action);
        }

        return !string.IsNullOrWhiteSpace(request.Capability);
    }
}

public sealed class ExecuteSessionActionResponse
{
    public Guid SessionId { get; set; }

    public string Capability { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public JsonElement Result { get; set; }

    public string Target { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;
}
