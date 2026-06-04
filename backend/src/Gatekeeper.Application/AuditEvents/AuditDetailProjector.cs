using System.Text.Json;

namespace Gatekeeper.Application.AuditEvents;

public static class AuditDetailProjector
{
    private static readonly HashSet<string> AllowedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
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
    };

    private const int MaxValueLength = 200;
    private const int MaxParameterValueLength = 100;

    public static IReadOnlyDictionary<string, string> Project(Dictionary<string, object?> source)
    {
        Dictionary<string, string> result = new(StringComparer.Ordinal);

        foreach ((string key, object? value) in source)
        {
            if (value is null || !AllowedKeys.Contains(key))
            {
                continue;
            }

            string? stringValue = key switch
            {
                "safeParameters" when value is IReadOnlyDictionary<string, string> parameters =>
                    SerializeSafeParameters(parameters),
                "output" when value is AuditOutputMetadata metadata => SerializeOutputMetadata(
                    metadata
                ),
                _ => ConvertToString(value),
            };

            if (!string.IsNullOrWhiteSpace(stringValue))
            {
                result[key] =
                    stringValue.Length <= MaxValueLength
                        ? stringValue
                        : stringValue[..MaxValueLength];
            }
        }

        return result;
    }

    private static string? ConvertToString(object value)
    {
        return value switch
        {
            string s => s,
            Guid g => g.ToString(),
            int i => i.ToString(),
            long l => l.ToString(),
            bool b => b ? "true" : "false",
            _ => value.ToString(),
        };
    }

    private static string? SerializeSafeParameters(IReadOnlyDictionary<string, string> parameters)
    {
        if (parameters.Count == 0)
        {
            return null;
        }

        Dictionary<string, string> bounded = new(StringComparer.Ordinal);
        foreach ((string key, string value) in parameters)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                bounded[key] =
                    value.Length <= MaxParameterValueLength
                        ? value
                        : value[..MaxParameterValueLength];
            }
        }

        return bounded.Count == 0 ? null : JsonSerializer.Serialize(bounded);
    }

    private static string? SerializeOutputMetadata(AuditOutputMetadata metadata)
    {
        Dictionary<string, int> entries = new(StringComparer.Ordinal);
        if (metadata.StdoutBytes.HasValue)
        {
            entries["stdoutBytes"] = metadata.StdoutBytes.Value;
        }

        if (metadata.StderrBytes.HasValue)
        {
            entries["stderrBytes"] = metadata.StderrBytes.Value;
        }

        return entries.Count == 0 ? null : JsonSerializer.Serialize(entries);
    }
}
