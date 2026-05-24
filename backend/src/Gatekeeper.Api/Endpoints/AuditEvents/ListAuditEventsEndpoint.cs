using System.Globalization;
using FastEndpoints;
using Gatekeeper.Api.AdminAuthentication;
using Gatekeeper.Application.AuditEvents;

namespace Gatekeeper.Api.Endpoints.AuditEvents;

public sealed class ListAuditEventsEndpoint
    : Endpoint<ListAuditEventsRequest, ListAuditEventsResponse>
{
    private readonly AdminSessionGuard _adminSessionGuard;
    private readonly IAuditEventQueryService _auditEvents;

    public ListAuditEventsEndpoint(
        AdminSessionGuard adminSessionGuard,
        IAuditEventQueryService auditEvents
    )
    {
        _adminSessionGuard = adminSessionGuard;
        _auditEvents = auditEvents;
    }

    public override void Configure()
    {
        Get("/api/v1/audit-events");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ListAuditEventsRequest request, CancellationToken ct)
    {
        if (!_adminSessionGuard.IsAuthenticated(HttpContext))
        {
            await Send.StringAsync(
                string.Empty,
                StatusCodes.Status401Unauthorized,
                cancellation: ct
            );
            return;
        }

        if (
            !TryParseRequest(
                request,
                out Guid? aggregateId,
                out DateTimeOffset? from,
                out DateTimeOffset? to
            )
        )
        {
            await Send.StringAsync(string.Empty, StatusCodes.Status400BadRequest, cancellation: ct);
            return;
        }

        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            await Send.StringAsync(string.Empty, StatusCodes.Status400BadRequest, cancellation: ct);
            return;
        }

        AuditEventPage page;
        try
        {
            page = await _auditEvents.ListAsync(
                new AuditEventQuery(
                    aggregateId,
                    request.EventType,
                    from,
                    to,
                    request.Cursor,
                    request.Limit
                ),
                ct
            );
        }
        catch (InvalidAuditEventCursorException)
        {
            await Send.StringAsync(string.Empty, StatusCodes.Status400BadRequest, cancellation: ct);
            return;
        }

        await Send.OkAsync(ListAuditEventsResponse.FromPage(page), ct);
    }

    private static bool TryParseRequest(
        ListAuditEventsRequest request,
        out Guid? aggregateId,
        out DateTimeOffset? from,
        out DateTimeOffset? to
    )
    {
        aggregateId = null;
        from = null;
        to = null;

        if (!string.IsNullOrWhiteSpace(request.AggregateId))
        {
            if (!Guid.TryParse(request.AggregateId, out Guid parsedAggregateId))
            {
                return false;
            }

            aggregateId = parsedAggregateId;
        }

        if (!TryParseDateTimeOffset(request.From, out from))
        {
            return false;
        }

        return TryParseDateTimeOffset(request.To, out to);
    }

    private static bool TryParseDateTimeOffset(string? value, out DateTimeOffset? parsed)
    {
        parsed = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (
            !DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTimeOffset dateTimeOffset
            )
        )
        {
            return false;
        }

        parsed = dateTimeOffset.ToUniversalTime();
        return true;
    }
}

public sealed class ListAuditEventsRequest
{
    public string? AggregateId { get; set; }

    public string? EventType { get; set; }

    public string? From { get; set; }

    public string? To { get; set; }

    public string? Cursor { get; set; }

    public int? Limit { get; set; }
}

public sealed class ListAuditEventsResponse
{
    public IReadOnlyList<ListAuditEventItemResponse> Items { get; set; } = [];

    public string? NextCursor { get; set; }

    public static ListAuditEventsResponse FromPage(AuditEventPage page)
    {
        return new ListAuditEventsResponse
        {
            Items = page.Items.Select(ListAuditEventItemResponse.FromSummary).ToArray(),
            NextCursor = page.NextCursor,
        };
    }
}

public sealed class ListAuditEventItemResponse
{
    public Guid Id { get; set; }

    public string EventType { get; set; } = string.Empty;

    public Guid? AggregateId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public IReadOnlyDictionary<string, string> Details { get; set; } =
        new Dictionary<string, string>();

    public static ListAuditEventItemResponse FromSummary(AuditEventSummary auditEvent)
    {
        return new ListAuditEventItemResponse
        {
            Id = auditEvent.Id,
            EventType = auditEvent.EventType,
            AggregateId = auditEvent.AggregateId,
            OccurredAt = auditEvent.OccurredAt,
            Details = auditEvent.Details,
        };
    }
}
