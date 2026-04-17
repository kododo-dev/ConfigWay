using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Kododo.ConfigWay.Core.Model;
using Kododo.ConfigWay.UI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace Kododo.ConfigWay.Tests.Integration;

/// <summary>
/// Full HTTP stack tests against an in-memory ASP.NET Core app.
/// Tests call the same endpoints as the SPA:
///   POST /config/api/GetConfiguration
///   POST /config/api/UpdateConfiguration
/// </summary>
public class ConfigurationApiTests : IAsyncLifetime
{
    private WebApplication _app    = null!;
    private HttpClient     _client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.AddConfigWay(x =>
        {
            x.AddOptions<SimpleOptions>("Simple");
            x.AddOptions<NestedOptions>("Nested");
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

    // ── GET configuration ─────────────────────────────────────────────────────

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

        sections.Should().HaveCount(2);
        sections.Select(s => s.Key).Should().BeEquivalentTo("Simple", "Nested");
    }

    [Fact]
    public async Task GetConfiguration_FlatSection_ContainsExpectedFields()
    {
        var sections = await FetchSections();

        var simple = sections.Single(s => s.Key == "Simple");
        simple.Fields.Select(f => f.Key).Should().BeEquivalentTo("Simple:Name", "Simple:Value");
        simple.Sections.Should().BeEmpty();
    }

    [Fact]
    public async Task GetConfiguration_NestedSection_ContainsSubSection()
    {
        var sections = await FetchSections();

        var nested = sections.Single(s => s.Key == "Nested");
        nested.Fields.Should().ContainSingle(f => f.Key == "Nested:TopLevel");
        nested.Sections.Should().ContainSingle(s => s.Key == "Nested:Child");
    }

    [Fact]
    public async Task GetConfiguration_InitialValues_AreNull()
    {
        var sections = await FetchSections();

        sections.SelectMany(s => s.Fields)
            .Should().AllSatisfy(f => f.Value.Should().BeNull());
    }

    // ── UPDATE configuration ──────────────────────────────────────────────────

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
            .Fields.Single(f => f.Key == "Simple:Name");

        field.Value.Should().Be("Alice");
    }

    [Fact]
    public async Task UpdateConfiguration_MultipleUpdates_LastValueWins()
    {
        await PostUpdateConfiguration([new Setting("Simple:Name", "First")]);
        await PostUpdateConfiguration([new Setting("Simple:Name", "Second")]);

        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Simple").Fields.Single(f => f.Key == "Simple:Name");

        field.Value.Should().Be("Second");
    }

    [Fact]
    public async Task UpdateConfiguration_PartialUpdate_OtherFieldsPreserved()
    {
        await PostUpdateConfiguration([
            new Setting("Simple:Name", "Alice"),
            new Setting("Simple:Value", "42")
        ]);

        // Update only Name
        await PostUpdateConfiguration([new Setting("Simple:Name", "Bob")]);

        var sections = await FetchSections();
        var fields = sections.Single(s => s.Key == "Simple").Fields;

        fields.Single(f => f.Key == "Simple:Name").Value.Should().Be("Bob");
        fields.Single(f => f.Key == "Simple:Value").Value.Should().Be("42");
    }

    [Fact]
    public async Task UpdateConfiguration_NestedField_SavedAndRetrieved()
    {
        await PostUpdateConfiguration([new Setting("Nested:Child:ChildProp", "deep")]);

        var sections = await FetchSections();
        var subSection = sections.Single(s => s.Key == "Nested").Sections.Single();
        var field = subSection.Fields.Single(f => f.Key == "Nested:Child:ChildProp");

        field.Value.Should().Be("deep");
    }

    [Fact]
    public async Task UpdateConfiguration_NullValue_StoredAndReturned()
    {
        await PostUpdateConfiguration([new Setting("Simple:Name", "Alice")]);
        await PostUpdateConfiguration([new Setting("Simple:Name", null)]);

        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Simple").Fields.Single(f => f.Key == "Simple:Name");

        field.Value.Should().BeNull();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Task<HttpResponseMessage> PostGetConfiguration() =>
        _client.PostAsJsonAsync("/config/api/GetConfiguration", new { });

    private async Task<HttpResponseMessage> PostUpdateConfiguration(Setting[] settings)
    {
        var json = JsonSerializer.Serialize(new { Settings = settings }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var response = await _client.PostAsync("/config/api/UpdateConfiguration", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        // No artificial delay: UpdateConfigurationHandler awaits ReloadAllAsync synchronously
        // before returning, so the new values are already observable when the response arrives.
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Xunit.Sdk.XunitException(
                $"UpdateConfiguration failed with {(int)response.StatusCode} {response.StatusCode}. Body: {body}.");
        }
        return response;
    }

    private async Task<SectionDto[]> FetchSections()
    {
        var response = await PostGetConfiguration();
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SectionDto[]>())!;
    }

    // ── Minimal DTOs for JSON deserialization ─────────────────────────────────
    // Mirror the shape of Kododo.ConfigWay.UI.DTO.Section / Field.
    // Only properties the tests actually inspect are included; JsonSerializerDefaults.Web
    // handles camelCase and is case-insensitive on deserialization.

    private sealed record SectionDto(
        string Key,
        string? Name,
        SectionDto[] Sections,
        FieldDto[] Fields,
        string? Description);

    private sealed record FieldDto(
        string Key,
        string? Name,
        int Type,
        string? Value,
        string? Description);
}