using System.Text.Json;
using Gatekeeper.Application.AccessRequests;
using Gatekeeper.Application.Common;
using Gatekeeper.Core.AccessRequests;

namespace Gatekeeper.Api.AdminAuthentication;

public sealed class AdminAuthAuditWriter
{
    private readonly IAuditEventRepository _auditEvents;
    private readonly IAccessRequestUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public AdminAuthAuditWriter(
        IAuditEventRepository auditEvents,
        IAccessRequestUnitOfWork unitOfWork,
        IClock clock
    )
    {
        _auditEvents = auditEvents;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task WriteLoginSucceededAsync(string username, CancellationToken cancellationToken)
    {
        await WriteAsync(
            AuditEvent.CreateAdminLoginSucceeded(
                _clock.UtcNow,
                JsonSerializer.Serialize(new Dictionary<string, string> { ["username"] = username })
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
        await WriteAsync(
            AuditEvent.CreateAdminLoginFailed(
                _clock.UtcNow,
                JsonSerializer.Serialize(
                    new Dictionary<string, string> { ["username"] = username, ["reason"] = reason }
                )
            ),
            cancellationToken
        );
    }

    public async Task WriteLogoutAsync(string username, CancellationToken cancellationToken)
    {
        await WriteAsync(
            AuditEvent.CreateAdminLogout(
                _clock.UtcNow,
                JsonSerializer.Serialize(new Dictionary<string, string> { ["username"] = username })
            ),
            cancellationToken
        );
    }

    private async Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        await _auditEvents.AddAsync(auditEvent, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
