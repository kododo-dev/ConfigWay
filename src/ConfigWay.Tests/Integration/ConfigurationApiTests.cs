using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Kododo.ConfigWay.Core.Model;
using Kododo.ConfigWay.UI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Kododo.ConfigWay.Tests.Integration;

public class ConfigurationApiTests : IAsyncLifetime
{
    private WebApplication _app    = null!;
    private HttpClient     _client = null!;

    private static readonly JsonSerializerOptions JsonOpts =
        new(JsonSerializerDefaults.Web);

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.AddConfigWay(x =>
        {
            x.AddOptions<SimpleOptions>("Simple");
            x.AddOptions<NestedOptions>("Nested");
            x.AddOptions<TypedOptions>("Typed");
            x.AddOptions<SimpleArrayOptions>("Arrays");
            x.AddOptions<ComplexArrayOptions>("Complex");
            x.AddUiEditor();
        });

        _app = builder.Build();
        _app.UseConfigWay();

        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task GetConfiguration_Returns200()
    {
        var response = await PostGetConfiguration();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetConfiguration_ReturnsRegisteredSections()
    {
        var sections = await FetchSections();

        Assert.Equal(5, sections.Length);
        Assert.Equivalent(
            new[] { "Simple", "Nested", "Typed", "Arrays", "Complex" },
            sections.Select(s => s.Key),
            strict: true);
    }

    [Fact]
    public async Task GetConfiguration_FlatSection_ContainsExpectedFields()
    {
        var sections = await FetchSections();

        var simple = sections.Single(s => s.Key == "Simple");
        Assert.Equivalent(new[] { "Name", "Value" }, simple.Fields.Select(f => f.Key), strict: true);
        Assert.Empty(simple.Sections);
        Assert.Empty(simple.Arrays);
    }

    [Fact]
    public async Task GetConfiguration_NestedSection_ContainsSubSection()
    {
        var sections = await FetchSections();

        var nested = sections.Single(s => s.Key == "Nested");
        Assert.Single(nested.Fields, f => f.Key == "TopLevel");
        Assert.Single(nested.Sections, s => s.Key == "Child");
    }

    [Fact]
    public async Task GetConfiguration_InitialValues_AreNull()
    {
        var sections = await FetchSections();

        Assert.All(sections.SelectMany(s => s.Fields), f => Assert.Null(f.Value));
    }

    [Fact]
    public async Task UpdateConfiguration_ValidSettings_Returns204()
    {
        var settings = new[] { new Setting("Simple:Name", "Alice") };
        var response = await PostUpdateConfiguration(settings);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task UpdateConfiguration_InvalidValue_ReturnsValidationProblem()
    {
        var response = await PostUpdateConfiguration([new Setting("Typed:Count", "not a number")]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var message = Assert.Single(problem.GetProperty("errors").GetProperty("settings").EnumerateArray());
        Assert.StartsWith("Typed:Count", message.GetString());
    }

    [Fact]
    public async Task UpdateConfiguration_InvalidValue_NothingSaved()
    {
        await PostUpdateConfiguration([new Setting("Simple:Name", "Alice"), new Setting("Typed:Count", "not a number")]);

        var sections = await FetchSections();
        var field = sections
            .Single(s => s.Key == "Simple")
            .Fields.Single(f => f.Key == "Name");

        Assert.Null(field.Value);
    }

    [Fact]
    public async Task UpdateConfiguration_AfterSave_GetReturnsUpdatedValue()
    {
        await PostUpdateConfiguration([new Setting("Simple:Name", "Alice")]);

        var sections = await FetchSections();
        var field = sections
            .Single(s => s.Key == "Simple")
            .Fields.Single(f => f.Key == "Name");

        Assert.Equal("Alice", field.Value);
    }

    [Fact]
    public async Task UpdateConfiguration_MultipleUpdates_LastValueWins()
    {
        await PostUpdateConfiguration([new Setting("Simple:Name", "First")]);
        await PostUpdateConfiguration([new Setting("Simple:Name", "Second")]);

        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Simple").Fields.Single(f => f.Key == "Name");

        Assert.Equal("Second", field.Value);
    }

    [Fact]
    public async Task UpdateConfiguration_PartialUpdate_OtherFieldsPreserved()
    {
        await PostUpdateConfiguration([
            new Setting("Simple:Name", "Alice"),
            new Setting("Simple:Value", "42")
        ]);

        await PostUpdateConfiguration([new Setting("Simple:Name", "Bob")]);

        var sections = await FetchSections();
        var fields = sections.Single(s => s.Key == "Simple").Fields;

        Assert.Equal("Bob", fields.Single(f => f.Key == "Name").Value);
        Assert.Equal("42", fields.Single(f => f.Key == "Value").Value);
    }

    [Fact]
    public async Task UpdateConfiguration_NestedField_SavedAndRetrieved()
    {
        await PostUpdateConfiguration([new Setting("Nested:Child:ChildProp", "deep")]);

        var sections = await FetchSections();
        var subSection = sections.Single(s => s.Key == "Nested").Sections.Single();
        var field = subSection.Fields.Single(f => f.Key == "ChildProp");

        Assert.Equal("deep", field.Value);
    }

    [Fact]
    public async Task UpdateConfiguration_NullValue_StoredAndReturned()
    {
        await PostUpdateConfiguration([new Setting("Simple:Name", "Alice")]);
        await PostUpdateConfiguration([new Setting("Simple:Name", null)]);

        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Simple").Fields.Single(f => f.Key == "Name");

        Assert.Null(field.Value);
    }

    [Fact]
    public async Task GetConfiguration_StringField_HasStringType()
    {
        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Typed").Fields.Single(f => f.Key == "Label");
        Assert.Equal("String", field.Type);
    }

    [Fact]
    public async Task GetConfiguration_BoolField_HasBoolType()
    {
        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Typed").Fields.Single(f => f.Key == "Enabled");
        Assert.Equal("Bool", field.Type);
    }

    [Fact]
    public async Task GetConfiguration_NumberField_HasNumberType()
    {
        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Typed").Fields.Single(f => f.Key == "Count");
        Assert.Equal("Number", field.Type);
    }

    [Fact]
    public async Task GetConfiguration_EnumField_HasEnumType()
    {
        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Typed").Fields.Single(f => f.Key == "Level");
        Assert.Equal("Enum", field.Type);
    }

    [Fact]
    public async Task GetConfiguration_EnumField_ReturnsOptions()
    {
        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Typed").Fields.Single(f => f.Key == "Level");
        Assert.NotNull(field.Options);
        Assert.Equivalent(new[] { "Low", "Medium", "High" }, field.Options!.Select(o => o.Value), strict: true);
    }

    [Fact]
    public async Task GetConfiguration_EnumField_DisplayAttributeAppliedToLabels()
    {
        var sections = await FetchSections();
        var options = sections.Single(s => s.Key == "Typed").Fields.Single(f => f.Key == "Level").Options!;
        Assert.Equal("Low priority", options.Single(o => o.Value == "Low").Label);
        Assert.Equal("High priority", options.Single(o => o.Value == "High").Label);
        Assert.Equal("Medium", options.Single(o => o.Value == "Medium").Label);
    }

    [Fact]
    public async Task UpdateConfiguration_BoolValue_SavedAndRetrieved()
    {
        await PostUpdateConfiguration([new Setting("Typed:Enabled", "True")]);

        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Typed").Fields.Single(f => f.Key == "Enabled");
        Assert.Equal("True", field.Value);
    }

    [Fact]
    public async Task UpdateConfiguration_IntValue_SavedAndRetrieved()
    {
        await PostUpdateConfiguration([new Setting("Typed:Count", "99")]);

        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Typed").Fields.Single(f => f.Key == "Count");
        Assert.Equal("99", field.Value);
    }

    [Fact]
    public async Task UpdateConfiguration_EnumValue_SavedAndRetrieved()
    {
        await PostUpdateConfiguration([new Setting("Typed:Level", "High")]);

        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Typed").Fields.Single(f => f.Key == "Level");
        Assert.Equal("High", field.Value);
    }

    [Fact]
    public async Task GetConfiguration_SimpleArraySection_HasArrayEntry()
    {
        var sections = await FetchSections();
        var section = sections.Single(s => s.Key == "Arrays");

        Assert.Contains(section.Arrays, a => a.Key == "Tags");
        Assert.Contains(section.Arrays, a => a.Key == "Ports");
    }

    [Fact]
    public async Task GetConfiguration_SimpleStringArray_IsSimpleTrue()
    {
        var sections = await FetchSections();
        var arr = sections.Single(s => s.Key == "Arrays").Arrays.Single(a => a.Key == "Tags");

        Assert.True(arr.IsSimple);
    }

    [Fact]
    public async Task GetConfiguration_SimpleStringArray_TemplateTypeIsString()
    {
        var sections = await FetchSections();
        var arr = sections.Single(s => s.Key == "Arrays").Arrays.Single(a => a.Key == "Tags");

        Assert.Equal("String", arr.Template.Type);
    }

    [Fact]
    public async Task GetConfiguration_SimpleIntArray_TemplateTypeIsNumber()
    {
        var sections = await FetchSections();
        var arr = sections.Single(s => s.Key == "Arrays").Arrays.Single(a => a.Key == "Ports");

        Assert.Equal("Number", arr.Template.Type);
    }

    [Fact]
    public async Task GetConfiguration_SimpleArray_InitiallyHasNoItems()
    {
        var sections = await FetchSections();
        var arr = sections.Single(s => s.Key == "Arrays").Arrays.Single(a => a.Key == "Tags");

        Assert.Empty(arr.Items);
    }

    [Fact]
    public async Task GetConfiguration_ComplexArray_IsSimpleFalse()
    {
        var sections = await FetchSections();
        var arr = sections.Single(s => s.Key == "Complex").Arrays.Single(a => a.Key == "Items");

        Assert.False(arr.IsSimple);
    }

    [Fact]
    public async Task GetConfiguration_ComplexArray_TemplateHasFields()
    {
        var sections = await FetchSections();
        var arr = sections.Single(s => s.Key == "Complex").Arrays.Single(a => a.Key == "Items");

        Assert.NotEmpty(arr.Template.Fields);
        Assert.Equivalent(
            new[] { "Name", "Priority", "Category" },
            arr.Template.Fields.Select(f => f.Key),
            strict: true);
    }

    [Fact]
    public async Task UpdateConfiguration_AddSimpleArrayItem_CanBeRetrieved()
    {
        await PostUpdateConfiguration([new Setting("Arrays:Tags:0", "production")]);

        var sections = await FetchSections();
        var arr = sections.Single(s => s.Key == "Arrays").Arrays.Single(a => a.Key == "Tags");

        Assert.Single(arr.Items, i => i.Index == 0 && i.Value == "production");
    }

    [Fact]
    public async Task UpdateConfiguration_AddMultipleSimpleArrayItems_AllRetrieved()
    {
        await PostUpdateConfiguration([
            new Setting("Arrays:Tags:0", "dev"),
            new Setting("Arrays:Tags:1", "staging"),
            new Setting("Arrays:Tags:2", "production"),
        ]);

        var sections = await FetchSections();
        var items = sections.Single(s => s.Key == "Arrays").Arrays.Single(a => a.Key == "Tags").Items;

        Assert.Equal(3, items.Length);
        Assert.Equivalent(new[] { "dev", "staging", "production" }, items.Select(i => i.Value), strict: true);
    }

    [Fact]
    public async Task UpdateConfiguration_AddComplexArrayItem_FieldsRetrieved()
    {
        await PostUpdateConfiguration([
            new Setting("Complex:Items:0:Name",     "First"),
            new Setting("Complex:Items:0:Priority", "10"),
        ]);

        var sections = await FetchSections();
        var item = sections.Single(s => s.Key == "Complex").Arrays.Single(a => a.Key == "Items")
            .Items.Single();

        Assert.Equal(0, item.Index);
        Assert.Equal("First", item.Fields.Single(f => f.Key == "Name").Value);
        Assert.Equal("10", item.Fields.Single(f => f.Key == "Priority").Value);
    }

    [Fact]
    public async Task UpdateConfiguration_DeleteArrayItem_ItemNoLongerReturned()
    {
        await PostUpdateConfiguration([
            new Setting("Arrays:Tags:0", "keep"),
            new Setting("Arrays:Tags:1", "remove"),
        ]);

        await PostUpdateConfiguration([], keysToDelete: ["Arrays:Tags:1"]);

        var sections = await FetchSections();
        var items = sections.Single(s => s.Key == "Arrays").Arrays.Single(a => a.Key == "Tags").Items;

        Assert.Single(items, i => i.Index == 0);
        Assert.DoesNotContain(items, i => i.Index == 1);
    }

    [Fact]
    public async Task UpdateConfiguration_Delete_Returns204()
    {
        await PostUpdateConfiguration([new Setting("Arrays:Tags:0", "temp")]);

        var response = await PostUpdateConfiguration([], keysToDelete: ["Arrays:Tags:0"]);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private Task<HttpResponseMessage> PostGetConfiguration() =>
        _client.PostAsJsonAsync("/config/api/GetConfiguration", new { });

    private Task<HttpResponseMessage> PostUpdateConfiguration(
        Setting[] settings,
        string[]? keysToDelete = null)
    {
        var payload = new { Settings = settings, KeysToDelete = keysToDelete ?? [] };
        var json    = JsonSerializer.Serialize(payload, JsonOpts);
        return _client.PostAsync(
            "/config/api/UpdateConfiguration",
            new StringContent(json, Encoding.UTF8, "application/json"));
    }

    private async Task<SectionDto[]> FetchSections()
    {
        var response = await PostGetConfiguration();
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SectionDto[]>(JsonOpts))!;
    }

    [Fact]
    public async Task GetConfiguration_Field_DefaultValueNullWithNoBaseConfig()
    {
        var sections = await FetchSections();
        Assert.All(sections.Single(s => s.Key == "Simple").Fields, f => Assert.Null(f.DefaultValue));
    }

    [Fact]
    public async Task GetConfiguration_ArrayItem_DefaultValueNullWithNoBaseConfig()
    {
        await PostUpdateConfiguration([new Setting("Arrays:Tags:0", "tag1")]);

        var sections = await FetchSections();
        var item = sections.Single(s => s.Key == "Arrays").Arrays.Single(a => a.Key == "Tags").Items.Single();
        Assert.Null(item.DefaultValue);
    }

    [Fact]
    public async Task GetConfiguration_ArrayItemAddedViaConfigWay_IsDeletable()
    {
        await PostUpdateConfiguration([new Setting("Arrays:Tags:0", "tag1")]);

        var sections = await FetchSections();
        var item = sections.Single(s => s.Key == "Arrays").Arrays.Single(a => a.Key == "Tags").Items.Single();
        Assert.True(item.IsDeletable);
    }

    private sealed record SectionDto(
        string Key,
        string? Name,
        SectionDto[] Sections,
        FieldDto[] Fields,
        ArrayFieldDto[] Arrays,
        string? Description);

    private sealed record FieldDto(
        string Key,
        string? Name,
        string Type,
        string? Value,
        string? DefaultValue,
        bool IsSensitive,
        bool HasOverride,
        string? Description,
        EnumOptionDto[]? Options);

    private sealed record ArrayFieldDto(
        string Key,
        string? Name,
        bool IsSimple,
        ArrayItemDto[] Items,
        ArrayItemDto Template);

    private sealed record ArrayItemDto(
        int Index,
        bool IsDeletable,
        string? Value,
        string? DefaultValue,
        string? Type,
        FieldDto[] Fields);

    private sealed record EnumOptionDto(string Value, string Label);
}

public class ConfigurationApiDefaultValueTests : IAsyncLifetime
{
    private WebApplication _app    = null!;
    private HttpClient     _client = null!;

    private static readonly JsonSerializerOptions JsonOpts =
        new(JsonSerializerDefaults.Web);

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Simple:Name"]   = "DefaultName",
            ["Simple:Value"]  = "DefaultValue",
            ["Arrays:Tags:0"] = "base-tag",
            ["Arrays:Tags:1"] = "base-tag-2",
        });

        builder.AddConfigWay(x =>
        {
            x.AddOptions<SimpleOptions>("Simple");
            x.AddOptions<SimpleArrayOptions>("Arrays");
            x.AddUiEditor();
        });

        _app = builder.Build();
        _app.UseConfigWay();

        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task GetConfiguration_WithBaseConfig_FieldDefaultValuePopulated()
    {
        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Simple").Fields.Single(f => f.Key == "Name");
        Assert.Equal("DefaultName", field.DefaultValue);
    }

    [Fact]
    public async Task GetConfiguration_WithBaseConfig_ValueAndDefaultValueMatchInitially()
    {
        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Simple").Fields.Single(f => f.Key == "Name");
        Assert.Equal("DefaultName", field.Value);
        Assert.Equal("DefaultName", field.DefaultValue);
    }

    [Fact]
    public async Task UpdateConfiguration_Override_DefaultValueRemainsFromBaseConfig()
    {
        await PostUpdateConfiguration([new Setting("Simple:Name", "Override")]);

        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Simple").Fields.Single(f => f.Key == "Name");
        Assert.Equal("Override", field.Value);
        Assert.Equal("DefaultName", field.DefaultValue);
    }

    [Fact]
    public async Task UpdateConfiguration_DeleteOverride_ValueFallsBackToDefault()
    {
        await PostUpdateConfiguration([new Setting("Simple:Name", "Override")]);
        await PostUpdateConfiguration([], keysToDelete: ["Simple:Name"]);

        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Simple").Fields.Single(f => f.Key == "Name");
        Assert.Equal("DefaultName", field.Value);
        Assert.Equal("DefaultName", field.DefaultValue);
    }

    [Fact]
    public async Task GetConfiguration_BaseConfigArrayItem_DefaultValuePopulated()
    {
        var sections = await FetchSections();
        var items = sections.Single(s => s.Key == "Arrays").Arrays.Single(a => a.Key == "Tags").Items;
        Assert.Equal("base-tag", items.Single(i => i.Index == 0).DefaultValue);
        Assert.Equal("base-tag-2", items.Single(i => i.Index == 1).DefaultValue);
    }

    [Fact]
    public async Task GetConfiguration_BaseConfigArrayItem_IsNotDeletable()
    {
        var sections = await FetchSections();
        var items = sections.Single(s => s.Key == "Arrays").Arrays.Single(a => a.Key == "Tags").Items;
        Assert.All(items, i => Assert.False(i.IsDeletable));
    }

    [Fact]
    public async Task UpdateConfiguration_OverrideBaseArrayItem_DefaultValueUnchanged()
    {
        await PostUpdateConfiguration([new Setting("Arrays:Tags:0", "overridden")]);

        var sections = await FetchSections();
        var item = sections.Single(s => s.Key == "Arrays").Arrays.Single(a => a.Key == "Tags")
            .Items.Single(i => i.Index == 0);
        Assert.Equal("overridden", item.Value);
        Assert.Equal("base-tag", item.DefaultValue);
    }

    [Fact]
    public async Task UpdateConfiguration_DeleteBaseArrayItemOverride_ValueFallsBackToDefault()
    {
        await PostUpdateConfiguration([new Setting("Arrays:Tags:0", "overridden")]);
        await PostUpdateConfiguration([], keysToDelete: ["Arrays:Tags:0"]);

        var sections = await FetchSections();
        var item = sections.Single(s => s.Key == "Arrays").Arrays.Single(a => a.Key == "Tags")
            .Items.Single(i => i.Index == 0);
        Assert.Equal("base-tag", item.Value);
        Assert.Equal("base-tag", item.DefaultValue);
    }

    private Task<HttpResponseMessage> PostGetConfiguration() =>
        _client.PostAsJsonAsync("/config/api/GetConfiguration", new { });

    private Task<HttpResponseMessage> PostUpdateConfiguration(
        Setting[] settings,
        string[]? keysToDelete = null)
    {
        var payload = new { Settings = settings, KeysToDelete = keysToDelete ?? [] };
        var json    = JsonSerializer.Serialize(payload, JsonOpts);
        return _client.PostAsync(
            "/config/api/UpdateConfiguration",
            new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json"));
    }

    private async Task<SectionDto[]> FetchSections()
    {
        var response = await PostGetConfiguration();
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SectionDto[]>(JsonOpts))!;
    }

    private sealed record SectionDto(
        string Key,
        string? Name,
        SectionDto[] Sections,
        FieldDto[] Fields,
        ArrayFieldDto[] Arrays,
        string? Description);

    private sealed record FieldDto(
        string Key,
        string? Name,
        string Type,
        string? Value,
        string? DefaultValue,
        bool IsSensitive,
        bool HasOverride,
        string? Description,
        EnumOptionDto[]? Options);

    private sealed record ArrayFieldDto(
        string Key,
        string? Name,
        bool IsSimple,
        ArrayItemDto[] Items,
        ArrayItemDto Template);

    private sealed record ArrayItemDto(
        int Index,
        bool IsDeletable,
        string? Value,
        string? DefaultValue,
        string? Type,
        FieldDto[] Fields);

    private sealed record EnumOptionDto(string Value, string Label);
}

public class ConfigurationApiSensitiveTests : IAsyncLifetime
{
    private WebApplication _app    = null!;
    private HttpClient     _client = null!;

