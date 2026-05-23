namespace Gatekeeper.Application.Sessions;

public sealed class SessionActionResult
{
    private SessionActionResult(
        SessionActionOutcome outcome,
        SessionActionExecution? execution,
        string? error
    )
    {
        Outcome = outcome;
        Execution = execution;
        Error = error;
    }

    public SessionActionOutcome Outcome { get; }

    public SessionActionExecution? Execution { get; }

    public string? Error { get; }

    public static SessionActionResult Succeeded(SessionActionExecution execution)
    {
        return new SessionActionResult(SessionActionOutcome.Succeeded, execution, null);
    }

    public static SessionActionResult Missing()
    {
        return new SessionActionResult(SessionActionOutcome.NotFound, null, null);
    }

    public static SessionActionResult Forbidden(string error)
    {
        return new SessionActionResult(SessionActionOutcome.Forbidden, null, error);
    }

    public static SessionActionResult Conflicted(string error)
    {
        return new SessionActionResult(SessionActionOutcome.Conflict, null, error);
    }

    public static SessionActionResult ValidationFailed(string error)
    {
        return new SessionActionResult(SessionActionOutcome.ValidationFailed, null, error);
    }
}
