using System.Text.Json;

namespace Gatekeeper.Application.Sessions;

public interface ISessionActionAdapter
{
    SessionActionValidationResult Validate(string capability, JsonElement? payload);

    Task<SessionActionAdapterResult> ExecuteAsync(
        string capability,
        JsonElement? payload,
        CancellationToken cancellationToken
    );
}
