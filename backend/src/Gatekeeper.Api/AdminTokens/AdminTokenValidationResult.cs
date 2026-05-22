namespace Gatekeeper.Api.AdminTokens;

public enum AdminTokenValidationResult
{
    Valid,
    MissingHeader,
    Forbidden,
}
