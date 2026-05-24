using System.Security.Cryptography;
using System.Text;

namespace Gatekeeper.Api.AdminAuthentication;

public sealed class AdminCredentialVerifier
{
    private readonly AdminAuthOptions _options;

    public AdminCredentialVerifier(AdminAuthOptions options)
    {
        _options = options;
    }

    public bool IsConfigured()
    {
        return _options.IsConfigured();
    }

    public bool Verify(string? username, string? password)
    {
        if (!_options.IsConfigured())
        {
            return false;
        }

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            return false;
        }

        return FixedTimeEquals(username, _options.Username!)
            && FixedTimeEquals(password, _options.Password!);
    }

    private static bool FixedTimeEquals(string providedValue, string configuredValue)
    {
        byte[] providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedValue));
        byte[] configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(configuredValue));
        return CryptographicOperations.FixedTimeEquals(providedHash, configuredHash);
    }
}
