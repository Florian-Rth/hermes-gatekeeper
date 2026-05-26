using System.Net.Sockets;
using System.Text;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace Gatekeeper.Infrastructure.SessionActions.Ssh;

public sealed class SshNetCommandClient : ISshCommandClient
{
    private const char KnownHostsCommentPrefix = '#';
    private const char KnownHostsMarkerPrefix = '@';

    public Task<SshCommandClientResult> ExecuteAsync(
        SshCommandClientRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.Run(() => Execute(request, cancellationToken), cancellationToken);
    }

    internal static string BuildCommandText(SshCommandClientRequest request)
    {
        IEnumerable<string> argv = new[] { request.Executable }.Concat(request.Arguments);
        return string.Join(" ", argv.Select(QuotePosixArgument));
    }

    internal static string QuotePosixArgument(string argument)
    {
        if (argument.Length == 0)
        {
            return "''";
        }

        return "'" + argument.Replace("'", "'\"'\"'") + "'";
    }

    internal static bool IsKnownHostTrusted(
        string knownHostsContent,
        string host,
        int port,
        string hostKeyName,
        byte[] hostKey
    )
    {
        string expectedKey = Convert.ToBase64String(hostKey);
        string bracketedHost = $"[{host}]:{port}";

        foreach (string rawLine in knownHostsContent.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line[0] == KnownHostsCommentPrefix)
            {
                continue;
            }

            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int hostIndex = parts[0].Length > 0 && parts[0][0] == KnownHostsMarkerPrefix ? 1 : 0;
            if (parts.Length <= hostIndex + 2)
            {
                continue;
            }

            if (!HostPatternMatches(parts[hostIndex], host, bracketedHost))
            {
                continue;
            }

            if (!string.Equals(parts[hostIndex + 1], hostKeyName, StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(parts[hostIndex + 2], expectedKey, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static SshCommandClientResult Execute(
        SshCommandClientRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            ValidateRequest(request);
            cancellationToken.ThrowIfCancellationRequested();

            string knownHostsContent = File.ReadAllText(request.KnownHostsPath);
            using var privateKeyFile = new PrivateKeyFile(request.PrivateKeyPath);
            var authenticationMethod = new PrivateKeyAuthenticationMethod(
                request.Username,
                privateKeyFile
            );
            var connectionInfo = new ConnectionInfo(
                request.Host,
                request.Port,
                request.Username,
                authenticationMethod
            )
            {
                Timeout = request.Timeout,
            };

            using var client = new SshClient(connectionInfo);
            client.HostKeyReceived += (_, args) =>
            {
                args.CanTrust = IsKnownHostTrusted(
                    knownHostsContent,
                    request.Host,
                    request.Port,
                    args.HostKeyName,
                    args.HostKey
                );
            };

            cancellationToken.ThrowIfCancellationRequested();
            client.Connect();
            cancellationToken.ThrowIfCancellationRequested();

            using SshCommand command = client.CreateCommand(BuildCommandText(request));
            command.CommandTimeout = request.Timeout;
            IAsyncResult asyncResult = command.BeginExecute();
            if (!asyncResult.AsyncWaitHandle.WaitOne(request.Timeout))
            {
                command.CancelAsync(true, 100);
                return SshCommandClientResult.Failed(
                    SshCommandClientFailureReason.Timeout,
                    "SSH command timed out."
                );
            }

            command.EndExecute(asyncResult);
            BoundedText stdout = ReadBoundedText(command.OutputStream, request.OutputLimitBytes);
            BoundedText stderr = ReadBoundedText(
                command.ExtendedOutputStream,
                request.OutputLimitBytes
            );

            return SshCommandClientResult.Completed(
                command.ExitStatus ?? -1,
                stdout.Value,
                stderr.Value,
                stdout.Truncated,
                stderr.Truncated
            );
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return SshCommandClientResult.Failed(
                SshCommandClientFailureReason.Timeout,
                "SSH command timed out."
            );
        }
        catch (SshOperationTimeoutException)
        {
            return SshCommandClientResult.Failed(
                SshCommandClientFailureReason.Timeout,
                "SSH command timed out."
            );
        }
        catch (SshAuthenticationException)
        {
            return SshCommandClientResult.Failed(
                SshCommandClientFailureReason.AuthenticationFailed,
                "SSH authentication failed."
            );
        }
        catch (SshConnectionException)
        {
            return SshCommandClientResult.Failed(
                SshCommandClientFailureReason.ConnectionFailed,
                "SSH connection failed."
            );
        }
        catch (SocketException)
        {
            return SshCommandClientResult.Failed(
                SshCommandClientFailureReason.ConnectionFailed,
                "SSH connection failed."
            );
        }
        catch (IOException)
        {
            return SshCommandClientResult.Failed(
                SshCommandClientFailureReason.ClientFailed,
                "SSH command client failed."
            );
        }
        catch (Exception)
        {
            return SshCommandClientResult.Failed(
                SshCommandClientFailureReason.ClientFailed,
                "SSH command client failed."
            );
        }
    }

    private static void ValidateRequest(SshCommandClientRequest request)
    {
        if (
            string.IsNullOrWhiteSpace(request.Host)
            || string.IsNullOrWhiteSpace(request.Username)
            || string.IsNullOrWhiteSpace(request.PrivateKeyPath)
            || string.IsNullOrWhiteSpace(request.KnownHostsPath)
            || string.IsNullOrWhiteSpace(request.Executable)
        )
        {
            throw new IOException("SSH command client request is incomplete.");
        }
    }

    private static bool HostPatternMatches(string patterns, string host, string bracketedHost)
    {
        foreach (string pattern in patterns.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (
                string.Equals(pattern, host, StringComparison.OrdinalIgnoreCase)
                || string.Equals(pattern, bracketedHost, StringComparison.OrdinalIgnoreCase)
            )
            {
                return true;
            }
        }

        return false;
    }

    private static BoundedText ReadBoundedText(Stream stream, int outputLimitBytes)
    {
        int effectiveLimit = outputLimitBytes > 0 ? outputLimitBytes : 1;
        byte[] buffer = new byte[4096];
        using var output = new MemoryStream(capacity: effectiveLimit);
        bool truncated = false;

        while (true)
        {
            int read = stream.Read(buffer, 0, buffer.Length);
            if (read == 0)
            {
                break;
            }

            int remaining = effectiveLimit - (int)output.Length;
            if (remaining > 0)
            {
                output.Write(buffer, 0, Math.Min(remaining, read));
            }

            if (read > remaining)
            {
                truncated = true;
            }
        }

        return new BoundedText(DecodeUtf8Prefix(output.ToArray()), truncated);
    }

    private static string DecodeUtf8Prefix(byte[] bytes)
    {
        int length = bytes.Length;
        while (length > 0)
        {
            try
            {
                return new UTF8Encoding(false, true).GetString(bytes, 0, length);
            }
            catch (DecoderFallbackException)
            {
                length--;
            }
        }

        return string.Empty;
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
