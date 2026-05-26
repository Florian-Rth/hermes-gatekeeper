namespace Gatekeeper.Application.Sessions;

public enum SshActionPolicyFailureReason
{
    None = 0,
    UnknownTarget = 1,
    UnknownAction = 2,
    MissingProfileMembership = 3,
    InvalidParameter = 4,
    InvalidConfiguration = 5,
}
