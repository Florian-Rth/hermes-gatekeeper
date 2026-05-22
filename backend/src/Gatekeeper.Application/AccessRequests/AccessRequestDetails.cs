using Gatekeeper.Core.AccessRequests;

namespace Gatekeeper.Application.AccessRequests;

public sealed class AccessRequestDetails
{
    public AccessRequestDetails(
        Guid id,
        string intent,
        string requester,
        IReadOnlyList<string> targets,
        IReadOnlyList<string> requestedCapabilities,
        int durationMinutes,
        RiskLevel risk,
        string? justification,
        IReadOnlyList<string> proposedActions,
        IReadOnlyList<string> forbiddenActions,
        IReadOnlyDictionary<string, string> metadata,
        AccessRequestStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt
    )
    {
        Id = id;
        Intent = intent;
        Requester = requester;
        Targets = targets;
        RequestedCapabilities = requestedCapabilities;
        DurationMinutes = durationMinutes;
        Risk = risk;
        Justification = justification;
        ProposedActions = proposedActions;
        ForbiddenActions = forbiddenActions;
        Metadata = metadata;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; }

    public string Intent { get; }

    public string Requester { get; }

    public IReadOnlyList<string> Targets { get; }

    public IReadOnlyList<string> RequestedCapabilities { get; }

    public int DurationMinutes { get; }

    public RiskLevel Risk { get; }

    public string? Justification { get; }

    public IReadOnlyList<string> ProposedActions { get; }

    public IReadOnlyList<string> ForbiddenActions { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    public AccessRequestStatus Status { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; }
}
