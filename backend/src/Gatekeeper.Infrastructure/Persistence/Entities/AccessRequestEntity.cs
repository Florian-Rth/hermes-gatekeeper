using Gatekeeper.Core.AccessRequests;

namespace Gatekeeper.Infrastructure.Persistence.Entities;

public sealed class AccessRequestEntity
{
    public Guid Id { get; set; }

    public string Intent { get; set; } = string.Empty;

    public string Requester { get; set; } = string.Empty;

    public string TargetsJson { get; set; } = "[]";

    public string RequestedCapabilitiesJson { get; set; } = "[]";

    public int DurationMinutes { get; set; }

    public RiskLevel Risk { get; set; }

    public string? Justification { get; set; }

    public string ProposedActionsJson { get; set; } = "[]";

    public string ForbiddenActionsJson { get; set; } = "[]";

    public string MetadataJson { get; set; } = "{}";

    public AccessRequestStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
