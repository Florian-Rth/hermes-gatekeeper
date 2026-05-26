using System.Text.Json;

namespace Gatekeeper.Application.Sessions;

public sealed class ExecuteSessionActionCommand
{
    public ExecuteSessionActionCommand(
        Guid sessionId,
        string? target,
        string? action,
        JsonElement? parameters,
        string? capability = null,
        JsonElement? payload = null
    )
    {
        SessionId = sessionId;
        Target = target ?? string.Empty;
        Action = action ?? string.Empty;
        Parameters = parameters;
        Capability = capability ?? string.Empty;
        Payload = payload;
    }

    public ExecuteSessionActionCommand(Guid sessionId, string capability, JsonElement? payload)
        : this(sessionId, null, null, null, capability, payload) { }

    public Guid SessionId { get; }

    public string Target { get; }

    public string Action { get; }

    public JsonElement? Parameters { get; }

    public string Capability { get; }

    public JsonElement? Payload { get; }

    public bool IsSshAction =>
        !string.IsNullOrWhiteSpace(Target) || !string.IsNullOrWhiteSpace(Action);
}
