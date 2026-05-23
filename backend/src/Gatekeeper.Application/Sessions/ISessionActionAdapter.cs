using System.Text.Json;

namespace Gatekeeper.Application.Sessions;

public interface ISessionActionAdapter
{
    Task<SessionActionAdapterResult> ExecuteAsync(
        string capability,
        JsonElement? payload,
        CancellationToken cancellationToken
    );
}
