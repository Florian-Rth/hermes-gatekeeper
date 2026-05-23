using FastEndpoints;
using Gatekeeper.Application.Sessions;
using Gatekeeper.Core.Sessions;

namespace Gatekeeper.Api.Endpoints.Sessions;

public sealed class CompleteSessionEndpoint : EndpointWithoutRequest<SessionLifecycleResponse>
{
    private readonly ISessionService _sessions;

    public CompleteSessionEndpoint(ISessionService sessions)
    {
        _sessions = sessions;
    }

    public override void Configure()
    {
        Post("/api/v1/sessions/{id}/complete");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(Route<string>("id"), out Guid id) || id == Guid.Empty)
        {
            await Send.StringAsync(string.Empty, StatusCodes.Status400BadRequest, cancellation: ct);
            return;
        }

        SessionLifecycleResult result = await _sessions.CompleteAsync(id, ct);
        if (result.NotFound)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (result.Conflict || result.Session is null)
        {
            await Send.StringAsync(string.Empty, StatusCodes.Status409Conflict, cancellation: ct);
            return;
        }

        await Send.OkAsync(SessionLifecycleResponse.FromDetails(result.Session), ct);
    }
}

public sealed class SessionLifecycleResponse
{
    public Guid Id { get; set; }

    public Guid AccessRequestId { get; set; }

    public SessionStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public DateTimeOffset? ExpiredAt { get; set; }

    public static SessionLifecycleResponse FromDetails(SessionDetails session)
    {
        return new SessionLifecycleResponse
        {
            Id = session.Id,
            AccessRequestId = session.AccessRequestId,
            Status = session.Status,
            CreatedAt = session.CreatedAt,
            ExpiresAt = session.ExpiresAt,
            CompletedAt = session.CompletedAt,
            RevokedAt = session.RevokedAt,
            ExpiredAt = session.ExpiredAt,
        };
    }
}
