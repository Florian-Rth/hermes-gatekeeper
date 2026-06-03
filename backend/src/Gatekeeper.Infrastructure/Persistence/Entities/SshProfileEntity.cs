namespace Gatekeeper.Infrastructure.Persistence.Entities;

public sealed class SshProfileEntity
{
    public Guid Id { get; set; }

    public Guid TargetId { get; set; }

    public string Name { get; set; } = string.Empty;

    public SshTargetEntity Target { get; set; } = null!;

    public List<SshProfileActionEntity> ProfileActions { get; set; } = new();
}
