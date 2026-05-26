namespace Gatekeeper.Infrastructure.SessionActions.Ssh;

public enum SshCommandClientFailureReason
{
    None = 0,
    Timeout = 1,
    ConnectionFailed = 2,
    AuthenticationFailed = 3,
    ClientFailed = 4,
}
