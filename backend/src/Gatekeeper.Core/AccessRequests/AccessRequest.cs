namespace Gatekeeper.Core.AccessRequests;

public sealed class AccessRequest
{
    private AccessRequest(
        Guid id,
        string intent,
        string requester,
        IReadOnlyList<string> targets,
        IReadOnlyList<string> requestedCapabilities,
        int durationMinutes,
        RiskLevel risk,
        string? justification,
        IReadOnlyList<string> proposedActions,
        IReadOnlyList<string> forbiddenActions,
        IReadOnlyDictionary<string, string> metadata,
        AccessRequestStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt
    )
    {
        Id = id;
        Intent = intent;
        Requester = requester;
        Targets = targets;
        RequestedCapabilities = requestedCapabilities;
        DurationMinutes = durationMinutes;
        Risk = risk;
        Justification = justification;
        ProposedActions = proposedActions;
        ForbiddenActions = forbiddenActions;
        Metadata = metadata;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; }

    public string Intent { get; }

    public string Requester { get; }

    public IReadOnlyList<string> Targets { get; }

    public IReadOnlyList<string> RequestedCapabilities { get; }

    public int DurationMinutes { get; }

    public RiskLevel Risk { get; }

    public string? Justification { get; }

    public IReadOnlyList<string> ProposedActions { get; }

    public IReadOnlyList<string> ForbiddenActions { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    public AccessRequestStatus Status { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; }

    public static AccessRequest Create(
        string intent,
        string requester,
        IEnumerable<string> targets,
        IEnumerable<string> requestedCapabilities,
        int durationMinutes,
        RiskLevel risk,
        string? justification,
        IEnumerable<string>? proposedActions,
        IEnumerable<string>? forbiddenActions,
        IReadOnlyDictionary<string, string>? metadata,
        DateTimeOffset now
    )
    {
        if (string.IsNullOrWhiteSpace(intent))
        {
            throw new ArgumentException("Intent is required.", nameof(intent));
        }

        if (string.IsNullOrWhiteSpace(requester))
        {
            throw new ArgumentException("Requester is required.", nameof(requester));
        }

        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(requestedCapabilities);

        var normalizedTargets = NormalizeRequiredList(targets, nameof(targets));
        var normalizedRequestedCapabilities = NormalizeRequiredList(
            requestedCapabilities,
            nameof(requestedCapabilities)
        );

        if (durationMinutes <= 0)
        {
            throw new ArgumentException(
                "Duration must be greater than zero.",
                nameof(durationMinutes)
            );
        }

        if (!Enum.IsDefined(risk))
        {
            throw new ArgumentOutOfRangeException(nameof(risk), risk, "Risk level is invalid.");
        }

        return new AccessRequest(
            Guid.NewGuid(),
            intent.Trim(),
            requester.Trim(),
            normalizedTargets,
            normalizedRequestedCapabilities,
            durationMinutes,
            risk,
            string.IsNullOrWhiteSpace(justification) ? null : justification.Trim(),
            NormalizeOptionalList(proposedActions),
            NormalizeOptionalList(forbiddenActions),
            NormalizeMetadata(metadata),
            AccessRequestStatus.Pending,
            now,
            now
        );
    }

    public static AccessRequest Load(
        Guid id,
        string intent,
        string requester,
        IReadOnlyList<string> targets,
        IReadOnlyList<string> requestedCapabilities,
        int durationMinutes,
        RiskLevel risk,
        string? justification,
        IReadOnlyList<string> proposedActions,
        IReadOnlyList<string> forbiddenActions,
        IReadOnlyDictionary<string, string> metadata,
        AccessRequestStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt
    )
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id is required.", nameof(id));
        }

        return new AccessRequest(
            id,
            intent,
            requester,
            targets,
            requestedCapabilities,
            durationMinutes,
            risk,
            justification,
            proposedActions,
            forbiddenActions,
            metadata,
            status,
            createdAt,
            updatedAt
        );
    }

    private static IReadOnlyList<string> NormalizeRequiredList(
        IEnumerable<string> values,
        string parameterName
    )
    {
        var normalized = NormalizeOptionalList(values);
        if (normalized.Count == 0)
        {
            throw new ArgumentException("At least one value is required.", parameterName);
        }

        return normalized;
    }

    private static IReadOnlyList<string> NormalizeOptionalList(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return [];
        }

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToArray();
    }

    private static IReadOnlyDictionary<string, string> NormalizeMetadata(
        IReadOnlyDictionary<string, string>? metadata
    )
    {
        if (metadata is null)
        {
            return new Dictionary<string, string>();
        }

        return metadata
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .ToDictionary(pair => pair.Key.Trim(), pair => pair.Value);
    }
}
