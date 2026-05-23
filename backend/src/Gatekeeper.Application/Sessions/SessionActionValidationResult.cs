namespace Gatekeeper.Application.Sessions;

public sealed class SessionActionValidationResult
{
    private SessionActionValidationResult(bool succeeded, string? error)
    {
        Succeeded = succeeded;
        Error = error;
    }

    public bool Succeeded { get; }

    public string? Error { get; }

    public static SessionActionValidationResult Success()
    {
        return new SessionActionValidationResult(true, null);
    }

    public static SessionActionValidationResult InvalidPayload(string error)
    {
        return new SessionActionValidationResult(false, error);
    }
}
