using System.Security.Claims;
using FastEndpoints;
using Gatekeeper.Api.AdminAuthentication;
using Microsoft.AspNetCore.Authentication;

namespace Gatekeeper.Api.Endpoints.Admin;

public sealed class AdminLogoutEndpoint : EndpointWithoutRequest<AdminSessionResponse>
{
    private readonly AdminSessionGuard _guard;
    private readonly AdminAuthAuditWriter _auditWriter;

    public AdminLogoutEndpoint(AdminSessionGuard guard, AdminAuthAuditWriter auditWriter)
    {
        _guard = guard;
        _auditWriter = auditWriter;
    }

    public override void Configure()
    {
        Post("/api/v1/admin/logout");
        AuthSchemes(AdminAuthConstants.Scheme);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!_guard.HasValidUnsafeRequestOrigin(HttpContext))
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        string username = HttpContext.User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
        await HttpContext.SignOutAsync(AdminAuthConstants.Scheme);
        await _auditWriter.WriteLogoutAsync(username, ct);
        await Send.OkAsync(
            new AdminSessionResponse { Authenticated = false, Username = string.Empty },
            ct
        );
    }
}