    private static readonly JsonSerializerOptions JsonOpts =
        new(JsonSerializerDefaults.Web);

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.AddConfigWay(x =>
        {
            x.AddOptions<SensitiveOptions>("Sensitive");
            x.AddOptions<NestedSensitiveOptions>("Nested");
            x.AddUiEditor();
        });

        _app = builder.Build();
        _app.UseConfigWay();

        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task GetConfiguration_PasswordField_IsSensitiveTrue()
    {
        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Sensitive").Fields.Single(f => f.Key == "Password");
        Assert.True(field.IsSensitive);
    }

    [Fact]
    public async Task GetConfiguration_NonPasswordField_IsSensitiveFalse()
    {
        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Sensitive").Fields.Single(f => f.Key == "Username");
        Assert.False(field.IsSensitive);
    }

    [Fact]
    public async Task GetConfiguration_SensitiveField_InitiallyValueIsNull()
    {
        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Sensitive").Fields.Single(f => f.Key == "Password");
        Assert.Null(field.Value);
    }

    [Fact]
    public async Task UpdateConfiguration_SensitiveField_ValueReturnedAsMask()
    {
        await PostUpdateConfiguration([new Setting("Sensitive:Password", "secret123")]);

        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Sensitive").Fields.Single(f => f.Key == "Password");
        Assert.Equal("***", field.Value);
    }

