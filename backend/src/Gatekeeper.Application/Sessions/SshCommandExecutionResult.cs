namespace Gatekeeper.Application.Sessions;

public sealed class SshCommandExecutionResult
{
    private SshCommandExecutionResult(
        bool succeeded,
        SshCommandOutput? output,
        SshCommandExecutionFailureReason failureReason,
        string? error
    )
    {
        Succeeded = succeeded;
        Output = output;
        FailureReason = failureReason;
        Error = error;
    }

    public bool Succeeded { get; }

    public SshCommandOutput? Output { get; }

    public SshCommandExecutionFailureReason FailureReason { get; }

    public string? Error { get; }

    public static SshCommandExecutionResult Completed(SshCommandOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        return new SshCommandExecutionResult(
            true,
            output,
            SshCommandExecutionFailureReason.None,
            null
        );
    }

    public static SshCommandExecutionResult Failed(
        SshCommandExecutionFailureReason failureReason,
        string error
    )
    {
        if (failureReason == SshCommandExecutionFailureReason.None)
        {
            throw new ArgumentException(
                "Failed SSH command execution results require a failure reason.",
                nameof(failureReason)
            );
        }

        return new SshCommandExecutionResult(false, null, failureReason, error);
    }
}
