using System.Text.Json.Nodes;
using Xunit;

namespace Kododo.ConfigWay.FeatureManagement.Tests.Unit;

public class KnownFiltersTests
{
    [Theory]
    [InlineData("Microsoft.Percentage", KnownFilters.Percentage)]
    [InlineData("percentage",           KnownFilters.Percentage)]
    [InlineData("Microsoft.TimeWindow", KnownFilters.TimeWindow)]
    [InlineData("TimeWindow",           KnownFilters.TimeWindow)]
    [InlineData("Targeting",            KnownFilters.Targeting)]
    [InlineData("MyCompany.Browser",    null)]
    [InlineData("PercentageFilter",     null)]
    public void Find_AcceptsTheFullAliasOrItsLastSegment(string name, string? expected)
    {
        Assert.Equal(expected, KnownFilters.Find(name));
    }

    [Fact]
    public void Normalize_Percentage_WritesTheValueAsANumber()
    {
        var result = KnownFilters.Normalize("Percentage", Parse("""{ "value": "25" }"""));

        Assert.Equal("""{"Value":25}""", result!.ToJsonString());
    }

    [Fact]
    public void Normalize_TimeWindow_WritesTimesInRfc1123AndKeepsTheRecurrence()
    {
        var result = KnownFilters.Normalize(KnownFilters.TimeWindow, Parse("""
            { "Start": "2026-10-20T10:00:00+02:00", "End": "", "Recurrence": { "Pattern": { "Type": "Daily" } } }
            """));

        Assert.Equal("Tue, 20 Oct 2026 08:00:00 GMT", result!["Start"]!.GetValue<string>());
        Assert.False(result.ContainsKey("End"));
        Assert.Equal("Daily", result["Recurrence"]!["Pattern"]!["Type"]!.GetValue<string>());
    }

    [Fact]
    public void Normalize_Targeting_WritesTheWholeAudience()
    {
        var result = KnownFilters.Normalize(KnownFilters.Targeting, Parse("""
            { "Audience": { "Users": { "0": "jeff" }, "Groups": [ { "Name": "Ring0", "RolloutPercentage": "100" } ],
                            "DefaultRolloutPercentage": "20" } }
            """));

        Assert.Equal(
            """{"Audience":{"Users":["jeff"],"Groups":[{"Name":"Ring0","RolloutPercentage":100}],"DefaultRolloutPercentage":20,"Exclusion":{"Users":[],"Groups":[]}}}""",
            result!.ToJsonString());
    }

    [Fact]
    public void Normalize_OtherFilter_CopiesTheParameters()
    {
        var parameters = Parse("""{ "Allowed": ["Edge"] }""");

        var result = KnownFilters.Normalize("MyCompany.Browser", parameters);

        Assert.NotSame(parameters, result);
        Assert.Equal(parameters.ToJsonString(), result!.ToJsonString());
    }

    [Theory]
    [InlineData("""{ "Value": 0 }""", 0)]
    [InlineData("""{ "Value": "100" }""", 0)]
    [InlineData("""{ }""", 1)]
    [InlineData("""{ "Value": 101 }""", 1)]
    [InlineData("""{ "Value": -1 }""", 1)]
    [InlineData("""{ "Value": "half" }""", 1)]
    public void Validate_Percentage(string parameters, int errors)
    {
        Assert.Equal(errors, KnownFilters.Validate(KnownFilters.Percentage, Parse(parameters)).Count());
    }

    [Theory]
    [InlineData("""{ "Start": "Sun, 01 Jun 2025 13:59:59 GMT" }""", 0)]
    [InlineData("""{ "End": "2025-08-01T00:00:00Z" }""", 0)]
    [InlineData("""{ "Start": "2025-06-01T00:00:00Z", "End": "2025-08-01T00:00:00Z" }""", 0)]
    [InlineData("""{ }""", 1)]
    [InlineData("""{ "Start": "soon" }""", 1)]
    [InlineData("""{ "Start": "2025-08-01T00:00:00Z", "End": "2025-06-01T00:00:00Z" }""", 1)]
    public void Validate_TimeWindow(string parameters, int errors)
    {
        Assert.Equal(errors, KnownFilters.Validate(KnownFilters.TimeWindow, Parse(parameters)).Count());
    }

    [Theory]
    [InlineData("""{ "Audience": { "Users": ["jeff"], "DefaultRolloutPercentage": 0 } }""", 0)]
    [InlineData("""{ "Audience": { "Groups": [ { "Name": "Ring0", "RolloutPercentage": 50 } ] } }""", 0)]
    [InlineData("""{ }""", 1)]
    [InlineData("""{ "Audience": { "Users": [""] } }""", 1)]
    [InlineData("""{ "Audience": { "Groups": [ { "Name": "Ring0" } ] } }""", 1)]
    [InlineData("""{ "Audience": { "Groups": [ { "Name": "a", "RolloutPercentage": 1 }, { "Name": "A", "RolloutPercentage": 1 } ] } }""", 1)]
    [InlineData("""{ "Audience": { "DefaultRolloutPercentage": 120 } }""", 1)]
    [InlineData("""{ "Audience": { "Exclusion": { "Groups": [" "] } } }""", 1)]
    public void Validate_Targeting(string parameters, int errors)
    {
        Assert.Equal(errors, KnownFilters.Validate(KnownFilters.Targeting, Parse(parameters)).Count());
    }

    [Fact]
    public void Validate_OtherFilter_AcceptsAnyParameters()
    {
        Assert.Empty(KnownFilters.Validate("MyCompany.Browser", null));
    }

    [Theory]
    [InlineData("Microsoft.Percentage", true)]
    [InlineData("percentage", true)]
    [InlineData("Acme.Browser", true)]
    [InlineData("Browser", true)]
    [InlineData("Other.Browser", false)]
    [InlineData("Microsoft.Percentag", false)]
    [InlineData("AlwaysOn", true)]
    [InlineData("on", true)]
    public void RegisteredFilters_MatchAsFeatureManagementDoes(string name, bool expected)
    {
        Assert.Equal(expected, RegisteredFilters.Matches(name, ["Microsoft.Percentage", "Acme.Browser"]));
    }

    private static JsonObject Parse(string json) => JsonNode.Parse(json)!.AsObject();
}
