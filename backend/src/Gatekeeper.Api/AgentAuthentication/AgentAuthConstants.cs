namespace Gatekeeper.Api.AgentAuthentication;

public static class AgentAuthConstants
{
    public const string SectionName = "AgentAuthentication";
    public const string HeaderName = "X-Gatekeeper-Agent-Key";
    public const string Scheme = "GatekeeperAgent";
    public const string ApiKeyAuthMethod = "apiKey";
    public const string MissingKeyReason = "missing_key";
    public const string MalformedKeyReason = "malformed_key";
    public const string InvalidKeyReason = "invalid_key";
    public const string AuthNotConfiguredReason = "auth_not_configured";
}
