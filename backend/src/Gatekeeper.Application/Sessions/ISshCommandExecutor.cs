namespace Gatekeeper.Application.Sessions;

public interface ISshCommandExecutor
{
    Task<SshCommandExecutionResult> ExecuteAsync(
        SshResolvedAction resolvedAction,
        CancellationToken cancellationToken
    );
}
