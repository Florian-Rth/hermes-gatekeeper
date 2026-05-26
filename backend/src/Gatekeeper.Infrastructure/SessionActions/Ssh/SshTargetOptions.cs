namespace Gatekeeper.Infrastructure.SessionActions.Ssh;

public sealed class SshTargetOptions
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 22;

    public string Username { get; set; } = string.Empty;

    public string PrivateKeyPath { get; set; } = string.Empty;

    public string KnownHostsPath { get; set; } = string.Empty;

    public int DefaultTimeoutSeconds { get; set; } = 10;

    public int DefaultOutputLimitBytes { get; set; } = 8192;

    public Dictionary<string, SshProfileOptions> Profiles { get; set; } =
        new(StringComparer.Ordinal);

    public Dictionary<string, SshActionOptions> Actions { get; set; } = new(StringComparer.Ordinal);
}
