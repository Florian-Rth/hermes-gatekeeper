using Gatekeeper.Core.AccessRequests;

namespace Gatekeeper.Application.Sessions;

public sealed class SshResolvedAction
{
    public SshResolvedAction(
        string targetAlias,
        string actionName,
        IReadOnlyList<string> command,
        IReadOnlyDictionary<string, string> safeParameters,
        TimeSpan timeout,
        int outputLimitBytes,
        bool isMutating,
        RiskLevel risk
    )
        : this(
            targetAlias,
            actionName,
            command,
            safeParameters,
            timeout,
            outputLimitBytes,
            isMutating,
            risk,
            host: string.Empty,
            port: 0,
            username: string.Empty,
            privateKeyPath: string.Empty,
            knownHostsPath: string.Empty
        ) { }

    public SshResolvedAction(
        string targetAlias,
        string actionName,
        IReadOnlyList<string> command,
        IReadOnlyDictionary<string, string> safeParameters,
        TimeSpan timeout,
        int outputLimitBytes,
        bool isMutating,
        RiskLevel risk,
        string host,
        int port,
        string username,
        string privateKeyPath,
        string knownHostsPath
    )
    {
        TargetAlias = targetAlias;
        ActionName = actionName;
        Command = command.ToArray();
        SafeParameters = new Dictionary<string, string>(safeParameters, StringComparer.Ordinal);
        Timeout = timeout;
        OutputLimitBytes = outputLimitBytes;
        IsMutating = isMutating;
        Risk = risk;
        Host = host;
        Port = port;
        Username = username;
        PrivateKeyPath = privateKeyPath;
        KnownHostsPath = knownHostsPath;
    }

    public string TargetAlias { get; }

    public string ActionName { get; }

    public IReadOnlyList<string> Command { get; }

    public IReadOnlyDictionary<string, string> SafeParameters { get; }

    public TimeSpan Timeout { get; }

    public int OutputLimitBytes { get; }

    public bool IsMutating { get; }

    public RiskLevel Risk { get; }

    public string Host { get; }

    public int Port { get; }

    public string Username { get; }

    public string PrivateKeyPath { get; }

    public string KnownHostsPath { get; }
}
