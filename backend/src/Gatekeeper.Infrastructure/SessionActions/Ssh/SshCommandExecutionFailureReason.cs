namespace Gatekeeper.Infrastructure.SessionActions.Ssh;

public enum SshCommandExecutionFailureReason
{
    None = 0,
    UnknownTarget = 1,
    InvalidResolvedCommand = 2,
    Timeout = 3,
    ConnectionFailed = 4,
    AuthenticationFailed = 5,
    ClientFailed = 6,
}
