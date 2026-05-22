namespace Gatekeeper.Application.AccessRequests;

public interface IAccessRequestService
{
    Task<AccessRequestDetails> CreateAsync(
        CreateAccessRequestCommand command,
        CancellationToken cancellationToken
    );

    Task<AccessRequestDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<AccessRequestSummary>> ListAsync(CancellationToken cancellationToken);
}
