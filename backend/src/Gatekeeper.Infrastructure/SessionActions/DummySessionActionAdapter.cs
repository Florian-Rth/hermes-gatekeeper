using System.Text.Json;
using Gatekeeper.Application.Sessions;

namespace Gatekeeper.Infrastructure.SessionActions;

public sealed class DummySessionActionAdapter : ISessionActionAdapter
{
    public SessionActionValidationResult Validate(string capability, JsonElement? payload)
    {
        return capability switch
        {
            "test.echo" => ValidateEcho(payload),
            "test.status.read" => ValidateStatusRead(payload),
            "test.fail" => SessionActionValidationResult.Success(),
            _ => SessionActionValidationResult.InvalidPayload("Unsupported dummy capability."),
        };
    }

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
        SessionActionValidationResult validation = ValidateEcho(payload);
        if (!validation.Succeeded)
        {
            return SessionActionAdapterResult.InvalidPayload(validation.Error!);
        }

        JsonElement messageElement = payload!.Value.GetProperty("message");
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
        SessionActionValidationResult validation = ValidateStatusRead(payload);
        if (!validation.Succeeded)
        {
            return SessionActionAdapterResult.InvalidPayload(validation.Error!);
        }

        JsonElement result = JsonSerializer.SerializeToElement(new { status = "ok" });
        return SessionActionAdapterResult.Success(result);
    }

    private static SessionActionValidationResult ValidateEcho(JsonElement? payload)
    {
        if (payload is null || payload.Value.ValueKind != JsonValueKind.Object)
        {
            return SessionActionValidationResult.InvalidPayload(
                "test.echo payload must be an object with a message string."
            );
        }

        if (!payload.Value.TryGetProperty("message", out JsonElement messageElement))
        {
            return SessionActionValidationResult.InvalidPayload(
                "test.echo payload must include a message string."
            );
        }

        if (messageElement.ValueKind != JsonValueKind.String || messageElement.GetString() is null)
        {
            return SessionActionValidationResult.InvalidPayload(
                "test.echo message must be a string."
            );
        }

        return SessionActionValidationResult.Success();
    }

    private static SessionActionValidationResult ValidateStatusRead(JsonElement? payload)
    {
        if (payload is not null && !IsEmptyObject(payload.Value))
        {
            return SessionActionValidationResult.InvalidPayload(
                "test.status.read payload must be omitted or an empty object."
            );
        }

        return SessionActionValidationResult.Success();
    }

    private static bool IsEmptyObject(JsonElement payload)
    {
        return payload.ValueKind == JsonValueKind.Object && !payload.EnumerateObject().Any();
    }
}
