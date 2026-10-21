using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration.Memory;
using Xunit;

namespace Kododo.ConfigWay.FeatureManagement.Tests.Unit;

public class ConfigurationJsonTests
{
    [Fact]
    public void Read_TurnsIndexedSectionsIntoArraysAndKeepsValuesAsText()
    {
        var provider = Provider(new()
        {
            ["feature_management:feature_flags:0:id"]      = "Beta",
            ["feature_management:feature_flags:0:enabled"] = "true",
            ["feature_management:feature_flags:1:id"]      = "Dark",
            ["feature_management:feature_flags:1:conditions:client_filters:0:name"] = "Microsoft.Percentage",
            ["feature_management:feature_flags:1:conditions:client_filters:0:parameters:Value"] = "50",
        });

        var node = ConfigurationJson.Read(provider, "feature_management:feature_flags");

        var flags = Assert.IsType<JsonArray>(node);
        Assert.Equal(2, flags.Count);
        Assert.Equal("Beta", flags[0]!["id"]!.GetValue<string>());
        Assert.Equal("true", flags[0]!["enabled"]!.GetValue<string>());
        Assert.Equal("50", flags[1]!["conditions"]!["client_filters"]![0]!["parameters"]!["Value"]!.GetValue<string>());
    }

    [Fact]
    public void Read_IndexesWithGaps_StayAnObject()
    {
        var provider = Provider(new() { ["list:0"] = "a", ["list:2"] = "c" });

        var node = ConfigurationJson.Read(provider, "list");

        var obj = Assert.IsType<JsonObject>(node);
        Assert.Equal(["a", "c"], obj.Items().Select(i => i.AsText()));
    }

    [Fact]
    public void Read_MissingSection_IsNull()
    {
        Assert.Null(ConfigurationJson.Read(Provider(new()), "feature_management"));
    }

    [Fact]
    public void Read_ObjectsIgnoreCase()
    {
        var provider = Provider(new() { ["flag:Enabled"] = "true" });

        var node = (JsonObject)ConfigurationJson.Read(provider, "flag")!;

        Assert.Equal(true, node.Get("enabled").AsBool());
        Assert.True(node.ContainsKey("ENABLED"));
    }

    [Fact]
    public void Flatten_WritesEveryValueAsConfigurationText()
    {
        var json = JsonNode.Parse("""
            { "id": "Beta", "enabled": false, "n": 12.5, "none": null,
              "list": ["a", { "b": true }], "empty": {} }
            """);
        var data = new Dictionary<string, string?>();

        ConfigurationJson.Flatten(json, "root", data);

        Assert.Equal(new Dictionary<string, string?>
        {
            ["root:id"]       = "Beta",
            ["root:enabled"]  = "false",
            ["root:n"]        = "12.5",
            ["root:none"]     = null,
            ["root:list:0"]   = "a",
            ["root:list:1:b"] = "true",
        }, data);
    }

    [Theory]
    [InlineData("50", 50.0)]
    [InlineData("12.5", 12.5)]
    [InlineData("x", null)]
    public void AsNumber_ReadsNumbersWrittenAsText(string text, double? expected)
    {
        Assert.Equal(expected, JsonValue.Create(text).AsNumber());
    }

    private static MemoryConfigurationProvider Provider(Dictionary<string, string?> data)
    {
        var provider = new MemoryConfigurationProvider(new MemoryConfigurationSource { InitialData = data });
        provider.Load();
        return provider;
    }
}
