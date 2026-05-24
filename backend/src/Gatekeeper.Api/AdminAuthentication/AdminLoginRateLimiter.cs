using System.Collections.Concurrent;
using Gatekeeper.Application.Common;

namespace Gatekeeper.Api.AdminAuthentication;

public sealed class AdminLoginRateLimiter
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(5);
    private const int MaxFailedAttempts = 5;

    private readonly ConcurrentDictionary<string, LoginFailureBucket> _failures = new();
    private readonly IClock _clock;

    public AdminLoginRateLimiter(IClock clock)
    {
        _clock = clock;
    }

    public bool IsLimited(string username)
    {
        string key = Normalize(username);
        DateTimeOffset now = _clock.UtcNow;
        if (!_failures.TryGetValue(key, out LoginFailureBucket? bucket))
        {
            return false;
        }

        if (now - bucket.FirstFailureAt >= Window)
        {
            _failures.TryRemove(key, out _);
            return false;
        }

        return bucket.Count >= MaxFailedAttempts;
    }

    public void RecordFailedAttempt(string username)
    {
        string key = Normalize(username);
        DateTimeOffset now = _clock.UtcNow;
        _failures.AddOrUpdate(
            key,
            _ => new LoginFailureBucket(now, 1),
            (_, bucket) =>
            {
                if (now - bucket.FirstFailureAt >= Window)
                {
                    return new LoginFailureBucket(now, 1);
                }

                return new LoginFailureBucket(bucket.FirstFailureAt, bucket.Count + 1);
            }
        );
    }

    public void Reset(string username)
    {
        _failures.TryRemove(Normalize(username), out _);
    }

    private static string Normalize(string username)
    {
        return string.IsNullOrWhiteSpace(username) ? "<empty>" : username.Trim().ToUpperInvariant();
    }

    private sealed class LoginFailureBucket
    {
        public LoginFailureBucket(DateTimeOffset firstFailureAt, int count)
        {
            FirstFailureAt = firstFailureAt;
            Count = count;
        }

        public DateTimeOffset FirstFailureAt { get; }

        public int Count { get; }
    }
}
