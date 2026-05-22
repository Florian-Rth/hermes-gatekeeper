namespace Gatekeeper.Application.Common;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
