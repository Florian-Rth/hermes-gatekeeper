using Gatekeeper.Core.Sessions;

namespace Gatekeeper.Application.Sessions;

public sealed class SessionLifecycleOptions
{
    public const int DefaultMaxActionCount = Session.DefaultMaxActionCount;

    public SessionLifecycleOptions(int maxActionCount)
    {
        ValidateMaxActionCount(maxActionCount, nameof(maxActionCount));

        MaxActionCount = maxActionCount;
    }

    public int MaxActionCount { get; }

    public static SessionLifecycleOptions Default { get; } =
        new SessionLifecycleOptions(DefaultMaxActionCount);

    public static SessionLifecycleOptions FromConfiguredValue(string? configuredValue)
    {
        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            return Default;
        }

        if (!int.TryParse(configuredValue, out int maxActionCount))
        {
            throw new InvalidOperationException(
                $"GATEKEEPER_SESSION_MAX_ACTION_COUNT must be an integer between 1 and {Session.MaxAllowedActionCount}."
            );
        }

        return new SessionLifecycleOptions(maxActionCount);
    }

    private static void ValidateMaxActionCount(int maxActionCount, string paramName)
    {
        if (maxActionCount < 1 || maxActionCount > Session.MaxAllowedActionCount)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                maxActionCount,
                $"Max action count must be between 1 and {Session.MaxAllowedActionCount}."
            );
        }
    }
}
