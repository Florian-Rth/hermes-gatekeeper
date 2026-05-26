namespace Gatekeeper.Infrastructure.SessionActions.Ssh;

public sealed class SshCommandClientResult
{
    private SshCommandClientResult(
        bool succeeded,
        int exitCode,
        string stdout,
        string stderr,
        bool stdoutTruncated,
        bool stderrTruncated,
        SshCommandClientFailureReason failureReason,
        string? error
    )
    {
        Succeeded = succeeded;
        ExitCode = exitCode;
        Stdout = stdout;
        Stderr = stderr;
        StdoutTruncated = stdoutTruncated;
        StderrTruncated = stderrTruncated;
        FailureReason = failureReason;
        Error = error;
    }

    public bool Succeeded { get; }

    public int ExitCode { get; }

    public string Stdout { get; }

    public string Stderr { get; }

    public bool StdoutTruncated { get; }

    public bool StderrTruncated { get; }

    public SshCommandClientFailureReason FailureReason { get; }

    public string? Error { get; }

    public static SshCommandClientResult Completed(int exitCode, string stdout, string stderr)
    {
        return Completed(exitCode, stdout, stderr, false, false);
    }

    public static SshCommandClientResult Completed(
        int exitCode,
        string stdout,
        string stderr,
        bool stdoutTruncated,
        bool stderrTruncated
    )
    {
        return new SshCommandClientResult(
            true,
            exitCode,
            stdout,
            stderr,
            stdoutTruncated,
            stderrTruncated,
            SshCommandClientFailureReason.None,
            null
        );
    }

    public static SshCommandClientResult Failed(
        SshCommandClientFailureReason failureReason,
        string error
    )
    {
        if (failureReason == SshCommandClientFailureReason.None)
        {
            throw new ArgumentException(
                "Failed SSH command client results require a failure reason.",
                nameof(failureReason)
            );
        }

        return new SshCommandClientResult(
            false,
            0,
            string.Empty,
            string.Empty,
            false,
            false,
            failureReason,
            error
        );
    }
}
