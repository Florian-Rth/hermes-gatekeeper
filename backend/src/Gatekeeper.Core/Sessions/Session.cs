using Gatekeeper.Core.AccessRequests;

namespace Gatekeeper.Core.Sessions;

public sealed class Session
{
    private Session(
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

    public static Session CreateFromApprovedAccessRequest(AccessRequest request, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Status != AccessRequestStatus.Approved)
        {
            throw new InvalidOperationException(
                "Only approved access requests can create sessions."
            );
        }

        return new Session(
            Guid.NewGuid(),
            request.Id,
            SessionStatus.Active,
            request.Targets,
            request.RequestedCapabilities,
            now,
            now.AddMinutes(request.DurationMinutes)
        );
    }

    public static Session Load(
        Guid id,
        Guid accessRequestId,
        SessionStatus status,
        IReadOnlyList<string> allowedTargets,
        IReadOnlyList<string> allowedCapabilities,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt
    )
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id is required.", nameof(id));
        }

        if (accessRequestId == Guid.Empty)
        {
            throw new ArgumentException("Access request id is required.", nameof(accessRequestId));
        }

        return new Session(
            id,
            accessRequestId,
            status,
            allowedTargets,
            allowedCapabilities,
            createdAt,
            expiresAt
        );
    }
}
