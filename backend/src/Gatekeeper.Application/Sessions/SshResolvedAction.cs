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
    {
        TargetAlias = targetAlias;
        ActionName = actionName;
        Command = command.ToArray();
        SafeParameters = new Dictionary<string, string>(safeParameters, StringComparer.Ordinal);
        Timeout = timeout;
        OutputLimitBytes = outputLimitBytes;
        IsMutating = isMutating;
        Risk = risk;
    }

    public string TargetAlias { get; }

    public string ActionName { get; }

    public IReadOnlyList<string> Command { get; }

    public IReadOnlyDictionary<string, string> SafeParameters { get; }

    public TimeSpan Timeout { get; }

    public int OutputLimitBytes { get; }

    public bool IsMutating { get; }

    public RiskLevel Risk { get; }
}
