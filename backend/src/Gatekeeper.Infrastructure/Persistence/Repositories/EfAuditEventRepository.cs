using System.Globalization;
using System.Text;
using System.Text.Json;
using Gatekeeper.Application.AccessRequests;
using Gatekeeper.Application.AuditEvents;
using Gatekeeper.Core.AccessRequests;
using Gatekeeper.Infrastructure.Persistence.Entities;
using Microsoft.Data.Sqlite;
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
        StringBuilder sql = new("SELECT * FROM AuditEvents WHERE 1 = 1");
        List<SqliteParameter> parameters = [];

        if (criteria.AggregateId.HasValue)
        {
            sql.Append(" AND AggregateId = @aggregateId");
            parameters.Add(
                new SqliteParameter("@aggregateId", ToSqliteGuid(criteria.AggregateId.Value))
            );
        }

        if (!string.IsNullOrWhiteSpace(criteria.EventType))
        {
            sql.Append(" AND EventType = @eventType");
            parameters.Add(new SqliteParameter("@eventType", criteria.EventType));
        }

        if (criteria.From.HasValue)
        {
            sql.Append(" AND OccurredAt >= @from");
            parameters.Add(
                new SqliteParameter("@from", ToSqliteDateTimeOffset(criteria.From.Value))
            );
        }

        if (criteria.To.HasValue)
        {
            sql.Append(" AND OccurredAt <= @to");
            parameters.Add(new SqliteParameter("@to", ToSqliteDateTimeOffset(criteria.To.Value)));
        }

        if (criteria.Cursor is not null)
        {
            sql.Append(
                " AND (OccurredAt > @cursorOccurredAt OR (OccurredAt = @cursorOccurredAt AND Id > @cursorId))"
            );
            parameters.Add(
                new SqliteParameter(
                    "@cursorOccurredAt",
                    ToSqliteDateTimeOffset(criteria.Cursor.OccurredAt)
                )
            );
            parameters.Add(new SqliteParameter("@cursorId", ToSqliteGuid(criteria.Cursor.Id)));
        }

        sql.Append(" ORDER BY OccurredAt, Id LIMIT @take");
        parameters.Add(new SqliteParameter("@take", criteria.Take));

        AuditEventEntity[] entities = await _dbContext
            .AuditEvents.FromSqlRaw(sql.ToString(), parameters.ToArray<object>())
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

        return entities.Select(ToSummary).ToArray();
    }

    private static AuditEventSummary ToSummary(AuditEventEntity entity)
    {
        return new AuditEventSummary(
            entity.Id,
            entity.EventType,
            entity.AggregateId,
            entity.OccurredAt,
            ExtractBoundedDetails(entity.PayloadJson)
        );
    }

    private static string ToSqliteDateTimeOffset(DateTimeOffset value)
    {
        return value
            .ToUniversalTime()
            .ToString("yyyy-MM-dd HH:mm:ss.FFFFFFFzzz", CultureInfo.InvariantCulture);
    }

    private static string ToSqliteGuid(Guid value)
    {
        return value.ToString("D").ToUpperInvariant();
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

    private static AuditEventEntity ToEntity(AuditEvent auditEvent)
    {
        return new AuditEventEntity
        {
            Id = auditEvent.Id,
            EventType = auditEvent.EventType,
            AggregateId = auditEvent.AggregateId,
            OccurredAt = auditEvent.OccurredAt,
            PayloadJson = auditEvent.PayloadJson,
        };
    }
}
