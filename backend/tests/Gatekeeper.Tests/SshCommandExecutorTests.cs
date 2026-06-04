using Gatekeeper.Application.Sessions;
using Gatekeeper.Core.AccessRequests;
using Gatekeeper.Infrastructure.SessionActions.Ssh;

namespace Gatekeeper.Tests;

public sealed class SshCommandExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_Should_ReturnBoundedOutputAndExitCode_When_CommandCompletes()
    {
        var client = new FakeSshCommandClient(
            SshCommandClientResult.Completed(0, "linux\n", "warning\n")
        );
        ConfiguredSshCommandExecutor executor = new(client);

        SshCommandExecutionResult result = await executor.ExecuteAsync(
            CreateResolvedAction(outputLimitBytes: 32),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Equal(SshCommandExecutionFailureReason.None, result.FailureReason);
        Assert.NotNull(result.Output);
        Assert.Equal(0, result.Output.ExitCode);
        Assert.Equal("linux\n", result.Output.Stdout);
        Assert.Equal("warning\n", result.Output.Stderr);
        Assert.False(result.Output.StdoutTruncated);
        Assert.False(result.Output.StderrTruncated);
    }

    [Fact]
    public async Task ExecuteAsync_Should_TruncateOutputAndMarkIt_When_OutputExceedsLimit()
    {
        var client = new FakeSshCommandClient(
            SshCommandClientResult.Completed(0, "abcdef", "uvwxyz")
        );
        ConfiguredSshCommandExecutor executor = new(client);

        SshCommandExecutionResult result = await executor.ExecuteAsync(
            CreateResolvedAction(outputLimitBytes: 3),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Output);
        Assert.Equal("abc", result.Output.Stdout);
        Assert.Equal("uvw", result.Output.Stderr);
        Assert.True(result.Output.StdoutTruncated);
        Assert.True(result.Output.StderrTruncated);
    }

    [Fact]
    public async Task ExecuteAsync_Should_MapTimeoutToTypedFailure_When_ClientTimesOut()
    {
        var client = new FakeSshCommandClient(
            SshCommandClientResult.Failed(SshCommandClientFailureReason.Timeout, "Timed out.")
        );
        ConfiguredSshCommandExecutor executor = new(client);

        SshCommandExecutionResult result = await executor.ExecuteAsync(
            CreateResolvedAction(outputLimitBytes: 32),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.Succeeded);
        Assert.Equal(SshCommandExecutionFailureReason.Timeout, result.FailureReason);
        Assert.Null(result.Output);
        Assert.Equal("SSH command timed out.", result.Error);
    }

    [Theory]
    [InlineData(
        SshCommandClientFailureReason.ConnectionFailed,
        SshCommandExecutionFailureReason.ConnectionFailed
    )]
    [InlineData(
        SshCommandClientFailureReason.AuthenticationFailed,
        SshCommandExecutionFailureReason.AuthenticationFailed
    )]
    [InlineData(
        SshCommandClientFailureReason.ClientFailed,
        SshCommandExecutionFailureReason.ClientFailed
    )]
    public async Task ExecuteAsync_Should_MapClientFailureToTypedFailure_When_ClientFails(
        SshCommandClientFailureReason clientFailureReason,
        SshCommandExecutionFailureReason executionFailureReason
    )
    {
        var client = new FakeSshCommandClient(
            SshCommandClientResult.Failed(clientFailureReason, "SSH client failed.")
        );
        ConfiguredSshCommandExecutor executor = new(client);

        SshCommandExecutionResult result = await executor.ExecuteAsync(
            CreateResolvedAction(outputLimitBytes: 32),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.Succeeded);
        Assert.Equal(executionFailureReason, result.FailureReason);
        Assert.Null(result.Output);
        Assert.Equal(ExpectedSanitizedError(clientFailureReason), result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_Should_EnforceTimeout_When_ClientIgnoresCancellation()
    {
        var client = new FakeSshCommandClient(_ =>
            new TaskCompletionSource<SshCommandClientResult>().Task
        );
        ConfiguredSshCommandExecutor executor = new(client);

        SshCommandExecutionResult result = await executor.ExecuteAsync(
            CreateResolvedAction(outputLimitBytes: 32, timeout: TimeSpan.FromMilliseconds(25)),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.Succeeded);
        Assert.Equal(SshCommandExecutionFailureReason.Timeout, result.FailureReason);
        Assert.Equal("SSH command timed out.", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_Should_SanitizeClientError_When_ClientErrorContainsSecrets()
    {
        const string secretPath = "/run/secrets/demo-key";
        var client = new FakeSshCommandClient(
            SshCommandClientResult.Failed(
                SshCommandClientFailureReason.AuthenticationFailed,
                $"Failed reading {secretPath} for gatekeeper-readonly@demo-ssh."
            )
        );
        ConfiguredSshCommandExecutor executor = new(client);

        SshCommandExecutionResult result = await executor.ExecuteAsync(
            CreateResolvedAction(outputLimitBytes: 32),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.Succeeded);
        Assert.Equal(SshCommandExecutionFailureReason.AuthenticationFailed, result.FailureReason);
        Assert.Equal("SSH authentication failed.", result.Error);
        Assert.DoesNotContain(secretPath, result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("demo-ssh", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("gatekeeper-readonly", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_Should_TruncateUtf8WithoutSplittingMultibyteCharacter()
    {
        var client = new FakeSshCommandClient(SshCommandClientResult.Completed(0, "éé", "😀x"));
        ConfiguredSshCommandExecutor executor = new(client);

        SshCommandExecutionResult result = await executor.ExecuteAsync(
            CreateResolvedAction(outputLimitBytes: 3),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Output);
        Assert.Equal("é", result.Output.Stdout);
        Assert.Equal(string.Empty, result.Output.Stderr);
        Assert.True(result.Output.StdoutTruncated);
        Assert.True(result.Output.StderrTruncated);
    }

    [Fact]
    public async Task ExecuteAsync_Should_ReturnNonZeroExitStructurally_When_CommandRan()
    {
        var client = new FakeSshCommandClient(
            SshCommandClientResult.Completed(3, string.Empty, "inactive\n")
        );
        ConfiguredSshCommandExecutor executor = new(client);

        SshCommandExecutionResult result = await executor.ExecuteAsync(
            CreateResolvedAction(outputLimitBytes: 32),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Equal(SshCommandExecutionFailureReason.None, result.FailureReason);
        Assert.NotNull(result.Output);
        Assert.Equal(3, result.Output.ExitCode);
        Assert.Equal("inactive\n", result.Output.Stderr);
    }

    [Fact]
    public async Task ExecuteAsync_Should_UseResolvedCommandArgv_When_CreatingClientRequest()
    {
        var client = new FakeSshCommandClient(
            SshCommandClientResult.Completed(0, string.Empty, string.Empty)
        );
        ConfiguredSshCommandExecutor executor = new(client);
        SshResolvedAction resolvedAction = CreateResolvedAction(
            new[] { "systemctl", "is-active", "ssh; rm -rf /" },
            outputLimitBytes: 32
        );

        await executor.ExecuteAsync(resolvedAction, TestContext.Current.CancellationToken);

        Assert.NotNull(client.LastRequest);
        Assert.Equal("systemctl", client.LastRequest.Executable);
        Assert.Equal(new[] { "is-active", "ssh; rm -rf /" }, client.LastRequest.Arguments);
        Assert.Equal("demo-ssh", client.LastRequest.Host);
        Assert.Equal(2222, client.LastRequest.Port);
        Assert.Equal("gatekeeper-readonly", client.LastRequest.Username);
        Assert.Equal(TimeSpan.FromSeconds(5), client.LastRequest.Timeout);
        Assert.Equal(32, client.LastRequest.OutputLimitBytes);
    }

    [Fact]
    public async Task ExecuteAsync_Should_FailClosed_When_ResolvedActionHasNoConnectionData()
    {
        var client = new FakeSshCommandClient(
            SshCommandClientResult.Completed(0, string.Empty, string.Empty)
        );
        ConfiguredSshCommandExecutor executor = new(client);
        SshResolvedAction resolvedAction = new(
            "missing-target",
            "system.status.read",
            new[] { "uname", "-a" },
            new Dictionary<string, string>(StringComparer.Ordinal),
            TimeSpan.FromSeconds(5),
            32,
            false,
            RiskLevel.Low,
            host: string.Empty,
            port: 0,
            username: string.Empty,
            privateKeyPath: string.Empty,
            knownHostsPath: string.Empty
        );

        SshCommandExecutionResult result = await executor.ExecuteAsync(
            resolvedAction,
            TestContext.Current.CancellationToken
        );

        Assert.False(result.Succeeded);
        Assert.Equal(SshCommandExecutionFailureReason.UnknownTarget, result.FailureReason);
        Assert.Null(client.LastRequest);
    }

    [Fact]
    public void IsKnownHostTrusted_Should_AcceptMatchingKnownHostsEntry()
    {
        byte[] hostKey = Convert.FromBase64String("AQIDBAU=");
        string knownHosts = "[demo-ssh]:2222 ssh-ed25519 AQIDBAU=";

        bool trusted = SshNetCommandClient.IsKnownHostTrusted(
            knownHosts,
            "demo-ssh",
            2222,
            "ssh-ed25519",
            hostKey
        );

        Assert.True(trusted);
    }

    [Fact]
    public void IsKnownHostTrusted_Should_RejectMismatchedKnownHostsEntry()
    {
        byte[] hostKey = Convert.FromBase64String("AQIDBAU=");
        string knownHosts = "[demo-ssh]:2222 ssh-ed25519 BQYHCAk=";

        bool trusted = SshNetCommandClient.IsKnownHostTrusted(
            knownHosts,
            "demo-ssh",
            2222,
            "ssh-ed25519",
            hostKey
        );

        Assert.False(trusted);
    }

    [Fact]
    public void BuildCommandText_Should_PosixQuoteResolvedArgv()
    {
        var request = new SshCommandClientRequest(
            "demo-ssh",
            22,
            "readonly",
            "/key",
            "/known_hosts",
            "printf",
            new[] { "hello world", "it's", "$(whoami)", string.Empty },
            TimeSpan.FromSeconds(5),
            128
        );

        string commandText = SshNetCommandClient.BuildCommandText(request);

        Assert.Equal("'printf' 'hello world' 'it'\"'\"'s' '$(whoami)' ''", commandText);
    }

    [Fact]
    public void ReadBoundedText_Should_RewindSeekableStreamsBeforeReading()
    {
        var stream = new MemoryStream("linux\n"u8.ToArray());
        stream.Seek(0, SeekOrigin.End);

        SshNetCommandClient.BoundedText output = SshNetCommandClient.ReadBoundedText(stream, 32);

        Assert.Equal("linux\n", output.Value);
        Assert.False(output.Truncated);
    }

    private static SshResolvedAction CreateResolvedAction(int outputLimitBytes)
    {
        return CreateResolvedAction(new[] { "uname", "-a" }, outputLimitBytes);
    }

    private static SshResolvedAction CreateResolvedAction(
        IReadOnlyList<string> command,
        int outputLimitBytes,
        TimeSpan? timeout = null
    )
    {
        return new SshResolvedAction(
            "demo-ssh",
            "system.status.read",
            command,
            new Dictionary<string, string>(StringComparer.Ordinal),
            timeout ?? TimeSpan.FromSeconds(5),
            outputLimitBytes,
            false,
            RiskLevel.Low,
            host: "demo-ssh",
            port: 2222,
            username: "gatekeeper-readonly",
            privateKeyPath: "/run/secrets/demo-key",
            knownHostsPath: "/app/config/known_hosts"
        );
    }

    private static SshResolvedAction CreateResolvedAction(int outputLimitBytes, TimeSpan timeout)
    {
        return CreateResolvedAction(new[] { "uname", "-a" }, outputLimitBytes, timeout);
    }

    private static string ExpectedSanitizedError(SshCommandClientFailureReason failureReason)
    {
        return failureReason switch
        {
            SshCommandClientFailureReason.ConnectionFailed => "SSH connection failed.",
            SshCommandClientFailureReason.AuthenticationFailed => "SSH authentication failed.",
            _ => "SSH command client failed.",
        };
    }

    private sealed class FakeSshCommandClient : ISshCommandClient
    {
        private readonly Func<SshCommandClientRequest, Task<SshCommandClientResult>> _execute;

        public FakeSshCommandClient(SshCommandClientResult result)
        {
            _execute = _ => Task.FromResult(result);
        }

        public FakeSshCommandClient(
            Func<SshCommandClientRequest, Task<SshCommandClientResult>> execute
        )
        {
            _execute = execute;
        }

        public SshCommandClientRequest? LastRequest { get; private set; }

        public Task<SshCommandClientResult> ExecuteAsync(
            SshCommandClientRequest request,
            CancellationToken cancellationToken
        )
        {
            LastRequest = request;
            return _execute(request);
        }
    }
}
