namespace Gatekeeper.Application.Sessions;

public sealed class SshApprovedProfileGrant
{
    public SshApprovedProfileGrant(string targetAlias, string profileName)
    {
        TargetAlias = targetAlias;
        ProfileName = profileName;
    }

    public string TargetAlias { get; }

    public string ProfileName { get; }
}
