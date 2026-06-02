using System.Text.Json;
using Gatekeeper.Core.Sessions;

namespace Gatekeeper.Infrastructure.Persistence.Repositories;

internal static class JsonColumnSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions(
        JsonSerializerDefaults.Web
    );

    public static string SerializeStringList(IReadOnlyList<string> values)
    {
        return JsonSerializer.Serialize(values, SerializerOptions);
    }

    public static IReadOnlyList<string> DeserializeStringList(string json)
    {
        string[]? values = JsonSerializer.Deserialize<string[]>(json, SerializerOptions);
        return values ?? [];
    }

    public static string SerializeStringDictionary(IReadOnlyDictionary<string, string> values)
    {
        return JsonSerializer.Serialize(values, SerializerOptions);
    }

    public static IReadOnlyDictionary<string, string> DeserializeStringDictionary(string json)
    {
        Dictionary<string, string>? values = JsonSerializer.Deserialize<Dictionary<string, string>>(
            json,
            SerializerOptions
        );
        return values ?? new Dictionary<string, string>();
    }

    public static string SerializeSshProfileGrantList(IReadOnlyList<SshProfileGrant> values)
    {
        return JsonSerializer.Serialize(values, SerializerOptions);
    }

    public static IReadOnlyList<SshProfileGrant> DeserializeSshProfileGrantList(string json)
    {
        SshProfileGrant[]? values = JsonSerializer.Deserialize<SshProfileGrant[]>(
            json,
            SerializerOptions
        );
        return values ?? [];
    }
}
