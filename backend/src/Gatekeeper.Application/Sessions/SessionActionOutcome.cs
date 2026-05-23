namespace Gatekeeper.Application.Sessions;

public enum SessionActionOutcome
{
    Succeeded = 0,
    NotFound = 1,
    Forbidden = 2,
    Conflict = 3,
    ValidationFailed = 4,
}
