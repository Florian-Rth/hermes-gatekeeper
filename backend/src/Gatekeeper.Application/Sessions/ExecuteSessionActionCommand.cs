using System.Text.Json;

namespace Gatekeeper.Application.Sessions;

public sealed class ExecuteSessionActionCommand
{
    public ExecuteSessionActionCommand(Guid sessionId, string capability, JsonElement? payload)
    {
        SessionId = sessionId;
        Capability = capability;
        Payload = payload;
    }

    public Guid SessionId { get; }

    public string Capability { get; }

    public JsonElement? Payload { get; }
}
