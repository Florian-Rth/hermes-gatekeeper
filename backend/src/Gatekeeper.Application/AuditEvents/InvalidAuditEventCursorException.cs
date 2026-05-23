namespace Gatekeeper.Application.AuditEvents;

public sealed class InvalidAuditEventCursorException : Exception
{
    public InvalidAuditEventCursorException()
        : base("Audit event cursor is invalid.") { }
}
