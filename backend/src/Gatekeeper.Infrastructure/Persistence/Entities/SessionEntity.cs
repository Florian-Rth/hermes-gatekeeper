using Gatekeeper.Core.Sessions;

namespace Gatekeeper.Infrastructure.Persistence.Entities;

public sealed class SessionEntity
{
    public Guid Id { get; set; }

    public Guid AccessRequestId { get; set; }

    public SessionStatus Status { get; set; }

    public string AllowedTargetsJson { get; set; } = "[]";

    public string AllowedCapabilitiesJson { get; set; } = "[]";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
}
