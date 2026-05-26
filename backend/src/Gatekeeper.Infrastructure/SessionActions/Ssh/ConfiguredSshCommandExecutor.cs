using System.Text;
using Gatekeeper.Application.Sessions;

namespace Gatekeeper.Infrastructure.SessionActions.Ssh;

public sealed class ConfiguredSshCommandExecutor : ISshCommandExecutor
{
    private readonly SshConnectorOptions _options;
    private readonly ISshCommandClient _client;

    public ConfiguredSshCommandExecutor(SshConnectorOptions options, ISshCommandClient client)
    {
        _options = options;
        _client = client;
    }

    public async Task<SshCommandExecutionResult> ExecuteAsync(
        SshResolvedAction resolvedAction,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(resolvedAction);

        if (!_options.Targets.TryGetValue(resolvedAction.TargetAlias, out SshTargetOptions? target))
        {
            return SshCommandExecutionResult.Failed(
                SshCommandExecutionFailureReason.UnknownTarget,
                "Unknown SSH target."
            );
        }

        if (resolvedAction.Command.Count == 0)
        {
            return SshCommandExecutionResult.Failed(
                SshCommandExecutionFailureReason.InvalidResolvedCommand,
                "Resolved SSH command is empty."
            );
        }

        SshCommandClientRequest request = CreateClientRequest(resolvedAction, target);
        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken
        );
        timeoutCts.CancelAfter(resolvedAction.Timeout);

        SshCommandClientResult clientResult;
        try
        {
            Task<SshCommandClientResult> clientTask = Task.Run(
                () => _client.ExecuteAsync(request, timeoutCts.Token),
                CancellationToken.None
            );
            clientResult = await clientTask.WaitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SshCommandExecutionResult.Failed(
                SshCommandExecutionFailureReason.Timeout,
                SanitizeError(SshCommandClientFailureReason.Timeout)
            );
        }
        if (!clientResult.Succeeded)
        {
            return SshCommandExecutionResult.Failed(
                MapFailureReason(clientResult.FailureReason),
                SanitizeError(clientResult.FailureReason)
            );
        }

        BoundedText stdout = BoundText(clientResult.Stdout, resolvedAction.OutputLimitBytes);
        BoundedText stderr = BoundText(clientResult.Stderr, resolvedAction.OutputLimitBytes);
        var output = new SshCommandOutput(
            clientResult.ExitCode,
            stdout.Value,
            stderr.Value,
            stdout.Truncated || clientResult.StdoutTruncated,
            stderr.Truncated || clientResult.StderrTruncated
        );

        return SshCommandExecutionResult.Completed(output);
    }

    private static SshCommandClientRequest CreateClientRequest(
        SshResolvedAction resolvedAction,
        SshTargetOptions target
    )
    {
        string executable = resolvedAction.Command[0];
        string[] arguments = resolvedAction.Command.Skip(1).ToArray();

        return new SshCommandClientRequest(
            target.Host,
            target.Port,
            target.Username,
            target.PrivateKeyPath,
            target.KnownHostsPath,
            executable,
            arguments,
            resolvedAction.Timeout,
            resolvedAction.OutputLimitBytes
        );
    }

    private static string SanitizeError(SshCommandClientFailureReason failureReason)
    {
        return failureReason switch
        {
            SshCommandClientFailureReason.Timeout => "SSH command timed out.",
            SshCommandClientFailureReason.ConnectionFailed => "SSH connection failed.",
            SshCommandClientFailureReason.AuthenticationFailed => "SSH authentication failed.",
            _ => "SSH command client failed.",
        };
    }

    private static SshCommandExecutionFailureReason MapFailureReason(
        SshCommandClientFailureReason failureReason
    )
    {
        return failureReason switch
        {
            SshCommandClientFailureReason.Timeout => SshCommandExecutionFailureReason.Timeout,
            SshCommandClientFailureReason.ConnectionFailed =>
                SshCommandExecutionFailureReason.ConnectionFailed,
            SshCommandClientFailureReason.AuthenticationFailed =>
                SshCommandExecutionFailureReason.AuthenticationFailed,
            _ => SshCommandExecutionFailureReason.ClientFailed,
        };
    }

    private static BoundedText BoundText(string value, int outputLimitBytes)
    {
        int effectiveLimit = outputLimitBytes > 0 ? outputLimitBytes : 1;
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length <= effectiveLimit)
        {
            return new BoundedText(value, false);
        }

        int length = effectiveLimit;
        while (length > 0)
        {
            try
            {
                string boundedValue = new UTF8Encoding(false, true).GetString(bytes, 0, length);
                return new BoundedText(boundedValue, true);
            }
            catch (DecoderFallbackException)
            {
                length--;
            }
        }

        return new BoundedText(string.Empty, true);
    }

    private sealed class BoundedText
    {
        public BoundedText(string value, bool truncated)
        {
            Value = value;
            Truncated = truncated;
        }

        public string Value { get; }

        public bool Truncated { get; }
    }
}
