using System.Text.Json;

namespace Gatekeeper.Application.Sessions;

public sealed class SessionActionAdapterResult
{
    private SessionActionAdapterResult(
        bool succeeded,
        bool validationFailed,
        JsonElement? result,
        string? error
    )
    {
        Succeeded = succeeded;
        ValidationFailed = validationFailed;
        Result = result;
        Error = error;
    }

    public bool Succeeded { get; }

    public bool ValidationFailed { get; }

    public JsonElement? Result { get; }

    public string? Error { get; }

    public static SessionActionAdapterResult Success(JsonElement result)
    {
        return new SessionActionAdapterResult(true, false, result, null);
    }

    public static SessionActionAdapterResult InvalidPayload(string error)
    {
        return new SessionActionAdapterResult(false, true, null, error);
    }

    public static SessionActionAdapterResult Failed(string error)
    {
        return new SessionActionAdapterResult(false, false, null, error);
    }
}
