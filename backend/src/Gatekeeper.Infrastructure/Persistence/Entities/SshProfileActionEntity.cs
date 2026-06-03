namespace Gatekeeper.Infrastructure.Persistence.Entities;

public sealed class SshProfileActionEntity
{
    public Guid ProfileId { get; set; }

    public Guid ActionId { get; set; }

    public SshProfileEntity Profile { get; set; } = null!;

    public SshActionEntity Action { get; set; } = null!;
}
