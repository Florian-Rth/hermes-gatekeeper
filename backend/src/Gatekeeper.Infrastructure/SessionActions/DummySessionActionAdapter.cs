using System.Text.Json;
using Gatekeeper.Application.Sessions;

namespace Gatekeeper.Infrastructure.SessionActions;

public sealed class DummySessionActionAdapter : ISessionActionAdapter
{
    public Task<SessionActionAdapterResult> ExecuteAsync(
        string capability,
        JsonElement? payload,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        SessionActionAdapterResult result = capability switch
        {
            "test.echo" => ExecuteEcho(payload),
            "test.status.read" => ExecuteStatusRead(payload),
            "test.fail" => SessionActionAdapterResult.Failed("Dummy adapter failure."),
            _ => SessionActionAdapterResult.Failed("Unsupported dummy capability."),
        };

        return Task.FromResult(result);
    }

    private static SessionActionAdapterResult ExecuteEcho(JsonElement? payload)
    {
        if (payload is null || payload.Value.ValueKind != JsonValueKind.Object)
        {
            return SessionActionAdapterResult.InvalidPayload(
                "test.echo payload must be an object with a message string."
            );
        }

        if (!payload.Value.TryGetProperty("message", out JsonElement messageElement))
        {
            return SessionActionAdapterResult.InvalidPayload(
                "test.echo payload must include a message string."
            );
        }

        if (messageElement.ValueKind != JsonValueKind.String)
        {
            return SessionActionAdapterResult.InvalidPayload("test.echo message must be a string.");
        }

        string? message = messageElement.GetString();
        if (message is null)
        {
            return SessionActionAdapterResult.InvalidPayload("test.echo message must be a string.");
        }

        JsonElement result = JsonSerializer.SerializeToElement(new { message });
        return SessionActionAdapterResult.Success(result);
    }

    private static SessionActionAdapterResult ExecuteStatusRead(JsonElement? payload)
    {
        if (payload is not null && !IsEmptyObject(payload.Value))
        {
            return SessionActionAdapterResult.InvalidPayload(
                "test.status.read payload must be omitted or an empty object."
            );
        }

        JsonElement result = JsonSerializer.SerializeToElement(new { status = "ok" });
        return SessionActionAdapterResult.Success(result);
    }

    private static bool IsEmptyObject(JsonElement payload)
    {
        return payload.ValueKind == JsonValueKind.Object && !payload.EnumerateObject().Any();
    }
}
