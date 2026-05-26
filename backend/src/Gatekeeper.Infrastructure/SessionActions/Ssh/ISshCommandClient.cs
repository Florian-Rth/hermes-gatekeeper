namespace Gatekeeper.Infrastructure.SessionActions.Ssh;

public interface ISshCommandClient
{
    Task<SshCommandClientResult> ExecuteAsync(
        SshCommandClientRequest request,
        CancellationToken cancellationToken
    );
}
