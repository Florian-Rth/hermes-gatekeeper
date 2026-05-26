namespace Gatekeeper.Infrastructure.SessionActions.Ssh;

public sealed class SshConnectorOptions
{
    public const string SectionName = "SshConnector";

    public Dictionary<string, SshTargetOptions> Targets { get; set; } = new(StringComparer.Ordinal);
}
