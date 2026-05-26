namespace Gatekeeper.Application.Sessions;

public sealed class SshActionPolicyResult
{
    private SshActionPolicyResult(
        bool succeeded,
        SshResolvedAction? resolvedAction,
        SshActionPolicyFailureReason failureReason,
        string? error
    )
    {
        Succeeded = succeeded;
        ResolvedAction = resolvedAction;
        FailureReason = failureReason;
        Error = error;
    }

    public bool Succeeded { get; }

    public SshResolvedAction? ResolvedAction { get; }

    public SshActionPolicyFailureReason FailureReason { get; }

    public string? Error { get; }

    public static SshActionPolicyResult Success(SshResolvedAction resolvedAction)
    {
        ArgumentNullException.ThrowIfNull(resolvedAction);

        return new SshActionPolicyResult(
            true,
            resolvedAction,
            SshActionPolicyFailureReason.None,
            null
        );
    }

    public static SshActionPolicyResult Failed(
        SshActionPolicyFailureReason failureReason,
        string error
    )
    {
        if (failureReason == SshActionPolicyFailureReason.None)
        {
            throw new ArgumentException(
                "Failed SSH action policy results require a failure reason.",
                nameof(failureReason)
            );
        }

        return new SshActionPolicyResult(false, null, failureReason, error);
    }
}
