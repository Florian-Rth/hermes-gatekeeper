using System.Security.Claims;
using FastEndpoints;
using Gatekeeper.Api.AdminAuthentication;
using Microsoft.AspNetCore.Authentication;

namespace Gatekeeper.Api.Endpoints.Admin;

public sealed class AdminLoginEndpoint : Endpoint<AdminLoginRequest, AdminSessionResponse>
{
    private readonly AdminCredentialVerifier _credentials;
    private readonly AdminLoginRateLimiter _rateLimiter;
    private readonly AdminAuthAuditWriter _auditWriter;
    private readonly AdminAuthOptions _options;

    public AdminLoginEndpoint(
        AdminCredentialVerifier credentials,
        AdminLoginRateLimiter rateLimiter,
        AdminAuthAuditWriter auditWriter,
        AdminAuthOptions options
    )
    {
        _credentials = credentials;
        _rateLimiter = rateLimiter;
        _auditWriter = auditWriter;
        _options = options;
    }

    public override void Configure()
    {
        Post("/api/v1/admin/login");
        AllowAnonymous();
    }

    public override async Task HandleAsync(AdminLoginRequest req, CancellationToken ct)
    {
        string username = req.Username ?? string.Empty;
        if (!_credentials.IsConfigured())
        {
            await Send.StringAsync(
                string.Empty,
                StatusCodes.Status503ServiceUnavailable,
                cancellation: ct
            );
            return;
        }

        if (_rateLimiter.IsLimited(username))
        {
            await _auditWriter.WriteLoginFailedAsync(username, "rate_limited", ct);
            await Send.StringAsync(
                string.Empty,
                StatusCodes.Status429TooManyRequests,
                cancellation: ct
            );
            return;
        }

        if (!_credentials.Verify(username, req.Password))
        {
            _rateLimiter.RecordFailedAttempt(username);
            await _auditWriter.WriteLoginFailedAsync(username, "invalid_credentials", ct);
            await Send.StringAsync(
                string.Empty,
                StatusCodes.Status401Unauthorized,
                cancellation: ct
            );
            return;
        }

        _rateLimiter.Reset(username);
        Claim[] claims = [new Claim(ClaimTypes.Name, _options.Username!)];
        ClaimsIdentity identity = new ClaimsIdentity(claims, AdminAuthConstants.Scheme);
        ClaimsPrincipal principal = new ClaimsPrincipal(identity);
        AuthenticationProperties properties = new AuthenticationProperties
        {
            IsPersistent = false,
            AllowRefresh = true,
        };
        await HttpContext.SignInAsync(AdminAuthConstants.Scheme, principal, properties);
        await _auditWriter.WriteLoginSucceededAsync(_options.Username!, ct);
        await Send.OkAsync(
            new AdminSessionResponse { Authenticated = true, Username = _options.Username! },
            ct
        );
    }
}

public sealed class AdminLoginRequest
{
    public string? Username { get; set; }

    public string? Password { get; set; }
}

public sealed class AdminSessionResponse
{
    public bool Authenticated { get; set; }

    public string Username { get; set; } = string.Empty;
}
