using Gatekeeper.Application.Sessions;

namespace Gatekeeper.Infrastructure.SessionActions.Ssh;

public interface ISshCommandExecutor
{
    Task<SshCommandExecutionResult> ExecuteAsync(
        SshResolvedAction resolvedAction,
        CancellationToken cancellationToken
    );
}
