using System.Text.Json;
using System.Text.Json.Nodes;
using Kododo.ConfigWay.Core.Model;

namespace Kododo.ConfigWay.FeatureManagement;

/// <summary>
/// The flags kept in the ConfigWay store: one setting per flag, keyed <c>feature_management:flags/{id}</c>, with the
/// flag in the Microsoft schema as JSON.
/// </summary>
internal static class StoredFlags
{
    public const string Prefix = "feature_management:flags/";

    public static string KeyFor(string id) => Prefix + id;

    /// <summary>The flags among <paramref name="settings"/>, in the order of their ids.</summary>
    public static IReadOnlyList<StoredFlag> Read(IEnumerable<Setting> settings) =>
        settings
            .Where(s => s.Key.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) && s.Key.Length > Prefix.Length)
            .Select(Parse)
            .OrderBy(f => f.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(f => f.Key, StringComparer.Ordinal)
            .ToArray();

    private static StoredFlag Parse(Setting setting)
    {
        var id = setting.Key[Prefix.Length..];
        try
        {
            if (JsonNode.Parse(setting.Value ?? string.Empty) is not JsonObject json)
                return new StoredFlag(setting.Key, id, null, "The stored flag is not a JSON object.");

            // The key decides the id, whatever the JSON says.
            foreach (var key in json.Select(p => p.Key).Where(k => k.Equals("id", StringComparison.OrdinalIgnoreCase)).ToArray())
                json.Remove(key);
            json["id"] = id;
            return new StoredFlag(setting.Key, id, json, null);
        }
        catch (JsonException ex)
        {
            return new StoredFlag(setting.Key, id, null, $"The stored flag is not valid JSON: {ex.Message}");
        }
    }
}

/// <summary>A flag kept in the store.</summary>
/// <param name="Key">The store key.</param>
/// <param name="Id">The flag id, from the key.</param>
/// <param name="Json">The flag; <c>null</c> when the stored value cannot be read.</param>
/// <param name="Error">Why the stored value cannot be read.</param>
internal sealed record StoredFlag(string Key, string Id, JsonObject? Json, string? Error);
