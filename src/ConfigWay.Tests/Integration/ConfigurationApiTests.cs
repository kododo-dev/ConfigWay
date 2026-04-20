using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetConfiguration_ReturnsRegisteredSections()
    {
        var sections = await FetchSections();

        sections.Should().HaveCount(5);
        sections.Select(s => s.Key)
            .Should().BeEquivalentTo("Simple", "Nested", "Typed", "Arrays", "Complex");
    }

    [Fact]
    public async Task GetConfiguration_FlatSection_ContainsExpectedFields()
    {
        var sections = await FetchSections();

        var simple = sections.Single(s => s.Key == "Simple");
        simple.Fields.Select(f => f.Key).Should().BeEquivalentTo("Name", "Value");
        simple.Sections.Should().BeEmpty();
        simple.Arrays.Should().BeEmpty();
    }

    [Fact]
    public async Task GetConfiguration_NestedSection_ContainsSubSection()
    {
        var sections = await FetchSections();

        var nested = sections.Single(s => s.Key == "Nested");
        nested.Fields.Should().ContainSingle(f => f.Key == "TopLevel");
        nested.Sections.Should().ContainSingle(s => s.Key == "Child");
    }

    [Fact]
    public async Task GetConfiguration_InitialValues_AreNull()
    {
        var sections = await FetchSections();

        sections.SelectMany(s => s.Fields)
            .Should().AllSatisfy(f => f.Value.Should().BeNull());
    }

    [Fact]
    public async Task UpdateConfiguration_ValidSettings_Returns200WithEmptyErrors()
    {
        var settings = new[] { new Setting("Simple:Name", "Alice") };
        var response = await PostUpdateConfiguration(settings);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var errors = await response.Content.ReadFromJsonAsync<string[]>();
        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateConfiguration_AfterSave_GetReturnsUpdatedValue()
    {
        await PostUpdateConfiguration([new Setting("Simple:Name", "Alice")]);

        var sections = await FetchSections();
        var field = sections
            .Single(s => s.Key == "Simple")
            .Fields.Single(f => f.Key == "Name");

        field.Value.Should().Be("Alice");
    }

    [Fact]
    public async Task UpdateConfiguration_MultipleUpdates_LastValueWins()
    {
        await PostUpdateConfiguration([new Setting("Simple:Name", "First")]);
        await PostUpdateConfiguration([new Setting("Simple:Name", "Second")]);

        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Simple").Fields.Single(f => f.Key == "Name");

        field.Value.Should().Be("Second");
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

        fields.Single(f => f.Key == "Name").Value.Should().Be("Bob");
        fields.Single(f => f.Key == "Value").Value.Should().Be("42");
    }

    [Fact]
    public async Task UpdateConfiguration_NestedField_SavedAndRetrieved()
    {
        await PostUpdateConfiguration([new Setting("Nested:Child:ChildProp", "deep")]);

        var sections = await FetchSections();
        var subSection = sections.Single(s => s.Key == "Nested").Sections.Single();
        var field = subSection.Fields.Single(f => f.Key == "ChildProp");

        field.Value.Should().Be("deep");
    }

    [Fact]
    public async Task UpdateConfiguration_NullValue_StoredAndReturned()
    {
        await PostUpdateConfiguration([new Setting("Simple:Name", "Alice")]);
        await PostUpdateConfiguration([new Setting("Simple:Name", null)]);

        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Simple").Fields.Single(f => f.Key == "Name");

        field.Value.Should().BeNull();
    }

    [Fact]
    public async Task GetConfiguration_StringField_HasStringType()
    {
        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Typed").Fields.Single(f => f.Key == "Label");
        field.Type.Should().Be("String");
    }

    [Fact]
    public async Task GetConfiguration_BoolField_HasBoolType()
    {
        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Typed").Fields.Single(f => f.Key == "Enabled");
        field.Type.Should().Be("Bool");
    }

    [Fact]
    public async Task GetConfiguration_NumberField_HasNumberType()
    {
        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Typed").Fields.Single(f => f.Key == "Count");
        field.Type.Should().Be("Number");
    }

    [Fact]
    public async Task GetConfiguration_EnumField_HasEnumType()
    {
        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Typed").Fields.Single(f => f.Key == "Level");
        field.Type.Should().Be("Enum");
    }

    [Fact]
    public async Task GetConfiguration_EnumField_ReturnsOptions()
    {
        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Typed").Fields.Single(f => f.Key == "Level");
        field.Options.Should().NotBeNull();
        field.Options!.Select(o => o.Value).Should().BeEquivalentTo("Low", "Medium", "High");
    }

    [Fact]
    public async Task GetConfiguration_EnumField_DisplayAttributeAppliedToLabels()
    {
        var sections = await FetchSections();
        var options = sections.Single(s => s.Key == "Typed").Fields.Single(f => f.Key == "Level").Options!;
        options.Single(o => o.Value == "Low").Label.Should().Be("Low priority");
        options.Single(o => o.Value == "High").Label.Should().Be("High priority");
        options.Single(o => o.Value == "Medium").Label.Should().Be("Medium");
    }

    [Fact]
    public async Task UpdateConfiguration_BoolValue_SavedAndRetrieved()
    {
        await PostUpdateConfiguration([new Setting("Typed:Enabled", "True")]);

        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Typed").Fields.Single(f => f.Key == "Enabled");
        field.Value.Should().Be("True");
    }

    [Fact]
    public async Task UpdateConfiguration_IntValue_SavedAndRetrieved()
    {
        await PostUpdateConfiguration([new Setting("Typed:Count", "99")]);

        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Typed").Fields.Single(f => f.Key == "Count");
        field.Value.Should().Be("99");
    }

    [Fact]
    public async Task UpdateConfiguration_EnumValue_SavedAndRetrieved()
    {
        await PostUpdateConfiguration([new Setting("Typed:Level", "High")]);

        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Typed").Fields.Single(f => f.Key == "Level");
        field.Value.Should().Be("High");
    }

    [Fact]
    public async Task GetConfiguration_SimpleArraySection_HasArrayEntry()
    {
        var sections = await FetchSections();
        var section = sections.Single(s => s.Key == "Arrays");

        section.Arrays.Should().Contain(a => a.Key == "Tags");
        section.Arrays.Should().Contain(a => a.Key == "Ports");
    }

    [Fact]
    public async Task GetConfiguration_SimpleStringArray_IsSimpleTrue()
    {
        var sections = await FetchSections();
        var arr = sections.Single(s => s.Key == "Arrays").Arrays.Single(a => a.Key == "Tags");

        arr.IsSimple.Should().BeTrue();
    }

    [Fact]
    public async Task GetConfiguration_SimpleStringArray_TemplateTypeIsString()
    {
        var sections = await FetchSections();
        var arr = sections.Single(s => s.Key == "Arrays").Arrays.Single(a => a.Key == "Tags");

        arr.Template.Type.Should().Be("String");
    }

    [Fact]
    public async Task GetConfiguration_SimpleIntArray_TemplateTypeIsNumber()
    {
        var sections = await FetchSections();
        var arr = sections.Single(s => s.Key == "Arrays").Arrays.Single(a => a.Key == "Ports");

        arr.Template.Type.Should().Be("Number");
    }

    [Fact]
    public async Task GetConfiguration_SimpleArray_InitiallyHasNoItems()
    {
        var sections = await FetchSections();
        var arr = sections.Single(s => s.Key == "Arrays").Arrays.Single(a => a.Key == "Tags");

        arr.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetConfiguration_ComplexArray_IsSimpleFalse()
    {
        var sections = await FetchSections();
        var arr = sections.Single(s => s.Key == "Complex").Arrays.Single(a => a.Key == "Items");

        arr.IsSimple.Should().BeFalse();
    }

    [Fact]
    public async Task GetConfiguration_ComplexArray_TemplateHasFields()
    {
        var sections = await FetchSections();
        var arr = sections.Single(s => s.Key == "Complex").Arrays.Single(a => a.Key == "Items");

        arr.Template.Fields.Should().NotBeEmpty();
        arr.Template.Fields.Select(f => f.Key)
            .Should().BeEquivalentTo("Name", "Priority", "Category");
    }

    [Fact]
    public async Task UpdateConfiguration_AddSimpleArrayItem_CanBeRetrieved()
    {
        await PostUpdateConfiguration([new Setting("Arrays:Tags:0", "production")]);

        var sections = await FetchSections();
        var arr = sections.Single(s => s.Key == "Arrays").Arrays.Single(a => a.Key == "Tags");

        arr.Items.Should().ContainSingle(i => i.Index == 0 && i.Value == "production");
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

        items.Should().HaveCount(3);
        items.Select(i => i.Value).Should().BeEquivalentTo("dev", "staging", "production");
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

        item.Index.Should().Be(0);
        item.Fields.Single(f => f.Key == "Name").Value.Should().Be("First");
        item.Fields.Single(f => f.Key == "Priority").Value.Should().Be("10");
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

        items.Should().ContainSingle(i => i.Index == 0);
        items.Should().NotContain(i => i.Index == 1);
    }

    [Fact]
    public async Task UpdateConfiguration_DeleteReturns200WithEmptyErrors()
    {
        await PostUpdateConfiguration([new Setting("Arrays:Tags:0", "temp")]);

        var response = await PostUpdateConfiguration([], keysToDelete: ["Arrays:Tags:0"]);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var errors = await response.Content.ReadFromJsonAsync<string[]>();
        errors.Should().BeEmpty();
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
        sections.Single(s => s.Key == "Simple").Fields
            .Should().AllSatisfy(f => f.DefaultValue.Should().BeNull());
    }

    [Fact]
    public async Task GetConfiguration_ArrayItem_DefaultValueNullWithNoBaseConfig()
    {
        await PostUpdateConfiguration([new Setting("Arrays:Tags:0", "tag1")]);

        var sections = await FetchSections();
        var item = sections.Single(s => s.Key == "Arrays").Arrays.Single(a => a.Key == "Tags").Items.Single();
        item.DefaultValue.Should().BeNull();
    }

    [Fact]
    public async Task GetConfiguration_ArrayItemAddedViaConfigWay_IsDeletable()
    {
        await PostUpdateConfiguration([new Setting("Arrays:Tags:0", "tag1")]);

        var sections = await FetchSections();
        var item = sections.Single(s => s.Key == "Arrays").Arrays.Single(a => a.Key == "Tags").Items.Single();
        item.IsDeletable.Should().BeTrue();
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
        field.DefaultValue.Should().Be("DefaultName");
    }

    [Fact]
    public async Task GetConfiguration_WithBaseConfig_ValueAndDefaultValueMatchInitially()
    {
        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Simple").Fields.Single(f => f.Key == "Name");
        field.Value.Should().Be("DefaultName");
        field.DefaultValue.Should().Be("DefaultName");
    }

    [Fact]
    public async Task UpdateConfiguration_Override_DefaultValueRemainsFromBaseConfig()
    {
        await PostUpdateConfiguration([new Setting("Simple:Name", "Override")]);

        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Simple").Fields.Single(f => f.Key == "Name");
        field.Value.Should().Be("Override");
        field.DefaultValue.Should().Be("DefaultName");
    }

    [Fact]
    public async Task UpdateConfiguration_DeleteOverride_ValueFallsBackToDefault()
    {
        await PostUpdateConfiguration([new Setting("Simple:Name", "Override")]);
        await PostUpdateConfiguration([], keysToDelete: ["Simple:Name"]);

        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Simple").Fields.Single(f => f.Key == "Name");
        field.Value.Should().Be("DefaultName");
        field.DefaultValue.Should().Be("DefaultName");
    }

    [Fact]
    public async Task GetConfiguration_BaseConfigArrayItem_DefaultValuePopulated()
    {
        var sections = await FetchSections();
        var items = sections.Single(s => s.Key == "Arrays").Arrays.Single(a => a.Key == "Tags").Items;
        items.Single(i => i.Index == 0).DefaultValue.Should().Be("base-tag");
        items.Single(i => i.Index == 1).DefaultValue.Should().Be("base-tag-2");
    }

    [Fact]
    public async Task GetConfiguration_BaseConfigArrayItem_IsNotDeletable()
    {
        var sections = await FetchSections();
        var items = sections.Single(s => s.Key == "Arrays").Arrays.Single(a => a.Key == "Tags").Items;
        items.Should().AllSatisfy(i => i.IsDeletable.Should().BeFalse());
    }

    [Fact]
    public async Task UpdateConfiguration_OverrideBaseArrayItem_DefaultValueUnchanged()
    {
        await PostUpdateConfiguration([new Setting("Arrays:Tags:0", "overridden")]);

        var sections = await FetchSections();
        var item = sections.Single(s => s.Key == "Arrays").Arrays.Single(a => a.Key == "Tags")
            .Items.Single(i => i.Index == 0);
        item.Value.Should().Be("overridden");
        item.DefaultValue.Should().Be("base-tag");
    }

    [Fact]
    public async Task UpdateConfiguration_DeleteBaseArrayItemOverride_ValueFallsBackToDefault()
    {
        await PostUpdateConfiguration([new Setting("Arrays:Tags:0", "overridden")]);
        await PostUpdateConfiguration([], keysToDelete: ["Arrays:Tags:0"]);

        var sections = await FetchSections();
        var item = sections.Single(s => s.Key == "Arrays").Arrays.Single(a => a.Key == "Tags")
            .Items.Single(i => i.Index == 0);
        item.Value.Should().Be("base-tag");
        item.DefaultValue.Should().Be("base-tag");
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
        field.IsSensitive.Should().BeTrue();
    }

    [Fact]
    public async Task GetConfiguration_NonPasswordField_IsSensitiveFalse()
    {
        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Sensitive").Fields.Single(f => f.Key == "Username");
        field.IsSensitive.Should().BeFalse();
    }

    [Fact]
    public async Task GetConfiguration_SensitiveField_InitiallyValueIsNull()
    {
        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Sensitive").Fields.Single(f => f.Key == "Password");
        field.Value.Should().BeNull();
    }

    [Fact]
    public async Task UpdateConfiguration_SensitiveField_ValueReturnedAsMask()
    {
        await PostUpdateConfiguration([new Setting("Sensitive:Password", "secret123")]);

        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Sensitive").Fields.Single(f => f.Key == "Password");
        field.Value.Should().Be("***");
    }

    [Fact]
    public async Task UpdateConfiguration_SensitiveField_ActualValueNeverExposed()
    {
        await PostUpdateConfiguration([new Setting("Sensitive:ApiKey", "real-key")]);

        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Sensitive").Fields.Single(f => f.Key == "ApiKey");
        field.Value.Should().NotBe("real-key");
    }

    [Fact]
    public async Task UpdateConfiguration_SensitiveField_CanBeOverwritten()
    {
        await PostUpdateConfiguration([new Setting("Sensitive:Password", "first")]);
        await PostUpdateConfiguration([new Setting("Sensitive:Password", "second")]);

        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Sensitive").Fields.Single(f => f.Key == "Password");
        field.Value.Should().Be("***");
    }

    [Fact]
    public async Task UpdateConfiguration_SensitiveField_DeletedValueBecomesNull()
    {
        await PostUpdateConfiguration([new Setting("Sensitive:Password", "secret")]);
        await PostUpdateConfiguration([], keysToDelete: ["Sensitive:Password"]);

        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Sensitive").Fields.Single(f => f.Key == "Password");
        field.Value.Should().BeNull();
    }

    [Fact]
    public async Task GetConfiguration_NestedSensitiveField_IsSensitiveTrue()
    {
        var sections = await FetchSections();
        var creds = sections.Single(s => s.Key == "Nested").Sections.Single(s => s.Key == "Credentials");
        creds.Fields.Single(f => f.Key == "Secret").IsSensitive.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateConfiguration_NestedSensitiveField_ValueReturnedAsMask()
    {
        await PostUpdateConfiguration([new Setting("Nested:Credentials:Secret", "nested-secret")]);

        var sections = await FetchSections();
        var creds = sections.Single(s => s.Key == "Nested").Sections.Single(s => s.Key == "Credentials");
        creds.Fields.Single(f => f.Key == "Secret").Value.Should().Be("***");
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