namespace Gatekeeper.Api.AdminAuthentication;

public sealed class AdminAuthOptions
{
    private const string DefaultCookieName = AdminAuthConstants.DefaultCookieName;
    private const int DefaultIdleMinutes = 60;

    public AdminAuthOptions(IConfiguration configuration)
    {
        Username = configuration["GATEKEEPER_ADMIN_USERNAME"];
        Password = configuration["GATEKEEPER_ADMIN_PASSWORD"];
        CookieName = string.IsNullOrWhiteSpace(configuration["GATEKEEPER_ADMIN_COOKIE_NAME"])
            ? DefaultCookieName
            : configuration["GATEKEEPER_ADMIN_COOKIE_NAME"]!;
        CookieSecure = ParseBool(configuration["GATEKEEPER_ADMIN_COOKIE_SECURE"], true);
        SessionIdleMinutes = ParsePositiveInt(
            configuration["GATEKEEPER_ADMIN_SESSION_IDLE_MINUTES"],
            DefaultIdleMinutes
        );
    }

    public string? Username { get; }

    public string? Password { get; }

    public string CookieName { get; }

    public bool CookieSecure { get; }

    public int SessionIdleMinutes { get; }

    public bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
    }

    private static bool ParseBool(string? value, bool fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return bool.TryParse(value, out bool parsed) ? parsed : fallback;
    }

    private static int ParsePositiveInt(string? value, int fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return int.TryParse(value, out int parsed) && parsed > 0 ? parsed : fallback;
    }
}
