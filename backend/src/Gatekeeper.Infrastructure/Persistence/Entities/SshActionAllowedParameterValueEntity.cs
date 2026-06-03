namespace Gatekeeper.Infrastructure.Persistence.Entities;

public sealed class SshActionAllowedParameterValueEntity
{
    public Guid Id { get; set; }

    public Guid AllowedParameterId { get; set; }

    public string Value { get; set; } = string.Empty;

    public SshActionAllowedParameterEntity AllowedParameter { get; set; } = null!;
}
