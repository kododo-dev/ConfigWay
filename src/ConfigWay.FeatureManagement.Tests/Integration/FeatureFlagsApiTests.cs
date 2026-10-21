using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Kododo.ConfigWay.Core.Configuration;
using Kododo.ConfigWay.Core.Model;
using Kododo.ConfigWay.UI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.FeatureFilters;
using Xunit;

namespace Kododo.ConfigWay.FeatureManagement.Tests.Integration;

/// <summary>The flags API, checked against the application's real feature manager.</summary>
public class FeatureFlagsApiTests : IAsyncLifetime
{
    private WebApplication _app    = null!;
    private HttpClient     _client = null!;

    /// <summary>The application's own flags, as appsettings would define them.</summary>
    private static readonly Dictionary<string, string?> Configured = new()
    {
        ["feature_management:feature_flags:0:id"]      = "Beta",
        ["feature_management:feature_flags:0:enabled"] = "true",

        ["feature_management:feature_flags:1:id"]      = "Rollout",
        ["feature_management:feature_flags:1:enabled"] = "true",
        ["feature_management:feature_flags:1:conditions:client_filters:0:name"]             = "Microsoft.Percentage",
        ["feature_management:feature_flags:1:conditions:client_filters:0:parameters:Value"] = "0",

        ["feature_management:feature_flags:2:id"]      = "Layout",
        ["feature_management:feature_flags:2:enabled"] = "true",
        ["feature_management:feature_flags:2:variants:0:name"]                = "Big",
        ["feature_management:feature_flags:2:variants:0:configuration_value"] = "500px",
        ["feature_management:feature_flags:2:variants:1:name"]                = "Small",
        ["feature_management:feature_flags:2:allocation:default_when_enabled"]  = "Big",
        ["feature_management:feature_flags:2:allocation:default_when_disabled"] = "Small",

        ["feature_management:feature_flags:3:id"]      = "Audience",
        ["feature_management:feature_flags:3:enabled"] = "true",
        ["feature_management:feature_flags:3:conditions:client_filters:0:name"] = "Microsoft.Targeting",
        ["feature_management:feature_flags:3:conditions:client_filters:0:parameters:Audience:Users:0"] = "jeff",
        ["feature_management:feature_flags:3:conditions:client_filters:0:parameters:Audience:Groups:0:Name"] = "Ring0",
        ["feature_management:feature_flags:3:conditions:client_filters:0:parameters:Audience:Groups:0:RolloutPercentage"] = "100",
        ["feature_management:feature_flags:3:conditions:client_filters:0:parameters:Audience:Exclusion:Users:0"] = "ross",

        ["FeatureManagement:Old"] = "true",
    };

