namespace Gatekeeper.Application.AccessRequests;

public sealed class ApproveAccessRequestCommand
{
    public ApproveAccessRequestCommand(Guid accessRequestId, string? comment)
    {
        AccessRequestId = accessRequestId;
        Comment = comment;
    }

    public Guid AccessRequestId { get; }

    public string? Comment { get; }
}
