using Gatekeeper.Core.AccessRequests;

namespace Gatekeeper.Core.Sessions;

public sealed class Session
{
    public const int DefaultMaxActionCount = 10;
    public const int MaxAllowedActionCount = 100;

    private Session(
        Guid id,
        Guid accessRequestId,
        SessionStatus status,
        IReadOnlyList<string> allowedTargets,
        IReadOnlyList<string> allowedCapabilities,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        int actionCount,
        int maxActionCount,
        DateTimeOffset? completedAt,
        DateTimeOffset? revokedAt,
        DateTimeOffset? expiredAt
    )
    {
        Id = id;
        AccessRequestId = accessRequestId;
        Status = status;
        AllowedTargets = allowedTargets;
        AllowedCapabilities = allowedCapabilities;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        ActionCount = actionCount;
        MaxActionCount = maxActionCount;
        CompletedAt = completedAt;
        RevokedAt = revokedAt;
        ExpiredAt = expiredAt;
    }

    public Guid Id { get; }

    public Guid AccessRequestId { get; }

    public SessionStatus Status { get; }

    public IReadOnlyList<string> AllowedTargets { get; }

    public IReadOnlyList<string> AllowedCapabilities { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset ExpiresAt { get; }

    public int ActionCount { get; }

    public int MaxActionCount { get; }

    public DateTimeOffset? CompletedAt { get; }

    public DateTimeOffset? RevokedAt { get; }

    public DateTimeOffset? ExpiredAt { get; }

    public static Session CreateFromApprovedAccessRequest(
        AccessRequest request,
        DateTimeOffset now,
        int maxActionCount
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Status != AccessRequestStatus.Approved)
        {
            throw new InvalidOperationException(
                "Only approved access requests can create sessions."
            );
        }

        ValidateActionBudget(0, maxActionCount);

        return new Session(
            Guid.NewGuid(),
            request.Id,
            SessionStatus.Active,
            request.Targets,
            request.RequestedCapabilities,
            now,
            now.AddMinutes(request.DurationMinutes),
            0,
            maxActionCount,
            null,
            null,
            null
        );
    }

    public static Session CreateFromApprovedAccessRequest(AccessRequest request, DateTimeOffset now)
    {
        return CreateFromApprovedAccessRequest(request, now, DefaultMaxActionCount);
    }

    public static Session Load(
        Guid id,
        Guid accessRequestId,
        SessionStatus status,
        IReadOnlyList<string> allowedTargets,
        IReadOnlyList<string> allowedCapabilities,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        int actionCount,
        int maxActionCount,
        DateTimeOffset? completedAt,
        DateTimeOffset? revokedAt,
        DateTimeOffset? expiredAt
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

        ValidateActionBudget(actionCount, maxActionCount);
        ValidateLifecycleTimestamps(status, completedAt, revokedAt, expiredAt);

        return new Session(
            id,
            accessRequestId,
            status,
            allowedTargets,
            allowedCapabilities,
            createdAt,
            expiresAt,
            actionCount,
            maxActionCount,
            completedAt,
            revokedAt,
            expiredAt
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
        return Load(
            id,
            accessRequestId,
            status,
            allowedTargets,
            allowedCapabilities,
            createdAt,
            expiresAt,
            0,
            DefaultMaxActionCount,
            null,
            null,
            null
        );
    }

    public Session Complete(DateTimeOffset completedAt)
    {
        if (Status != SessionStatus.Active)
        {
            throw new InvalidOperationException("Only active sessions can be completed.");
        }

        return new Session(
            Id,
            AccessRequestId,
            SessionStatus.Completed,
            AllowedTargets,
            AllowedCapabilities,
            CreatedAt,
            ExpiresAt,
            ActionCount,
            MaxActionCount,
            completedAt,
            null,
            null
        );
    }

    public Session Revoke(DateTimeOffset revokedAt)
    {
        if (Status != SessionStatus.Active)
        {
            throw new InvalidOperationException("Only active sessions can be revoked.");
        }

        return new Session(
            Id,
            AccessRequestId,
            SessionStatus.Revoked,
            AllowedTargets,
            AllowedCapabilities,
            CreatedAt,
            ExpiresAt,
            ActionCount,
            MaxActionCount,
            null,
            revokedAt,
            null
        );
    }

    public Session Expire(DateTimeOffset expiredAt)
    {
        if (Status != SessionStatus.Active)
        {
            throw new InvalidOperationException("Only active sessions can be expired.");
        }

        return new Session(
            Id,
            AccessRequestId,
            SessionStatus.Expired,
            AllowedTargets,
            AllowedCapabilities,
            CreatedAt,
            ExpiresAt,
            ActionCount,
            MaxActionCount,
            null,
            null,
            expiredAt
        );
    }

    private static void ValidateActionBudget(int actionCount, int maxActionCount)
    {
        if (actionCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(actionCount),
                actionCount,
                "Action count must be non-negative."
            );
        }

        if (maxActionCount < 1 || maxActionCount > MaxAllowedActionCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxActionCount),
                maxActionCount,
                $"Max action count must be between 1 and {MaxAllowedActionCount}."
            );
        }

        if (actionCount > maxActionCount)
        {
            throw new ArgumentException(
                "Action count cannot exceed max action count.",
                nameof(actionCount)
            );
        }
    }

    private static void ValidateLifecycleTimestamps(
        SessionStatus status,
        DateTimeOffset? completedAt,
        DateTimeOffset? revokedAt,
        DateTimeOffset? expiredAt
    )
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Invalid session status."
            );
        }

        switch (status)
        {
            case SessionStatus.Active:
                if (completedAt is not null || revokedAt is not null || expiredAt is not null)
                {
                    throw new ArgumentException(
                        "Active sessions must not have lifecycle completion timestamps."
                    );
                }

                break;
            case SessionStatus.Completed:
                if (completedAt is null || revokedAt is not null || expiredAt is not null)
                {
                    throw new ArgumentException(
                        "Completed sessions must have CompletedAt and no RevokedAt or ExpiredAt."
                    );
                }

                break;
            case SessionStatus.Revoked:
                if (revokedAt is null || completedAt is not null || expiredAt is not null)
                {
                    throw new ArgumentException(
                        "Revoked sessions must have RevokedAt and no CompletedAt or ExpiredAt."
                    );
                }

                break;
            case SessionStatus.Expired:
                if (expiredAt is null || completedAt is not null || revokedAt is not null)
                {
                    throw new ArgumentException(
                        "Expired sessions must have ExpiredAt and no CompletedAt or RevokedAt."
                    );
                }

                break;
        }
    }
}
