namespace Gatekeeper.Core.Sessions;

public sealed class SshProfileGrant
{
    public SshProfileGrant(string targetAlias, string profileName)
    {
        TargetAlias = targetAlias;
        ProfileName = profileName;
    }

    public string TargetAlias { get; }

    public string ProfileName { get; }
}
