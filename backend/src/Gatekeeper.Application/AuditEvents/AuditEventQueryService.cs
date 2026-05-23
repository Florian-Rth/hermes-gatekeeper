using System.Globalization;
using System.Text;

namespace Gatekeeper.Application.AuditEvents;

public sealed class AuditEventQueryService : IAuditEventQueryService
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 100;

    private readonly IAuditEventQueryRepository _repository;

    public AuditEventQueryService(IAuditEventQueryRepository repository)
    {
        _repository = repository;
    }

    public async Task<AuditEventPage> ListAsync(
        AuditEventQuery query,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(query);

        int requestedLimit = query.Limit.GetValueOrDefault(DefaultLimit);
        if (requestedLimit <= 0)
        {
            requestedLimit = DefaultLimit;
        }

        int limit = Math.Min(requestedLimit, MaxLimit);
        AuditEventCursor? cursor = DecodeCursor(query.Cursor);
        AuditEventQueryCriteria criteria = new(
            query.AggregateId,
            string.IsNullOrWhiteSpace(query.EventType) ? null : query.EventType.Trim(),
            query.From,
            query.To,
            cursor,
            limit + 1
        );

        IReadOnlyList<AuditEventSummary> events = await _repository.ListAsync(
            criteria,
            cancellationToken
        );
        AuditEventSummary[] items = events.Take(limit).ToArray();
        string? nextCursor = events.Count > limit ? EncodeCursor(items[^1]) : null;

        return new AuditEventPage(items, nextCursor);
    }

    private static string? EncodeCursor(AuditEventSummary item)
    {
        string cursorValue = string.Join(
            '|',
            item.OccurredAt.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture),
            item.Id.ToString("D")
        );
        return Convert
            .ToBase64String(Encoding.UTF8.GetBytes(cursorValue))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static AuditEventCursor? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        try
        {
            string padded = cursor.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');
            string cursorValue = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            string[] parts = cursorValue.Split('|', 2);
            if (
                parts.Length != 2
                || !long.TryParse(parts[0], CultureInfo.InvariantCulture, out long ticks)
                || !Guid.TryParse(parts[1], out Guid id)
                || id == Guid.Empty
            )
            {
                throw new InvalidAuditEventCursorException();
            }

            DateTimeOffset occurredAt = new(new DateTime(ticks, DateTimeKind.Utc));
            return new AuditEventCursor(occurredAt, id);
        }
        catch (FormatException)
        {
            throw new InvalidAuditEventCursorException();
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new InvalidAuditEventCursorException();
        }
    }
}
