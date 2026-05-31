using System.Text.Json;
using Gatekeeper.Application.AccessRequests;
using Gatekeeper.Application.Common;
using Gatekeeper.Core.AccessRequests;
using Microsoft.Extensions.Logging;

namespace Gatekeeper.Api.AgentAuthentication;

public sealed class AgentAuthAuditWriter
{
    private readonly IAuditEventRepository _auditEvents;
    private readonly IAccessRequestUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<AgentAuthAuditWriter> _logger;

    public AgentAuthAuditWriter(
        IAuditEventRepository auditEvents,
        IAccessRequestUnitOfWork unitOfWork,
        IClock clock,
        ILogger<AgentAuthAuditWriter> logger
    )
    {
        _auditEvents = auditEvents;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public async Task WriteFailedAuthenticationAsync(
        string routeTemplate,
        string httpMethod,
        string reasonCode,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await WriteAsync(
                AuditEvent.CreateAgentAuthenticationFailed(
                    _clock.UtcNow,
                    JsonSerializer.Serialize(
                        new Dictionary<string, string>
                        {
                            ["routeTemplate"] = routeTemplate,
                            ["httpMethod"] = httpMethod,
                            ["reasonCode"] = reasonCode,
                            ["authMethod"] = AgentAuthConstants.ApiKeyAuthMethod,
                        }
                    )
                ),
                cancellationToken
            );
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to persist failed-agent-authentication audit for {HttpMethod} {RouteTemplate}.",
                httpMethod,
                routeTemplate
            );
        }
    }

    private async Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        await _auditEvents.AddAsync(auditEvent, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
