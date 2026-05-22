namespace Gatekeeper.Application.AccessRequests;

public interface IAccessRequestService
{
    Task<AccessRequestDetails> CreateAsync(
        CreateAccessRequestCommand command,
        CancellationToken cancellationToken
    );

    Task<ApprovalResult> ApproveAsync(
        ApproveAccessRequestCommand command,
        CancellationToken cancellationToken
    );

    Task<DenialResult> DenyAsync(
        DenyAccessRequestCommand command,
        CancellationToken cancellationToken
    );

    Task<AccessRequestDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<AccessRequestSummary>> ListAsync(CancellationToken cancellationToken);
}
