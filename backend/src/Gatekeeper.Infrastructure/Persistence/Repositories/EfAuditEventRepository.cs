using System.Text.Json;
using Gatekeeper.Application.AccessRequests;
using Gatekeeper.Application.AuditEvents;
using Gatekeeper.Core.AccessRequests;
using Gatekeeper.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatekeeper.Infrastructure.Persistence.Repositories;

public sealed class EfAuditEventRepository : IAuditEventRepository, IAuditEventQueryRepository
{
    private static readonly string[] DetailAllowList =
    [
        "action",
        "outcome",
        "reason",
        "status",
        "requester",
        "risk",
        "isMutating",
        "target",
        "capability",
        "comment",
        "sessionId",
        "accessRequestId",
        "targetAlias",
        "safeParameters",
        "exitStatus",
        "durationMs",
        "timedOut",
        "stdoutTruncated",
        "stderrTruncated",
        "output",
        "reasonCode",
        "routeTemplate",
        "httpMethod",
        "agentId",
        "authMethod",
    ];

    private readonly GatekeeperDbContext _dbContext;

    public EfAuditEventRepository(GatekeeperDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        await _dbContext.AuditEvents.AddAsync(ToEntity(auditEvent), cancellationToken);
    }

    public async Task<IReadOnlyList<AuditEventSummary>> ListAsync(
        AuditEventQueryCriteria criteria,
        CancellationToken cancellationToken
    )
    {
        IQueryable<AuditEventEntity> query = _dbContext.AuditEvents.AsNoTracking();

        if (criteria.AggregateId.HasValue)
        {
            Guid aggregateId = criteria.AggregateId.Value;
            query = query.Where(entity => entity.AggregateId == aggregateId);
        }

        if (!string.IsNullOrWhiteSpace(criteria.EventType))
        {
            string eventType = criteria.EventType;
            query = query.Where(entity => entity.EventType == eventType);
        }

        AuditEventEntity[] orderedEntities = (await query.ToArrayAsync(cancellationToken))
            .Where(entity => !criteria.From.HasValue || entity.OccurredAt >= criteria.From.Value)
            .Where(entity => !criteria.To.HasValue || entity.OccurredAt <= criteria.To.Value)
            .OrderBy(entity => entity.OccurredAt)
            .ThenBy(entity => entity.Id)
            .ToArray();

        if (criteria.Cursor is not null)
        {
            DateTimeOffset cursorOccurredAt = criteria.Cursor.OccurredAt;
            Guid cursorId = criteria.Cursor.Id;

            orderedEntities = orderedEntities
                .Where(entity =>
                    entity.OccurredAt > cursorOccurredAt
                    || (entity.OccurredAt == cursorOccurredAt && entity.Id.CompareTo(cursorId) > 0)
                )
                .ToArray();
        }

        return orderedEntities.Take(criteria.Take).Select(ToSummary).ToArray();
    }

    internal static AuditEventEntity ToEntity(AuditEvent auditEvent)
    {
        return new AuditEventEntity
        {
            Id = auditEvent.Id,
            EventType = auditEvent.EventType,
            AggregateId = auditEvent.AggregateId,
            OccurredAt = auditEvent.OccurredAt,
            PayloadJson = auditEvent.PayloadJson,
            DetailsJson = auditEvent.BoundedDetails is not null
                ? JsonSerializer.Serialize(auditEvent.BoundedDetails)
                : null,
        };
    }

    private static AuditEventSummary ToSummary(AuditEventEntity entity)
    {
        return new AuditEventSummary(
            entity.Id,
            entity.EventType,
            entity.AggregateId,
            entity.OccurredAt,
            DeserializeDetailsOrFallback(entity)
        );
    }

    private static IReadOnlyDictionary<string, string> DeserializeDetailsOrFallback(
        AuditEventEntity entity
    )
    {
        if (!string.IsNullOrWhiteSpace(entity.DetailsJson))
        {
            try
            {
                Dictionary<string, string>? details = JsonSerializer.Deserialize<
                    Dictionary<string, string>
                >(entity.DetailsJson);
                if (details is not null)
                {
                    return details;
                }
            }
            catch (JsonException) { }
        }

        return ExtractBoundedDetails(entity.PayloadJson);
    }

    private static IReadOnlyDictionary<string, string> ExtractBoundedDetails(string payloadJson)
    {
        Dictionary<string, string> details = new(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return details;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(payloadJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return details;
            }

            ExtractBoundedDetails(document.RootElement, details);
            if (
                document.RootElement.TryGetProperty("Details", out JsonElement nestedDetails)
                || document.RootElement.TryGetProperty("details", out nestedDetails)
            )
            {
                ExtractBoundedDetails(nestedDetails, details);
            }
        }
        catch (JsonException) { }

        return details;
    }

    private static void ExtractBoundedDetails(
        JsonElement source,
        Dictionary<string, string> destination
    )
    {
        if (source.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (JsonProperty property in source.EnumerateObject())
        {
            string? key = DetailAllowList.FirstOrDefault(allowed =>
                string.Equals(allowed, property.Name, StringComparison.OrdinalIgnoreCase)
            );
            if (key is null)
            {
                continue;
            }

            string? detailValue = ExtractDetailValue(key, property.Value);

            if (!string.IsNullOrWhiteSpace(detailValue))
            {
                destination[key] = detailValue.Length <= 200 ? detailValue : detailValue[..200];
            }
        }
    }

    private static string? ExtractDetailValue(string key, JsonElement value)
    {
        if (string.Equals(key, "safeParameters", StringComparison.Ordinal))
        {
            return value.ValueKind == JsonValueKind.Object ? SerializeSafeParameters(value) : null;
        }

        if (string.Equals(key, "output", StringComparison.Ordinal))
        {
            return value.ValueKind == JsonValueKind.Object ? SerializeOutputMetadata(value) : null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };
    }

    private static string? SerializeSafeParameters(JsonElement value)
    {
        Dictionary<string, string> safeParameters = new(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            string? parameterValue = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Number => property.Value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null,
            };

            if (!string.IsNullOrWhiteSpace(parameterValue))
            {
                safeParameters[property.Name] =
                    parameterValue.Length <= 100 ? parameterValue : parameterValue[..100];
            }
        }

        return safeParameters.Count == 0 ? null : JsonSerializer.Serialize(safeParameters);
    }

    private static string? SerializeOutputMetadata(JsonElement value)
    {
        Dictionary<string, int> outputMetadata = new(StringComparer.Ordinal);
        if (TryGetInt32(value, "StdoutBytes", out int stdoutBytes))
        {
            outputMetadata["stdoutBytes"] = stdoutBytes;
        }

        if (TryGetInt32(value, "StderrBytes", out int stderrBytes))
        {
            outputMetadata["stderrBytes"] = stderrBytes;
        }

        return outputMetadata.Count == 0 ? null : JsonSerializer.Serialize(outputMetadata);
    }

    private static bool TryGetInt32(JsonElement source, string propertyName, out int value)
    {
        foreach (JsonProperty property in source.EnumerateObject())
        {
            if (
                string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase)
                && property.Value.ValueKind == JsonValueKind.Number
                && property.Value.TryGetInt32(out value)
            )
            {
                return true;
            }
        }

        value = 0;
        return false;
    }
}
