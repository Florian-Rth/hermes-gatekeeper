namespace Gatekeeper.Infrastructure.Persistence.Entities;

public sealed class SshActionAllowedParameterEntity
{
    public Guid Id { get; set; }

    public Guid ActionId { get; set; }

    public string Name { get; set; } = string.Empty;

    public SshActionEntity Action { get; set; } = null!;

    public List<SshActionAllowedParameterValueEntity> AllowedValues { get; set; } = new();
}
