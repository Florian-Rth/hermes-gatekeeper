using Gatekeeper.Core.Sessions;

namespace Gatekeeper.Application.Sessions;

public sealed class SessionDetails
{
    public SessionDetails(
        Guid id,
        Guid accessRequestId,
        SessionStatus status,
        IReadOnlyList<string> allowedTargets,
        IReadOnlyList<string> allowedCapabilities,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt
    )
    {
        Id = id;
        AccessRequestId = accessRequestId;
        Status = status;
        AllowedTargets = allowedTargets;
        AllowedCapabilities = allowedCapabilities;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; }

    public Guid AccessRequestId { get; }

    public SessionStatus Status { get; }

    public IReadOnlyList<string> AllowedTargets { get; }

    public IReadOnlyList<string> AllowedCapabilities { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset ExpiresAt { get; }
}
