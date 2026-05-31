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
    private const string RouteTemplate = "/api/v1/sessions/{sessionId}/actions";

    private readonly ISessionActionService _sessionActions;
    private readonly AgentApiKeyGuard _agentApiKeyGuard;
    private readonly AgentAuthAuditWriter _agentAuthAuditWriter;

    public ExecuteSessionActionEndpoint(
        ISessionActionService sessionActions,
        AgentApiKeyGuard agentApiKeyGuard,
        AgentAuthAuditWriter agentAuthAuditWriter
    )
    {
        _sessionActions = sessionActions;
        _agentApiKeyGuard = agentApiKeyGuard;
        _agentAuthAuditWriter = agentAuthAuditWriter;
    }

    public override void Configure()
    {
        Post(RouteTemplate);
        AllowAnonymous();
    }

    public override async Task HandleAsync(ExecuteSessionActionRequest req, CancellationToken ct)
    {
        AgentIdentity? agentIdentity = await EnsureAgentAuthenticatedAsync(ct);
        if (agentIdentity is null)
        {
            return;
        }

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
                ToAuthenticatedAgent(agentIdentity)
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

    public override async Task OnBeforeValidateAsync(
        ExecuteSessionActionRequest req,
        CancellationToken ct
    )
    {
        await EnsureAgentAuthenticatedAsync(ct);
    }

    private async Task<AgentIdentity?> EnsureAgentAuthenticatedAsync(CancellationToken ct)
    {
        if (HttpContext.Response.HasStarted)
        {
            return null;
        }

        AgentAuthResult result = _agentApiKeyGuard.Authenticate(HttpContext);
        if (!result.Succeeded || result.Identity is null)
        {
            await _agentAuthAuditWriter.WriteFailedAuthenticationAsync(
                RouteTemplate,
                HttpMethods.Post,
                result.FailureReason ?? AgentAuthConstants.InvalidKeyReason,
                ct
            );
            await Send.StringAsync(
                string.Empty,
                StatusCodes.Status401Unauthorized,
                cancellation: ct
            );
            return null;
        }

        return result.Identity;
    }

    private static AuthenticatedAgent ToAuthenticatedAgent(AgentIdentity identity)
    {
        return new AuthenticatedAgent(identity.AgentId, identity.AuthMethod);
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