    [Fact]
    public async Task UpdateConfiguration_SensitiveField_ActualValueNeverExposed()
    {
        await PostUpdateConfiguration([new Setting("Sensitive:ApiKey", "real-key")]);

        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Sensitive").Fields.Single(f => f.Key == "ApiKey");
        Assert.NotEqual("real-key", field.Value);
    }

    [Fact]
    public async Task UpdateConfiguration_SensitiveField_CanBeOverwritten()
    {
        await PostUpdateConfiguration([new Setting("Sensitive:Password", "first")]);
        await PostUpdateConfiguration([new Setting("Sensitive:Password", "second")]);

        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Sensitive").Fields.Single(f => f.Key == "Password");
        Assert.Equal("***", field.Value);
    }

    [Fact]
    public async Task UpdateConfiguration_SensitiveField_DeletedValueBecomesNull()
    {
        await PostUpdateConfiguration([new Setting("Sensitive:Password", "secret")]);
        await PostUpdateConfiguration([], keysToDelete: ["Sensitive:Password"]);

        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Sensitive").Fields.Single(f => f.Key == "Password");
        Assert.Null(field.Value);
    }

    [Fact]
    public async Task GetConfiguration_NestedSensitiveField_IsSensitiveTrue()
    {
        var sections = await FetchSections();
        var creds = sections.Single(s => s.Key == "Nested").Sections.Single(s => s.Key == "Credentials");
        Assert.True(creds.Fields.Single(f => f.Key == "Secret").IsSensitive);
    }

