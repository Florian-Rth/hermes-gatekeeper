using Gatekeeper.Application.Sessions;

namespace Gatekeeper.Application.AccessRequests;

public sealed class ApprovalResult
{
    private ApprovalResult(
        bool success,
        bool notFound,
        bool conflict,
        AccessRequestDetails? accessRequest,
        SessionDetails? session
    )
    {
        Success = success;
        NotFound = notFound;
        Conflict = conflict;
        AccessRequest = accessRequest;
        Session = session;
    }

    public bool Success { get; }

    public bool NotFound { get; }

    public bool Conflict { get; }

    public AccessRequestDetails? AccessRequest { get; }

    public SessionDetails? Session { get; }

    public static ApprovalResult Succeeded(
        AccessRequestDetails accessRequest,
        SessionDetails session
    )
    {
        return new ApprovalResult(true, false, false, accessRequest, session);
    }

    public static ApprovalResult Missing()
    {
        return new ApprovalResult(false, true, false, null, null);
    }

    public static ApprovalResult Conflicted()
    {
        return new ApprovalResult(false, false, true, null, null);
    }
}
