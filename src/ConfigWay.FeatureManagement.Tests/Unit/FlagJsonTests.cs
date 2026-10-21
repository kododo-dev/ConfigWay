using System.Text.Json.Nodes;
using Kododo.ConfigWay.Core.Model;
using Kododo.ConfigWay.FeatureManagement.Model;
using Xunit;

namespace Kododo.ConfigWay.FeatureManagement.Tests.Unit;

public class FlagJsonTests
{
    [Fact]
    public void ToFlag_ReadsTheMicrosoftSchema()
    {
        var flag = FlagJson.ToFlag("Beta", Parse("""
            { "id": "Beta", "description": "New checkout", "enabled": "true",
              "conditions": { "requirement_type": "all", "client_filters": [
                { "name": "Microsoft.Percentage", "parameters": { "Value": "10" } },
                { "name": "" } ] } }
            """));

        Assert.Equal("Beta", flag.Id);
        Assert.True(flag.Enabled);
        Assert.Equal("New checkout", flag.Description);
        Assert.Equal(RequirementType.All, flag.RequirementType);
        var filter = Assert.Single(flag.Filters);
        Assert.Equal("""{"Value":10}""", filter.Parameters!.ToJsonString());
    }

    [Fact]
    public void ToFlag_WithoutEnabled_IsOff()
    {
        Assert.False(FlagJson.ToFlag("Beta", Parse("""{ "id": "Beta" }""")).Enabled);
    }

    [Fact]
    public void Apply_KeepsWhatTheEditorDoesNotEdit()
    {
        var existing = Parse("""
            { "id": "Beta", "enabled": true, "description": "old",
              "conditions": { "client_filters": [ { "name": "Microsoft.Percentage", "parameters": { "Value": 5 } } ] },
              "variants": [ { "name": "On" } ], "allocation": { "default_when_enabled": "On" },
              "telemetry": { "enabled": true } }
            """);

        var json = FlagJson.Apply(new FeatureFlag("Beta", false, RequirementType.Any, []), existing);

        Assert.False(json["enabled"]!.GetValue<bool>());
        Assert.False(json.ContainsKey("description"));
        Assert.False(json.ContainsKey("conditions"));
        Assert.Equal("On", json["variants"]![0]!["name"]!.GetValue<string>());
        Assert.Equal("On", json["allocation"]!["default_when_enabled"]!.GetValue<string>());
        Assert.True(json["telemetry"]!["enabled"]!.GetValue<bool>());
        Assert.Equal("""{"id":"Beta","enabled":true}""", FlagJson.Apply(
            new FeatureFlag("Beta", true, RequirementType.Any, []), null).ToJsonString());
    }

    [Fact]
    public void Apply_WritesTheConditionsInTheMicrosoftSchema()
    {
        var flag = new FeatureFlag("Beta", true, RequirementType.All,
            [new FeatureFilter("Percentage", Parse("""{ "Value": "50" }""")), new FeatureFilter("MyCompany.Browser")],
            "note");

        var json = FlagJson.Apply(flag, null);

        Assert.Equal(
            """{"id":"Beta","description":"note","enabled":true,"conditions":{"requirement_type":"All","client_filters":[{"name":"Percentage","parameters":{"Value":50}},{"name":"MyCompany.Browser"}]}}""",
            json.ToJsonString());
    }

    [Fact]
    public void Apply_ReplacesEditedPropertiesWhateverTheirCase()
    {
        var json = FlagJson.Apply(
            new FeatureFlag("Beta", true, RequirementType.Any, []),
            Parse("""{ "ID": "Beta", "Enabled": "false", "Conditions": { } }"""));

        Assert.Equal("""{"id":"Beta","enabled":true}""", json.ToJsonString());
    }

    [Fact]
    public void FromDotnetSchema_ReadsABoolean()
    {
        Assert.Equal("""{"id":"Old","enabled":true}""", FlagJson.FromDotnetSchema("Old", JsonValue.Create("True")).ToJsonString());
    }

    [Fact]
    public void FromDotnetSchema_ReadsTheFilters()
    {
        var json = FlagJson.FromDotnetSchema("Old", Parse("""
            { "RequirementType": "All", "EnabledFor": [ { "Name": "Percentage", "Parameters": { "Value": "30" } } ] }
            """));

        var flag = FlagJson.ToFlag("Old", json);
        Assert.True(flag.Enabled);
        Assert.Equal(RequirementType.All, flag.RequirementType);
        Assert.Equal("""{"Value":30}""", Assert.Single(flag.Filters).Parameters!.ToJsonString());
    }

    [Theory]
    [InlineData("""{ "EnabledFor": [ { "Name": "AlwaysOn" } ] }""", true, 0)]
    [InlineData("""{ "EnabledFor": [] }""", false, 0)]
    [InlineData("""{ }""", false, 0)]
    [InlineData("""{ "Status": "Disabled", "EnabledFor": [ { "Name": "AlwaysOn" } ] }""", false, 0)]
    public void FromDotnetSchema_ReadsTheState(string definition, bool enabled, int filters)
    {
        var flag = FlagJson.ToFlag("Old", FlagJson.FromDotnetSchema("Old", Parse(definition)));

        Assert.Equal(enabled, flag.Enabled);
        Assert.Equal(filters, flag.Filters.Length);
    }

    [Fact]
    public void StoredFlags_TakeTheIdFromTheKeyAndReportUnreadableValues()
    {
        var flags = StoredFlags.Read([
            new Setting("feature_management:flags/zeta", """{ "ID": "other", "enabled": true }"""),
            new Setting("feature_management:flags/Alpha", "{ not json"),
            new Setting("feature_management:flags/", "{}"),
            new Setting("Smtp:Host", "localhost"),
        ]);

        Assert.Equal(["Alpha", "zeta"], flags.Select(f => f.Id));
        Assert.Null(flags[0].Json);
        Assert.NotNull(flags[0].Error);
        Assert.Equal("""{"enabled":true,"id":"zeta"}""", flags[1].Json!.ToJsonString());
    }

    [Fact]
    public void Provider_ExposesTheStoredFlagsWhereFeatureManagementReadsThem()
    {
        var provider = new FeatureFlagsProvider();
        provider.Apply([
            new Setting("feature_management:flags/Beta", """{ "id": "Beta", "enabled": true }"""),
            new Setting("feature_management:flags/Broken", "nope"),
            new Setting("feature_management:flags/Alpha", """{ "enabled": false }"""),
        ]);

        Assert.True(provider.TryGet("feature_management:feature_flags:100000:id", out var first));
        Assert.Equal("Alpha", first);
        Assert.True(provider.TryGet("feature_management:feature_flags:100001:enabled", out var enabled));
        Assert.Equal("true", enabled);
        Assert.False(provider.TryGet("feature_management:feature_flags:100002:id", out _));
    }

    private static JsonObject Parse(string json) => JsonNode.Parse(json)!.AsObject();
}
