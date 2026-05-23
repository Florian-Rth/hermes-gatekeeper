using FastEndpoints;
using FluentValidation;
using Gatekeeper.Application.Sessions;
using Gatekeeper.Core.Sessions;

namespace Gatekeeper.Api.Endpoints.Sessions;

public sealed class GetSessionEndpoint : Endpoint<GetSessionRequest, GetSessionResponse>
{
    private readonly ISessionService _sessions;

    public GetSessionEndpoint(ISessionService sessions)
    {
        _sessions = sessions;
    }

    public override void Configure()
    {
        Get("/api/v1/sessions/{id}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetSessionRequest req, CancellationToken ct)
    {
        SessionDetails? session = await _sessions.GetByIdAsync(req.Id, ct);
        if (session is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        GetSessionResponse response = new GetSessionResponse
        {
            Id = session.Id,
            AccessRequestId = session.AccessRequestId,
            Status = session.Status,
            AllowedTargets = session.AllowedTargets,
            AllowedCapabilities = session.AllowedCapabilities,
            CreatedAt = session.CreatedAt,
            ExpiresAt = session.ExpiresAt,
            ActionCount = session.ActionCount,
            MaxActionCount = session.MaxActionCount,
            CompletedAt = session.CompletedAt,
            RevokedAt = session.RevokedAt,
            ExpiredAt = session.ExpiredAt,
        };
        await Send.OkAsync(response, ct);
    }
}

public sealed class GetSessionRequest
{
    public Guid Id { get; set; }
}

public sealed class GetSessionValidator : Validator<GetSessionRequest>
{
    public GetSessionValidator()
    {
        RuleFor(request => request.Id).NotEmpty();
    }
}

public sealed class GetSessionResponse
{
    public Guid Id { get; set; }

    public Guid AccessRequestId { get; set; }

    public SessionStatus Status { get; set; }

    public IReadOnlyList<string> AllowedTargets { get; set; } = [];

    public IReadOnlyList<string> AllowedCapabilities { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public int ActionCount { get; set; }

    public int MaxActionCount { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public DateTimeOffset? ExpiredAt { get; set; }
}
