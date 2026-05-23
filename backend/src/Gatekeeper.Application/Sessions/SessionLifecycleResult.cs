namespace Gatekeeper.Application.Sessions;

public sealed class SessionLifecycleResult
{
    private SessionLifecycleResult(SessionDetails? session, bool notFound, bool conflict)
    {
        Session = session;
        NotFound = notFound;
        Conflict = conflict;
    }

    public SessionDetails? Session { get; }

    public bool NotFound { get; }

    public bool Conflict { get; }

    public bool Success => Session is not null && !NotFound && !Conflict;

    public static SessionLifecycleResult Succeeded(SessionDetails session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new SessionLifecycleResult(session, notFound: false, conflict: false);
    }

    public static SessionLifecycleResult Missing()
    {
        return new SessionLifecycleResult(null, notFound: true, conflict: false);
    }

    public static SessionLifecycleResult Conflicted(SessionDetails session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new SessionLifecycleResult(session, notFound: false, conflict: true);
    }
}
