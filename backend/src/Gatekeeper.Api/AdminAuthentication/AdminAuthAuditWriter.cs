using System.Text.Json;
using Gatekeeper.Application.AccessRequests;
using Gatekeeper.Application.AuditEvents;
using Gatekeeper.Application.Common;
using Gatekeeper.Core.AccessRequests;

namespace Gatekeeper.Api.AdminAuthentication;

public sealed class AdminAuthAuditWriter
{
    private readonly IAuditEventRepository _auditEvents;
    private readonly IClock _clock;

    public AdminAuthAuditWriter(IAuditEventRepository auditEvents, IClock clock)
    {
        _auditEvents = auditEvents;
        _clock = clock;
    }

    public async Task WriteLoginSucceededAsync(string username, CancellationToken cancellationToken)
    {
        Dictionary<string, string> payload = new() { ["username"] = username };
        await WriteAsync(
            AuditEvent.CreateAdminLoginSucceeded(
                _clock.UtcNow,
                JsonSerializer.Serialize(payload),
                AuditDetailProjector.Project(
                    new Dictionary<string, object?> { ["requester"] = username }
                )
            ),
            cancellationToken
        );
    }

    public async Task WriteLoginFailedAsync(
        string username,
        string reason,
        CancellationToken cancellationToken
    )
    {
        Dictionary<string, string> payload = new() { ["username"] = username, ["reason"] = reason };
        await WriteAsync(
            AuditEvent.CreateAdminLoginFailed(
                _clock.UtcNow,
                JsonSerializer.Serialize(payload),
                AuditDetailProjector.Project(
                    new Dictionary<string, object?>
                    {
                        ["requester"] = username,
                        ["reason"] = reason,
                    }
                )
            ),
            cancellationToken
        );
    }

    public async Task WriteLogoutAsync(string username, CancellationToken cancellationToken)
    {
        Dictionary<string, string> payload = new() { ["username"] = username };
        await WriteAsync(
            AuditEvent.CreateAdminLogout(
                _clock.UtcNow,
                JsonSerializer.Serialize(payload),
                AuditDetailProjector.Project(
                    new Dictionary<string, object?> { ["requester"] = username }
                )
            ),
            cancellationToken
        );
    }

    private async Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        await _auditEvents.AddAsync(auditEvent, cancellationToken);
        await _auditEvents.SaveChangesAsync(cancellationToken);
    }
}
