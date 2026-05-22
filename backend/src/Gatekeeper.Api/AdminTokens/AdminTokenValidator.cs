using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Primitives;

namespace Gatekeeper.Api.AdminTokens;

public sealed class AdminTokenValidator : IAdminTokenValidator
{
    public const string HeaderName = "X-Gatekeeper-Admin-Token";
    private const string ConfigurationKey = "GATEKEEPER_ADMIN_TOKEN";

    private readonly IConfiguration _configuration;

    public AdminTokenValidator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public AdminTokenValidationResult Validate(IHeaderDictionary headers)
    {
        if (!headers.TryGetValue(HeaderName, out StringValues providedValues))
        {
            return AdminTokenValidationResult.MissingHeader;
        }

        string? configuredToken = _configuration[ConfigurationKey];
        if (string.IsNullOrWhiteSpace(configuredToken))
        {
            return AdminTokenValidationResult.Forbidden;
        }

        if (providedValues.Count != 1)
        {
            return AdminTokenValidationResult.Forbidden;
        }

        string? providedToken = providedValues[0];
        if (string.IsNullOrEmpty(providedToken))
        {
            return AdminTokenValidationResult.Forbidden;
        }

        if (!FixedTimeEquals(providedToken, configuredToken))
        {
            return AdminTokenValidationResult.Forbidden;
        }

        return AdminTokenValidationResult.Valid;
    }

    private static bool FixedTimeEquals(string providedToken, string configuredToken)
    {
        byte[] providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedToken));
        byte[] configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(configuredToken));
        return CryptographicOperations.FixedTimeEquals(providedHash, configuredHash);
    }
}