    [Fact]
    public async Task UpdateConfiguration_NestedSensitiveField_ValueReturnedAsMask()
    {
        await PostUpdateConfiguration([new Setting("Nested:Credentials:Secret", "nested-secret")]);

        var sections = await FetchSections();
        var creds = sections.Single(s => s.Key == "Nested").Sections.Single(s => s.Key == "Credentials");
        Assert.Equal("***", creds.Fields.Single(f => f.Key == "Secret").Value);
    }

    [Fact]
    public async Task GetConfiguration_SensitiveField_InitiallyHasOverrideFalse()
    {
        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Sensitive").Fields.Single(f => f.Key == "Password");
        Assert.False(field.HasOverride);
    }

    [Fact]
    public async Task UpdateConfiguration_SensitiveField_HasOverrideTrueAfterSave()
    {
        await PostUpdateConfiguration([new Setting("Sensitive:Password", "secret")]);

        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Sensitive").Fields.Single(f => f.Key == "Password");
        Assert.True(field.HasOverride);
    }

    [Fact]
    public async Task UpdateConfiguration_SensitiveField_HasOverrideFalseAfterDelete()
    {
        await PostUpdateConfiguration([new Setting("Sensitive:Password", "secret")]);
        await PostUpdateConfiguration([], keysToDelete: ["Sensitive:Password"]);

        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Sensitive").Fields.Single(f => f.Key == "Password");
        Assert.False(field.HasOverride);
    }

