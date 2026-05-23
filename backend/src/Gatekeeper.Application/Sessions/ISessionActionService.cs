namespace Gatekeeper.Application.Sessions;

public interface ISessionActionService
{
    Task<SessionActionResult> ExecuteAsync(
        ExecuteSessionActionCommand command,
        CancellationToken cancellationToken
    );
}
