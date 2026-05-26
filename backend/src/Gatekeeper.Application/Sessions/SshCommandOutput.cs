namespace Gatekeeper.Application.Sessions;

public sealed class SshCommandOutput
{
    public SshCommandOutput(
        int exitCode,
        string stdout,
        string stderr,
        bool stdoutTruncated,
        bool stderrTruncated
    )
    {
        ExitCode = exitCode;
        Stdout = stdout;
        Stderr = stderr;
        StdoutTruncated = stdoutTruncated;
        StderrTruncated = stderrTruncated;
    }

    public int ExitCode { get; }

    public string Stdout { get; }

    public string Stderr { get; }

    public bool StdoutTruncated { get; }

    public bool StderrTruncated { get; }
}