    [Fact]
    public async Task GetConfiguration_NonSensitiveField_InitiallyHasOverrideFalse()
    {
        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Sensitive").Fields.Single(f => f.Key == "Username");
        Assert.False(field.HasOverride);
    }

    [Fact]
    public async Task UpdateConfiguration_NonSensitiveField_HasOverrideTrueAfterSave()
    {
        await PostUpdateConfiguration([new Setting("Sensitive:Username", "admin")]);

        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Sensitive").Fields.Single(f => f.Key == "Username");
        Assert.True(field.HasOverride);
    }

    private Task<HttpResponseMessage> PostGetConfiguration() =>
        _client.PostAsJsonAsync("/config/api/GetConfiguration", new { });

    private Task<HttpResponseMessage> PostUpdateConfiguration(
        Setting[] settings,
        string[]? keysToDelete = null)
    {
        var payload = new { Settings = settings, KeysToDelete = keysToDelete ?? [] };
        var json    = JsonSerializer.Serialize(payload, JsonOpts);
        return _client.PostAsync(
            "/config/api/UpdateConfiguration",
            new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json"));
    }

    private async Task<SectionDto[]> FetchSections()
    {
        var response = await PostGetConfiguration();
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SectionDto[]>(JsonOpts))!;
    }

    private sealed record SectionDto(
        string Key,
        string? Name,
        SectionDto[] Sections,
        FieldDto[] Fields,
        ArrayFieldDto[] Arrays,
        string? Description);

    private sealed record FieldDto(
        string Key,
        string? Name,
        string Type,
        string? Value,
        string? DefaultValue,
        bool IsSensitive,
        bool HasOverride,
        string? Description,
        EnumOptionDto[]? Options);

    private sealed record ArrayFieldDto(
        string Key,
        string? Name,
        bool IsSimple,
        ArrayItemDto[] Items,
        ArrayItemDto Template);

    private sealed record ArrayItemDto(
        int Index,
        bool IsDeletable,
        string? Value,
        string? DefaultValue,
        string? Type,
        FieldDto[] Fields);

    private sealed record EnumOptionDto(string Value, string Label);
}