    public async Task InitializeAsync()
    {
        (_app, _client) = await StartAsync(featureManagement: true);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    private static async Task<(WebApplication, HttpClient)> StartAsync(bool featureManagement)
    {
        return await StartAsync(featureManagement ? fm => fm.AddFeatureFilter<BrowserFilter>() : null);
    }

    private static async Task<(WebApplication, HttpClient)> StartAsync(Action<IFeatureManagementBuilder>? featureManagement)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(Configured);
        builder.AddConfigWay(x =>
        {
            x.AddUiEditor();
            x.AddFeatureFlags();
        });
        if (featureManagement is not null)
            featureManagement(builder.Services.AddFeatureManagement());

        var app = builder.Build();
        app.UseConfigWay().AllowAnonymous();
        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    private IVariantFeatureManager FeatureManager => _app.Services.GetRequiredService<IVariantFeatureManager>();

    // ── Listing ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCapabilities_ReportsTheFlagsExtension()
    {
        var capabilities = await PostJson("GetCapabilities", new { });

        Assert.Contains("flags", capabilities["extensions"]!.AsArray().Select(e => e!.GetValue<string>()));
    }

    [Fact]
    public async Task GetFeatureFlags_ListsTheConfiguredFlagsOfBothSchemas()
    {
        var result = await PostJson("GetFeatureFlags", new { });

        Assert.True(result["isFeatureManagementRegistered"]!.GetValue<bool>());
        Assert.Equal(["Audience", "Beta", "Layout", "Old", "Rollout"], Ids(result));

        var beta = Item(result, "Beta");
        Assert.True(beta["flag"]!["enabled"]!.GetValue<bool>());
        Assert.False(beta["hasOverride"]!.GetValue<bool>());
        Assert.NotNull(beta["configured"]);

        Assert.True(Item(result, "Old")["isDotnetSchema"]!.GetValue<bool>());
        Assert.True(Item(result, "Layout")["hasVariants"]!.GetValue<bool>());

        var rollout = Item(result, "Rollout")["flag"]!["filters"]![0]!;
        Assert.Equal("Microsoft.Percentage", rollout["name"]!.GetValue<string>());
        Assert.Equal(0, rollout["parameters"]!["Value"]!.GetValue<double>());
    }

    [Fact]
    public async Task GetFeatureFlags_ListsTheRegisteredFilters()
    {
        var result = await PostJson("GetFeatureFlags", new { });

        var filters = result["filters"]!.AsArray().Select(f => f!.GetValue<string>()).ToArray();
        Assert.Contains("Microsoft.Percentage", filters);
        Assert.Contains("Microsoft.TimeWindow", filters);
        Assert.Contains("Microsoft.Targeting", filters);
    }

    [Fact]
    public async Task GetFeatureFlags_WithoutFeatureManagement_SaysSo()
    {
        var (app, client) = await StartAsync(featureManagement: false);
        await using var _ = app;

        var result = await (await client.PostAsJsonAsync("/config/api/GetFeatureFlags", new { }))
            .Content.ReadFromJsonAsync<JsonObject>();

        Assert.False(result!["isFeatureManagementRegistered"]!.GetValue<bool>());
        Assert.Empty(result["filters"]!.AsArray());
        Assert.Equal(5, result["flags"]!.AsArray().Count);
    }

    // ── Overrides ────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveFeatureFlag_OverridesTheConfiguredFlag()
    {
        Assert.True(await FeatureManager.IsEnabledAsync("Beta"));

        await Save(Flag("Beta", enabled: false));

        Assert.False(await FeatureManager.IsEnabledAsync("Beta"));
        var item = Item(await PostJson("GetFeatureFlags", new { }), "Beta");
        Assert.True(item["hasOverride"]!.GetValue<bool>());
        Assert.False(item["flag"]!["enabled"]!.GetValue<bool>());
        Assert.True(item["configured"]!["enabled"]!.GetValue<bool>());
    }

    [Fact]
    public async Task SaveFeatureFlag_OverridesAFlagOfTheDotnetSchema()
    {
        Assert.True(await FeatureManager.IsEnabledAsync("Old"));

        await Save(Flag("Old", enabled: false));

        Assert.False(await FeatureManager.IsEnabledAsync("Old"));
    }

    [Fact]
    public async Task SaveFeatureFlag_WithAPercentage_ChangesTheRollout()
    {
        Assert.False(await FeatureManager.IsEnabledAsync("Rollout"));

        await Save(Flag("Rollout", enabled: true, filters: [Filter("Microsoft.Percentage", """{ "Value": 100 }""")]));

        Assert.True(await FeatureManager.IsEnabledAsync("Rollout"));
    }

    [Fact]
    public async Task SaveFeatureFlag_WithATimeWindow_FollowsTheWindow()
    {
        await Save(Flag("Window", enabled: true, filters: [Filter("Microsoft.TimeWindow",
            """{ "Start": "2000-01-01T00:00:00Z", "End": "2001-01-01T00:00:00Z" }""")]), isNew: true);
        Assert.False(await FeatureManager.IsEnabledAsync("Window"));

        await Save(Flag("Window", enabled: true, filters: [Filter("TimeWindow",
            """{ "Start": "2000-01-01T00:00:00Z" }""")]));
        Assert.True(await FeatureManager.IsEnabledAsync("Window"));
    }

    [Fact]
    public async Task SaveFeatureFlag_IgnoresTheCaseOfTheId()
    {
        await Save(Flag("beta", enabled: false));

        var result = await PostJson("GetFeatureFlags", new { });
        Assert.Single(Ids(result), id => id.Equals("beta", StringComparison.OrdinalIgnoreCase));
        Assert.True(Item(result, "Beta")["hasOverride"]!.GetValue<bool>());
        Assert.False(await FeatureManager.IsEnabledAsync("BETA"));
    }

    [Fact]
    public async Task SaveFeatureFlag_KeepsTheVariantsOfTheConfiguredFlag()
    {
        Assert.Equal("Big", (await FeatureManager.GetVariantAsync("Layout"))!.Name);

        await Save(Flag("Layout", enabled: false));

        Assert.False(await FeatureManager.IsEnabledAsync("Layout"));
        Assert.Equal("Small", (await FeatureManager.GetVariantAsync("Layout"))!.Name);
        var stored = await StoredJson("Layout");
        Assert.Equal(2, stored["variants"]!.AsArray().Count);
    }

    [Fact]
    public async Task SaveFeatureFlag_NewFlag_IsAddedAndResetRemovesIt()
    {
        Assert.False(await FeatureManager.IsEnabledAsync("Fresh"));

        await Save(Flag("Fresh", enabled: true), isNew: true);
        Assert.True(await FeatureManager.IsEnabledAsync("Fresh"));
        var item = Item(await PostJson("GetFeatureFlags", new { }), "Fresh");
        Assert.Null(item["configured"]);

        await Post("ResetFeatureFlag", new { Id = "Fresh" }, HttpStatusCode.NoContent);
        Assert.False(await FeatureManager.IsEnabledAsync("Fresh"));
        Assert.DoesNotContain("Fresh", Ids(await PostJson("GetFeatureFlags", new { })));
    }

    [Fact]
    public async Task ResetFeatureFlag_BringsBackTheConfiguredFlag()
    {
        await Save(Flag("Beta", enabled: false));

        await Post("ResetFeatureFlag", new { Id = "BETA" }, HttpStatusCode.NoContent);

        Assert.True(await FeatureManager.IsEnabledAsync("Beta"));
        Assert.False(Item(await PostJson("GetFeatureFlags", new { }), "Beta")["hasOverride"]!.GetValue<bool>());
    }

    [Fact]
    public async Task ResetFeatureFlag_WithoutOverride_IsNotFound()
    {
        await Post("ResetFeatureFlag", new { Id = "Beta" }, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UnreadableOverride_IsReportedAndCanBeReset()
    {
        var store = _app.Services.GetRequiredService<Configuration>().Store;
        await store.SetAsync([new Setting("feature_management:flags/Beta", "{ broken")]);
        await _app.Services.GetRequiredService<IConfigurationEditor>().ReloadAllAsync(CancellationToken.None);

        var item = Item(await PostJson("GetFeatureFlags", new { }), "Beta");
        Assert.NotNull(item["error"]);
        Assert.True(await FeatureManager.IsEnabledAsync("Beta"));

        await Post("ResetFeatureFlag", new { Id = "Beta" }, HttpStatusCode.NoContent);
        Assert.Null(Item(await PostJson("GetFeatureFlags", new { }), "Beta")["error"]);
    }

    // ── Validation ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveFeatureFlag_NewFlagWithATakenId_IsInvalid()
    {
        var errors = await SaveInvalid(Flag("beta", enabled: true), isNew: true);

        Assert.Contains("flag.id", errors.Select(e => e.Key));
    }

    [Fact]
    public async Task SaveFeatureFlag_UnknownFlag_IsNotFound()
    {
        await Post("SaveFeatureFlag", new { Flag = Flag("Missing", enabled: true) }, HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("a:b")]
    [InlineData(" Beta")]
    [InlineData("")]
    public async Task SaveFeatureFlag_InvalidId_IsInvalid(string id)
    {
        var errors = await SaveInvalid(Flag(id, enabled: true), isNew: true);

        Assert.Contains("flag.id", errors.Select(e => e.Key));
    }

    [Fact]
    public async Task SaveFeatureFlag_InvalidFilterParameters_AreReportedPerFilter()
    {
        var errors = await SaveInvalid(Flag("Rollout", enabled: true, filters:
        [
            Filter("Microsoft.Percentage", """{ "Value": 50 }"""),
            Filter("Microsoft.Percentage", """{ "Value": 150 }"""),
        ]));

        var error = Assert.Single(errors);
        Assert.Equal("flag.filters[1].parameters", error.Key);
        Assert.False(await FeatureManager.IsEnabledAsync("Rollout"));
    }

    [Fact]
    public async Task SaveFeatureFlag_UnregisteredFilter_IsInvalidAndTheAppKeepsWorking()
    {
        var errors = await SaveInvalid(Flag("Rollout", enabled: true, filters:
        [
            Filter("Microsoft.Percentage", """{ "Value": 100 }"""),
            Filter("Microsoft.Percentag", "{}"),
        ]));

        var error = Assert.Single(errors);
        Assert.Equal("flag.filters[1].name", error.Key);
        Assert.False(await FeatureManager.IsEnabledAsync("Rollout"));
    }

    [Theory]
    [InlineData("Acme.Browser")]
    [InlineData("browser")]
    [InlineData("AlwaysOn")]
    public async Task SaveFeatureFlag_RegisteredFilter_IsSaved(string name)
    {
        await Save(Flag("Beta", enabled: true, filters: [Filter(name, """{ "Allowed": ["Edge"] }""")]));

        var stored = await StoredJson("Beta");
        Assert.Equal(name, stored["conditions"]!["client_filters"]![0]!["name"]!.GetValue<string>());
        Assert.Equal(name == "AlwaysOn", await FeatureManager.IsEnabledAsync("Beta"));
    }

    [Fact]
    public async Task SaveFeatureFlag_WithoutFeatureManagement_AcceptsAnyFilter()
    {
        var (app, client) = await StartAsync(featureManagement: false);
        await using var _ = app;

        var response = await client.PostAsJsonAsync("/config/api/SaveFeatureFlag", new
        {
            Flag = Flag("Beta", enabled: true, filters: [Filter("Acme.NotYetDeployed", "{}")]),
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task GetFeatureFlags_ListsACustomFilterByItsAlias()
    {
        var result = await PostJson("GetFeatureFlags", new { });

        Assert.Contains("Acme.Browser", result["filters"]!.AsArray().Select(f => f!.GetValue<string>()));
    }

    // ── Access ───────────────────────────────────────────────────────────────

    public static TheoryData<string, string> Requests => new()
    {
        { "GetFeatureFlags", "{}" },
        { "SaveFeatureFlag", """{ "flag": { "id": "Beta", "enabled": false, "requirementType": "Any", "filters": [] } }""" },
        { "ResetFeatureFlag", """{ "id": "Beta" }""" },
        { "EvaluateFeatureFlag", """{ "id": "Beta" }""" },
    };

    [Theory]
    [MemberData(nameof(Requests))]
    public async Task FlagsApi_OutsideDevelopmentWithoutADecision_IsForbidden(string request, string body)
    {
        await using var app = await StartProtectedAsync(group => { });
        var response = await app.GetTestClient().PostAsync(
            $"/config/api/{request}", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(await app.Services.GetRequiredService<IVariantFeatureManager>().IsEnabledAsync("Beta"));
    }

    [Theory]
    [MemberData(nameof(Requests))]
    public async Task FlagsApi_FollowsTheEditorsAuthorization(string request, string body)
    {
        await using var app = await StartProtectedAsync(group => group.RequireAuthorization());
        var response = await app.GetTestClient().PostAsync(
            $"/config/api/{request}", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(await app.Services.GetRequiredService<IVariantFeatureManager>().IsEnabledAsync("Beta"));
    }

    private static async Task<WebApplication> StartProtectedAsync(Action<Microsoft.AspNetCore.Routing.RouteGroupBuilder> decide)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(Configured);
        builder.AddConfigWay(x =>
        {
            x.AddUiEditor();
            x.AddFeatureFlags();
        });
        builder.Services.AddFeatureManagement();
        builder.Services.AddAuthentication("Test").AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, NoUserHandler>("Test", null);
        builder.Services.AddAuthorization();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        decide(app.UseConfigWay());
        await app.StartAsync();
        return app;
    }

    // ── Evaluation ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("jeff", new string[0], true)]
    [InlineData("anna", new[] { "Ring0" }, true)]
    [InlineData("anna", new string[0], false)]
    [InlineData("ross", new[] { "Ring0" }, false)]
    [InlineData(null, new string[0], false)]
    public async Task EvaluateFeatureFlag_ChecksTheGivenUser(string? user, string[] groups, bool expected)
    {
        var result = await PostJson("EvaluateFeatureFlag", new { Id = "Audience", UserId = user, Groups = groups });

        Assert.Equal(expected, result["enabled"]!.GetValue<bool>());
    }

    [Fact]
    public async Task EvaluateFeatureFlag_ReturnsTheVariant()
    {
        var result = await PostJson("EvaluateFeatureFlag", new { Id = "Layout" });

        Assert.True(result["enabled"]!.GetValue<bool>());
        Assert.Equal("Big", result["variant"]!.GetValue<string>());
    }

    [Fact]
    public async Task EvaluateFeatureFlag_UnknownFlag_IsNotFound()
    {
        await Post("EvaluateFeatureFlag", new { Id = "Missing" }, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EvaluateFeatureFlag_WithoutFeatureManagement_IsAConflict()
    {
        var (app, client) = await StartAsync(featureManagement: false);
        await using var _ = app;

        var response = await client.PostAsJsonAsync("/config/api/EvaluateFeatureFlag", new { Id = "Beta" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Evaluation_ReflectsAnOverrideOfTheTargeting()
    {
        var context = new TargetingContext { UserId = "anna", Groups = [] };
        Assert.False(await FeatureManager.IsEnabledAsync("Audience", context));

        await Save(Flag("Audience", enabled: true, filters:
            [Filter("Microsoft.Targeting", """{ "Audience": { "Users": ["anna"] } }""")]));

        Assert.True(await FeatureManager.IsEnabledAsync("Audience", context));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static object Flag(string id, bool enabled, object[]? filters = null, string requirementType = "Any") =>
        new { Id = id, Enabled = enabled, RequirementType = requirementType, Filters = filters ?? [] };

    private static object Filter(string name, string parameters) =>
        new { Name = name, Parameters = JsonNode.Parse(parameters) };

    private Task Save(object flag, bool isNew = false) =>
        Post("SaveFeatureFlag", new { Flag = flag, IsNew = isNew }, HttpStatusCode.NoContent);

    private async Task<Dictionary<string, string[]>> SaveInvalid(object flag, bool isNew = false)
    {
        var response = await _client.PostAsJsonAsync("/config/api/SaveFeatureFlag", new { Flag = flag, IsNew = isNew });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonObject>();
        return problem!["errors"]!.AsObject()
            .ToDictionary(e => e.Key, e => e.Value!.AsArray().Select(m => m!.GetValue<string>()).ToArray());
    }

    private async Task Post(string request, object body, HttpStatusCode expected)
    {
        var response = await _client.PostAsJsonAsync($"/config/api/{request}", body);
        Assert.True(expected == response.StatusCode,
            $"{request}: expected {expected}, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    private async Task<JsonObject> PostJson(string request, object body)
    {
        var response = await _client.PostAsJsonAsync($"/config/api/{request}", body);
        Assert.True(response.IsSuccessStatusCode,
            $"{request}: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<JsonObject>())!;
    }

    private async Task<JsonObject> StoredJson(string id)
    {
        var settings = await _app.Services.GetRequiredService<Configuration>().Store.GetAllAsync();
        return JsonNode.Parse(settings.Single(s => s.Key == $"feature_management:flags/{id}").Value!)!.AsObject();
    }

    private static string[] Ids(JsonObject result) =>
        result["flags"]!.AsArray().Select(f => f!["id"]!.GetValue<string>()).ToArray();

    private static JsonObject Item(JsonObject result, string id) =>
        result["flags"]!.AsArray().Single(f => f!["id"]!.GetValue<string>() == id)!.AsObject();
}

/// <summary>A filter of the application's own, registered with an alias.</summary>
[FilterAlias("Acme.Browser")]
internal sealed class BrowserFilter : IFeatureFilter
{
    public Task<bool> EvaluateAsync(FeatureFilterEvaluationContext context) => Task.FromResult(false);
}

/// <summary>An authentication scheme that never finds a user.</summary>
internal sealed class NoUserHandler(
    Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions> options,
    Microsoft.Extensions.Logging.ILoggerFactory logger,
    System.Text.Encodings.Web.UrlEncoder encoder)
    : Microsoft.AspNetCore.Authentication.AuthenticationHandler<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<Microsoft.AspNetCore.Authentication.AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.NoResult());
}
