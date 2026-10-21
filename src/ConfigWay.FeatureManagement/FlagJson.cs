using System.Text.Json.Nodes;
using Kododo.ConfigWay.FeatureManagement.Model;

namespace Kododo.ConfigWay.FeatureManagement;

/// <summary>
/// Converts between the editor's <see cref="FeatureFlag"/> and a flag in the Microsoft feature management schema
/// (<c>feature_management:feature_flags</c>), and from the older .NET schema (<c>FeatureManagement</c>).
/// </summary>
internal static class FlagJson
{
    /// <summary>The flag as the editor shows it.</summary>
    public static FeatureFlag ToFlag(string id, JsonObject json)
    {
        var conditions = json.Get("conditions") as JsonObject;
        var filters = conditions.Get("client_filters").Items()
            .OfType<JsonObject>()
            .Select(f => (Name: f.Get("name").AsText(), Parameters: f.Get("parameters") as JsonObject))
            .Where(f => !string.IsNullOrWhiteSpace(f.Name))
            .Select(f => new FeatureFilter(f.Name!, KnownFilters.Normalize(f.Name!, f.Parameters)))
            .ToArray();

        return new FeatureFlag(
            id,
            json.Get("enabled").AsBool() ?? false,
            string.Equals(conditions.Get("requirement_type").AsText(), "All", StringComparison.OrdinalIgnoreCase)
                ? RequirementType.All
                : RequirementType.Any,
            filters,
            json.Get("description").AsText());
    }

    /// <summary>Whether the flag has variants, which the editor keeps but does not edit yet.</summary>
    public static bool HasVariants(JsonObject json) =>
        json.Get("variants") is not null || json.Get("allocation") is not null;

    /// <summary>
    /// The flag with the editor's values applied. Everything the editor does not know about, e.g. variants or
    /// telemetry, is kept from <paramref name="existing"/>.
    /// </summary>
    public static JsonObject Apply(FeatureFlag flag, JsonObject? existing)
    {
        var json = new JsonObject { ["id"] = flag.Id };
        if (!string.IsNullOrWhiteSpace(flag.Description))
            json["description"] = flag.Description;
        json["enabled"] = flag.Enabled;

        if (flag.Filters.Length > 0)
        {
            json["conditions"] = new JsonObject
            {
                ["requirement_type"] = flag.RequirementType.ToString(),
                ["client_filters"] = new JsonArray(flag.Filters
                    .Select(f =>
                    {
                        var filter = new JsonObject { ["name"] = f.Name };
                        if (KnownFilters.Normalize(f.Name, f.Parameters) is { } parameters)
                            filter["parameters"] = parameters;
                        return (JsonNode)filter;
                    })
                    .ToArray()),
            };
        }

        if (existing is not null)
        {
            foreach (var (key, value) in existing)
            {
                if (!IsEdited(key))
                    json[key] = value?.DeepClone();
            }
        }

        return json;
    }

    /// <summary>
    /// A flag of the .NET schema in the Microsoft schema: <c>"Beta": true</c> or
    /// <c>"Beta": { "EnabledFor": [...], "RequirementType": "All" }</c>.
    /// </summary>
    public static JsonObject FromDotnetSchema(string id, JsonNode? node)
    {
        if (node is not JsonObject obj)
            return new JsonObject { ["id"] = id, ["enabled"] = node.AsBool() ?? false };

        var enabledFor = obj.Get("EnabledFor").Items()
            .OfType<JsonObject>()
            .Select(f => (Name: f.Get("Name").AsText(), Parameters: f.Get("Parameters") as JsonObject))
            .Where(f => !string.IsNullOrWhiteSpace(f.Name))
            .ToArray();
        var alwaysOn = enabledFor.Any(f => f.Name is "AlwaysOn" or "On");
        var disabled = string.Equals(obj.Get("Status").AsText(), "Disabled", StringComparison.OrdinalIgnoreCase);

        var json = new JsonObject { ["id"] = id, ["enabled"] = !disabled && enabledFor.Length > 0 };
        if (!alwaysOn && enabledFor.Length > 0)
        {
            json["conditions"] = new JsonObject
            {
                ["requirement_type"] = obj.Get("RequirementType").AsText() ?? "Any",
                ["client_filters"] = new JsonArray(enabledFor
                    .Select(f => (JsonNode)new JsonObject
                    {
                        ["name"] = f.Name,
                        ["parameters"] = f.Parameters?.DeepClone(),
                    })
                    .ToArray()),
            };
        }

        return json;
    }

    /// <summary>Whether a .NET schema flag has variants, which are not carried over to an override.</summary>
    public static bool HasDotnetVariants(JsonNode? node) =>
        node is JsonObject obj && (obj.Get("Variants") is not null || obj.Get("Allocation") is not null);

    private static bool IsEdited(string key) =>
        key.Equals("id", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("enabled", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("description", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("conditions", StringComparison.OrdinalIgnoreCase);
}
