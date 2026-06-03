using Gatekeeper.Core.AccessRequests;

namespace Gatekeeper.Infrastructure.Persistence.Entities;

public sealed class SshActionEntity
{
    public Guid Id { get; set; }

    public Guid TargetId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string CommandJson { get; set; } = "[]";

    public string CommandTemplateJson { get; set; } = "[]";

    public bool IsMutating { get; set; }

    public RiskLevel Risk { get; set; }

    public int? TimeoutSeconds { get; set; }

    public int? OutputLimitBytes { get; set; }

    public SshTargetEntity Target { get; set; } = null!;

    public List<SshProfileActionEntity> ProfileActions { get; set; } = new();

    public List<SshActionAllowedParameterEntity> AllowedParameters { get; set; } = new();
}
