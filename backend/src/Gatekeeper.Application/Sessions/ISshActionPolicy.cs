using System.Text.Json;

namespace Gatekeeper.Application.Sessions;

public interface ISshActionPolicy
{
    SshActionPolicyResult Resolve(
        string targetAlias,
        string actionName,
        IReadOnlyCollection<SshApprovedProfileGrant> approvedProfileGrants,
        JsonElement? parameters
    );
}
