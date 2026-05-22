namespace Gatekeeper.Api.AdminTokens;

public interface IAdminTokenValidator
{
    AdminTokenValidationResult Validate(IHeaderDictionary headers);
}
