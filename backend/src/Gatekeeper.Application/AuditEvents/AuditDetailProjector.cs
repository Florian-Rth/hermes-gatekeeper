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
            AddProjectedValue(result, key, value);
        }

        return result;
    }

    public static IReadOnlyDictionary<string, string> ProjectFromSerializedPayload(
        string payloadJson
    )
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

            ProjectJsonObject(document.RootElement, details);
            if (
                document.RootElement.TryGetProperty("Details", out JsonElement nestedDetails)
                || document.RootElement.TryGetProperty("details", out nestedDetails)
            )
            {
                ProjectJsonObject(nestedDetails, details);
            }
        }
        catch (JsonException) { }

        return details;
    }

    private static void AddProjectedValue(
        Dictionary<string, string> destination,
        string key,
        object? value
    )
    {
        if (value is null || !AllowedKeys.Contains(key))
        {
            return;
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

        AddBoundedValue(destination, key, stringValue);
    }

    private static void ProjectJsonObject(
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
            string? allowedKey = AllowedKeys.FirstOrDefault(allowed =>
                string.Equals(allowed, property.Name, StringComparison.OrdinalIgnoreCase)
            );
            if (allowedKey is null)
            {
                continue;
            }

            string? stringValue = allowedKey switch
            {
                "safeParameters" when property.Value.ValueKind == JsonValueKind.Object =>
                    SerializeSafeParameters(property.Value),
                "output" when property.Value.ValueKind == JsonValueKind.Object =>
                    SerializeOutputMetadata(property.Value),
                _ => ConvertJsonValueToString(property.Value),
            };

            AddBoundedValue(destination, allowedKey, stringValue);
        }
    }

    private static void AddBoundedValue(
        Dictionary<string, string> destination,
        string key,
        string? value
    )
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        destination[key] = value.Length <= MaxValueLength ? value : value[..MaxValueLength];
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

    private static string? ConvertJsonValueToString(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
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

    private static string? SerializeSafeParameters(JsonElement value)
    {
        Dictionary<string, string> safeParameters = new(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            string? parameterValue = ConvertJsonValueToString(property.Value);
            if (!string.IsNullOrWhiteSpace(parameterValue))
            {
                safeParameters[property.Name] =
                    parameterValue.Length <= MaxParameterValueLength
                        ? parameterValue
                        : parameterValue[..MaxParameterValueLength];
            }
        }

        return safeParameters.Count == 0 ? null : JsonSerializer.Serialize(safeParameters);
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
