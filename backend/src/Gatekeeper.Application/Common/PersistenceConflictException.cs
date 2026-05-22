namespace Gatekeeper.Application.Common;

public sealed class PersistenceConflictException : Exception
{
    public PersistenceConflictException(string message)
        : base(message) { }

    public PersistenceConflictException(string message, Exception innerException)
        : base(message, innerException) { }
}
