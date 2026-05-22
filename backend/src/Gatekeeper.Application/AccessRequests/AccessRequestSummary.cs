using Gatekeeper.Core.AccessRequests;

namespace Gatekeeper.Application.AccessRequests;

public sealed class AccessRequestSummary
{
    public AccessRequestSummary(
        Guid id,
        string intent,
        string requester,
        IReadOnlyList<string> targets,
        IReadOnlyList<string> requestedCapabilities,
        int durationMinutes,
        RiskLevel risk,
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

    public AccessRequestStatus Status { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; }
}
