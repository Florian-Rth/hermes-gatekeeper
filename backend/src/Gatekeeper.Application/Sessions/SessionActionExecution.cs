using System.Text.Json;

namespace Gatekeeper.Application.Sessions;

public sealed class SessionActionExecution
{
    public SessionActionExecution(
        Guid sessionId,
        string capability,
        string status,
        JsonElement result
    )
    {
        SessionId = sessionId;
        Capability = capability;
        Status = status;
        Result = result;
    }

    public Guid SessionId { get; }

    public string Capability { get; }

    public string Status { get; }

    public JsonElement Result { get; }
}
