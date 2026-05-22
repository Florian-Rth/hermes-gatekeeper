namespace Gatekeeper.Application.AccessRequests;

public sealed class DenialResult
{
    private DenialResult(
        bool success,
        bool notFound,
        bool conflict,
        AccessRequestDetails? accessRequest
    )
    {
        Success = success;
        NotFound = notFound;
        Conflict = conflict;
        AccessRequest = accessRequest;
    }

    public bool Success { get; }

    public bool NotFound { get; }

    public bool Conflict { get; }

    public AccessRequestDetails? AccessRequest { get; }

    public static DenialResult Succeeded(AccessRequestDetails accessRequest)
    {
        return new DenialResult(true, false, false, accessRequest);
    }

    public static DenialResult Missing()
    {
        return new DenialResult(false, true, false, null);
    }

    public static DenialResult Conflicted()
    {
        return new DenialResult(false, false, true, null);
    }
}
