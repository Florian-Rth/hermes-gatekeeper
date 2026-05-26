namespace Gatekeeper.Infrastructure.SessionActions.Ssh;

public sealed class DisabledSshCommandClient : ISshCommandClient
{
    public Task<SshCommandClientResult> ExecuteAsync(
        SshCommandClientRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        SshCommandClientResult result = SshCommandClientResult.Failed(
            SshCommandClientFailureReason.ClientFailed,
            "SSH command client is not configured."
        );

        return Task.FromResult(result);
    }
}
