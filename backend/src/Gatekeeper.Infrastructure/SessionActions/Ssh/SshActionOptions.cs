using Gatekeeper.Core.AccessRequests;

namespace Gatekeeper.Infrastructure.SessionActions.Ssh;

public sealed class SshActionOptions
{
    public List<string> Command { get; set; } = new();

    public List<string> CommandTemplate { get; set; } = new();

    public Dictionary<string, List<string>> AllowedParameters { get; set; } =
        new(StringComparer.Ordinal);

    public bool? IsMutating { get; set; }

    public RiskLevel? Risk { get; set; }

    public int? TimeoutSeconds { get; set; }

    public int? OutputLimitBytes { get; set; }
}
