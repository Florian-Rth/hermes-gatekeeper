namespace Gatekeeper.Infrastructure.Persistence.Entities;

public sealed class SshTargetEntity
{
    public Guid Id { get; set; }

    public string Alias { get; set; } = string.Empty;

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; }

    public string Username { get; set; } = string.Empty;

    public string PrivateKeyPath { get; set; } = string.Empty;

    public string KnownHostsPath { get; set; } = string.Empty;

    public int DefaultTimeoutSeconds { get; set; }

    public int DefaultOutputLimitBytes { get; set; }

    public List<SshProfileEntity> Profiles { get; set; } = new();

    public List<SshActionEntity> Actions { get; set; } = new();
}
