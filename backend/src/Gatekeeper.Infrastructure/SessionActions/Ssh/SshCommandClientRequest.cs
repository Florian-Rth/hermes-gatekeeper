namespace Gatekeeper.Infrastructure.SessionActions.Ssh;

public sealed class SshCommandClientRequest
{
    public SshCommandClientRequest(
        string host,
        int port,
        string username,
        string privateKeyPath,
        string knownHostsPath,
        string executable,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        int outputLimitBytes
    )
    {
        Host = host;
        Port = port;
        Username = username;
        PrivateKeyPath = privateKeyPath;
        KnownHostsPath = knownHostsPath;
        Executable = executable;
        Arguments = arguments.ToArray();
        Timeout = timeout;
        OutputLimitBytes = outputLimitBytes;
    }

    public string Host { get; }

    public int Port { get; }

    public string Username { get; }

    public string PrivateKeyPath { get; }

    public string KnownHostsPath { get; }

    public string Executable { get; }

    public IReadOnlyList<string> Arguments { get; }

    public TimeSpan Timeout { get; }

    public int OutputLimitBytes { get; }
}
