using System.Text.Json;
using FastEndpoints;
using FluentValidation;
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
        AllowAnonymous();
    }

    public override async Task HandleAsync(ExecuteSessionActionRequest req, CancellationToken ct)
    {
        if (req.SessionId == Guid.Empty || string.IsNullOrWhiteSpace(req.Capability))
        {
            await Send.StringAsync(string.Empty, StatusCodes.Status400BadRequest, cancellation: ct);
            return;
        }

        SessionActionResult result = await _sessionActions.ExecuteAsync(
            new ExecuteSessionActionCommand(req.SessionId, req.Capability, req.Payload),
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
        };
        await Send.OkAsync(response, ct);
    }
}

public sealed class ExecuteSessionActionRequest
{
    public Guid SessionId { get; set; }

    public string Capability { get; set; } = string.Empty;

    public JsonElement? Payload { get; set; }
}

public sealed class ExecuteSessionActionValidator : Validator<ExecuteSessionActionRequest>
{
    public ExecuteSessionActionValidator()
    {
        RuleFor(request => request.SessionId).NotEmpty();
        RuleFor(request => request.Capability).NotEmpty().MaximumLength(200);
    }
}

public sealed class ExecuteSessionActionResponse
{
    public Guid SessionId { get; set; }

    public string Capability { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public JsonElement Result { get; set; }
}
