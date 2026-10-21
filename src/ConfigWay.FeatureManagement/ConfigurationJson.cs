using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;

namespace Kododo.ConfigWay.FeatureManagement;

/// <summary>Converts between configuration keys and JSON, the shape feature flags are edited and stored in.</summary>
internal static class ConfigurationJson
{
    private static readonly JsonNodeOptions CaseInsensitive = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Reads the section at <paramref name="path"/> of one provider as JSON. Sections whose keys are <c>0..n-1</c>
    /// become arrays; values stay text, as configuration has no types.
    /// </summary>
    /// <returns>The section, or <c>null</c> when the provider has nothing at the path.</returns>
    public static JsonNode? Read(IConfigurationProvider provider, string path)
    {
        var children = provider.GetChildKeys([], path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (children.Length == 0)
            return provider.TryGet(path, out var value) && value is not null ? JsonValue.Create(value) : null;

        var indexes = children
            .Select(c => int.TryParse(c, NumberStyles.None, CultureInfo.InvariantCulture, out var i) ? i : -1)
            .ToArray();

        if (indexes.All(i => i >= 0) && indexes.Order().SequenceEqual(Enumerable.Range(0, indexes.Length)))
        {
            var array = new JsonArray();
            foreach (var index in indexes.Order())
                array.Add(Read(provider, ConfigurationPath.Combine(path, index.ToString(CultureInfo.InvariantCulture))));
            return array;
        }

        var obj = new JsonObject(CaseInsensitive);
        foreach (var child in children.Order(StringComparer.OrdinalIgnoreCase))
            obj[child] = Read(provider, ConfigurationPath.Combine(path, child));
        return obj;
    }

    /// <summary>Writes <paramref name="node"/> as configuration keys under <paramref name="path"/>.</summary>
    public static void Flatten(JsonNode? node, string path, IDictionary<string, string?> data)
    {
        switch (node)
        {
            case null:
                data[path] = null;
                break;
            case JsonObject obj:
                foreach (var (key, value) in obj)
                    Flatten(value, ConfigurationPath.Combine(path, key), data);
                break;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                    Flatten(array[i], ConfigurationPath.Combine(path, i.ToString(CultureInfo.InvariantCulture)), data);
                break;
            case JsonValue value:
                data[path] = value.GetValueKind() switch
                {
                    JsonValueKind.String => value.GetValue<string>(),
                    JsonValueKind.True   => "true",
                    JsonValueKind.False  => "false",
                    JsonValueKind.Null   => null,
                    _                    => value.ToJsonString(),
                };
                break;
        }
    }

    /// <summary>The property of <paramref name="obj"/> named <paramref name="name"/>, ignoring case.</summary>
    public static JsonNode? Get(this JsonObject? obj, string name) =>
        obj?.FirstOrDefault(p => string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase)).Value;

    /// <summary>The text of a value, or <c>null</c> when it is missing or not a value.</summary>
    public static string? AsText(this JsonNode? node) =>
        node is JsonValue value
            ? value.GetValueKind() switch
            {
                JsonValueKind.String => value.GetValue<string>(),
                JsonValueKind.Number => value.ToJsonString(),
                JsonValueKind.True   => "true",
                JsonValueKind.False  => "false",
                _                    => null,
            }
            : null;

    /// <summary>A number written as a number or as text, as configuration has it.</summary>
    public static double? AsNumber(this JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.Number
            ? value.GetValue<double>()
            : double.TryParse(node.AsText(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                ? number
                : null;

    /// <summary>A boolean written as a boolean or as text, as configuration has it.</summary>
    public static bool? AsBool(this JsonNode? node) =>
        bool.TryParse(node.AsText(), out var flag) ? flag : null;

    /// <summary>The items of an array, or of an object with index keys, e.g. when a source sets only some indexes.</summary>
    public static IEnumerable<JsonNode?> Items(this JsonNode? node) => node switch
    {
        JsonArray array => array,
        JsonObject obj  => obj.Select(p => p.Value),
        _               => [],
    };
}
