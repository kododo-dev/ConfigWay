using System.Globalization;
using System.Text.Json.Nodes;

namespace Kododo.ConfigWay.FeatureManagement;

/// <summary>
/// The built-in filters of Microsoft.FeatureManagement that the editor has forms for: their parameters are written in
/// one shape and checked before a save. Other filters are kept as they are.
/// </summary>
internal static class KnownFilters
{
    public const string Percentage = "Microsoft.Percentage";
    public const string TimeWindow = "Microsoft.TimeWindow";
    public const string Targeting  = "Microsoft.Targeting";

    private static readonly string[] All = [Percentage, TimeWindow, Targeting];

    /// <summary>
    /// The built-in filter <paramref name="name"/> refers to, by its full alias or its last segment as the library
    /// accepts them (<c>Microsoft.Percentage</c> or <c>Percentage</c>); <c>null</c> for any other filter.
    /// </summary>
    public static string? Find(string? name) =>
        name is null
            ? null
            : All.FirstOrDefault(f =>
                string.Equals(f, name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(f[(f.LastIndexOf('.') + 1)..], name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The parameters of a built-in filter in one shape: numbers as numbers, times in RFC 1123, empty parts left out.
    /// Values that cannot be read are kept as they are, so that nothing is lost; other filters are copied.
    /// </summary>
    public static JsonObject? Normalize(string name, JsonObject? parameters) => Find(name) switch
    {
        Percentage => NormalizePercentage(parameters),
        TimeWindow => NormalizeTimeWindow(parameters),
        Targeting  => NormalizeTargeting(parameters),
        _          => parameters?.DeepClone().AsObject(),
    };

    /// <summary>What is wrong with the parameters of a built-in filter; nothing for other filters.</summary>
    public static IEnumerable<string> Validate(string name, JsonObject? parameters) => Find(name) switch
    {
        Percentage => ValidatePercentage(parameters),
        TimeWindow => ValidateTimeWindow(parameters),
        Targeting  => ValidateTargeting(parameters),
        _          => [],
    };

    private static JsonObject NormalizePercentage(JsonObject? parameters)
    {
        var result = new JsonObject();
        SetNumber(result, "Value", parameters.Get("Value"));
        return result;
    }

    private static JsonObject NormalizeTimeWindow(JsonObject? parameters)
    {
        var result = new JsonObject();
        foreach (var name in new[] { "Start", "End" })
        {
            var text = parameters.Get(name).AsText();
            if (string.IsNullOrWhiteSpace(text))
                continue;
            result[name] = TryParseTime(text, out var time) ? time.ToString("r", CultureInfo.InvariantCulture) : text;
        }

        if (parameters.Get("Recurrence") is { } recurrence)
            result["Recurrence"] = recurrence.DeepClone();
        return result;
    }

    private static JsonObject NormalizeTargeting(JsonObject? parameters)
    {
        var audience = parameters.Get("Audience") as JsonObject;
        var result = new JsonObject
        {
            ["Users"]  = Texts(audience.Get("Users")),
            ["Groups"] = new JsonArray(audience.Get("Groups").Items().OfType<JsonObject>().Select(group =>
            {
                var item = new JsonObject { ["Name"] = group.Get("Name").AsText() ?? string.Empty };
                SetNumber(item, "RolloutPercentage", group.Get("RolloutPercentage"));
                return (JsonNode)item;
            }).ToArray()),
        };
        SetNumber(result, "DefaultRolloutPercentage", audience.Get("DefaultRolloutPercentage"));

        var exclusion = audience.Get("Exclusion") as JsonObject;
        result["Exclusion"] = new JsonObject
        {
            ["Users"]  = Texts(exclusion.Get("Users")),
            ["Groups"] = Texts(exclusion.Get("Groups")),
        };
        return new JsonObject { ["Audience"] = result };
    }

    private static IEnumerable<string> ValidatePercentage(JsonObject? parameters)
    {
        if (parameters.Get("Value").AsNumber() is not { } value)
            yield return "The percentage is required.";
        else if (value is < 0 or > 100)
            yield return "The percentage must be between 0 and 100.";
    }

    private static IEnumerable<string> ValidateTimeWindow(JsonObject? parameters)
    {
        DateTimeOffset? start = null, end = null;
        foreach (var name in new[] { "Start", "End" })
        {
            var text = parameters.Get(name).AsText();
            if (string.IsNullOrWhiteSpace(text))
                continue;
            if (!TryParseTime(text, out var time))
                yield return $"The {name.ToLowerInvariant()} time is not a valid date and time.";
            else if (name == "Start")
                start = time;
            else
                end = time;
        }

        if (string.IsNullOrWhiteSpace(parameters.Get("Start").AsText()) &&
            string.IsNullOrWhiteSpace(parameters.Get("End").AsText()))
            yield return "The time window needs a start, an end or both.";
        else if (start >= end)
            yield return "The start must be before the end.";
    }

    private static IEnumerable<string> ValidateTargeting(JsonObject? parameters)
    {
        if (parameters.Get("Audience") is not JsonObject audience)
        {
            yield return "The audience is required.";
            yield break;
        }

        if (audience.Get("Users").Items().Any(u => string.IsNullOrWhiteSpace(u.AsText())))
            yield return "User names cannot be empty.";

        var groups = audience.Get("Groups").Items().ToArray();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            var name = (group as JsonObject).Get("Name").AsText();
            if (string.IsNullOrWhiteSpace(name))
                yield return "Group names cannot be empty.";
            else if (!names.Add(name))
                yield return $"The group '{name}' is listed more than once.";

            if (!IsPercentage((group as JsonObject).Get("RolloutPercentage"), required: true))
                yield return $"The rollout percentage of the group '{name}' must be between 0 and 100.";
        }

        if (!IsPercentage(audience.Get("DefaultRolloutPercentage"), required: false))
            yield return "The default rollout percentage must be between 0 and 100.";

        var exclusion = audience.Get("Exclusion") as JsonObject;
        if (exclusion.Get("Users").Items().Concat(exclusion.Get("Groups").Items())
            .Any(e => string.IsNullOrWhiteSpace(e.AsText())))
            yield return "Excluded users and groups cannot be empty.";
    }

    private static bool IsPercentage(JsonNode? node, bool required) =>
        node is null ? !required : node.AsNumber() is >= 0 and <= 100;

    private static void SetNumber(JsonObject target, string name, JsonNode? value)
    {
        if (value is null)
            return;
        target[name] = value.AsNumber() is { } number ? JsonValue.Create(number) : value.DeepClone();
    }

    private static JsonArray Texts(JsonNode? node) =>
        new(node.Items().Select(i => (JsonNode?)JsonValue.Create(i.AsText() ?? string.Empty)).ToArray());

    private static bool TryParseTime(string text, out DateTimeOffset time) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out time);
}
