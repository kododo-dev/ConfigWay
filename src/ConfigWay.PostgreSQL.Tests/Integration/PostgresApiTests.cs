using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Kododo.ConfigWay.PostgreSQL.Tests.Fixtures;
using Kododo.ConfigWay.UI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace Kododo.ConfigWay.PostgreSQL.Tests.Integration;

[Collection("PostgreSQL")]
public class PostgresApiTests : IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private WebApplication _app    = null!;
    private HttpClient     _client = null!;

    public PostgresApiTests(PostgreSqlFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.AddConfigWay(x =>
        {
            x.AddOptions<SimpleOptions>("Simple");
            x.AddOptions<NestedOptions>("Nested");
            x.UsePostgreSql(_fixture.ConnectionString);
            x.AddUiEditor();
        });

        _app = builder.Build();
        _app.UseConfigWay();

        await _app.StartAsync();
        _client = _app.GetTestClient();

        await _fixture.ResetAsync();
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
    public async Task GetConfiguration_ReturnsRegisteredOptions()
    {
        var sections = await FetchSections();

        sections.Should().HaveCount(2);
        sections.Select(s => s.Key).Should().BeEquivalentTo("Simple", "Nested");
    }

    [Fact]
    public async Task GetConfiguration_InitialValues_AreNull()
    {
        var sections = await FetchSections();
        sections.SelectMany(s => s.Fields).Should().AllSatisfy(f => f.Value.Should().BeNull());
    }

    [Fact]
    public async Task UpdateConfiguration_ValidSettings_Returns200WithEmptyErrors()
    {
        var response = await PostUpdate(new[] { new { key = "Simple:Name", value = "Alice" } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var errors = await response.Content.ReadFromJsonAsync<string[]>();
        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateConfiguration_AfterSave_GetReturnsUpdatedValue()
    {
        await PostUpdate(new[] { new { key = "Simple:Name", value = "Alice" } });

        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Simple").Fields.Single(f => f.Key == "Name");

        field.Value.Should().Be("Alice");
    }

    [Fact]
    public async Task UpdateConfiguration_PersistedToPostgres_SurvivesAppRestart()
    {
        await PostUpdate(new[] { new { key = "Simple:Name", value = "Persistent" } });

        await using var restartedApp = await CreateAppAsync();
        using var restartedClient    = restartedApp.GetTestClient();

        var response  = await restartedClient.PostAsJsonAsync("/config/api/GetConfiguration", new { });
        var sections  = (await response.Content.ReadFromJsonAsync<SectionDto[]>())!;
        var field     = sections.Single(s => s.Key == "Simple").Fields.Single(f => f.Key == "Name");

        field.Value.Should().Be("Persistent");
    }

    [Fact]
    public async Task UpdateConfiguration_MultipleUpdates_LastValueWins()
    {
        await PostUpdate(new[] { new { key = "Simple:Name", value = "First"  } });
        await PostUpdate(new[] { new { key = "Simple:Name", value = "Second" } });

        var sections = await FetchSections();
        var field    = sections.Single(s => s.Key == "Simple").Fields.Single(f => f.Key == "Name");

        field.Value.Should().Be("Second");
    }

    [Fact]
    public async Task UpdateConfiguration_PartialUpdate_OtherFieldsPreserved()
    {
        await PostUpdate(new[]
        {
            new { key = "Simple:Name",  value = "Alice" },
            new { key = "Simple:Value", value = "42" },
        });

        await PostUpdate(new[] { new { key = "Simple:Name", value = "Bob" } });

        var sections = await FetchSections();
        var fields   = sections.Single(s => s.Key == "Simple").Fields;

        fields.Single(f => f.Key == "Name").Value.Should().Be("Bob");
        fields.Single(f => f.Key == "Value").Value.Should().Be("42");
    }

    [Fact]
    public async Task UpdateConfiguration_NestedField_PersistedAndRetrieved()
    {
        await PostUpdate(new[] { new { key = "Nested:Child:ChildProp", value = "deep" } });

        var sections   = await FetchSections();
        var subSection = sections.Single(s => s.Key == "Nested").Sections.Single(s => s.Key == "Child");
        var field      = subSection.Fields.Single(f => f.Key == "ChildProp");

        field.Value.Should().Be("deep");
    }

    [Fact]
    public async Task UpdateConfiguration_NullValue_ClearsField()
    {
        await PostUpdate(new[] { new { key = "Simple:Name", value = "Alice" } });
        await PostUpdate(new[] { new { key = "Simple:Name", value = (string?)null } });

        var sections = await FetchSections();
        var field    = sections.Single(s => s.Key == "Simple").Fields.Single(f => f.Key == "Name");

        field.Value.Should().BeNull();
    }

    private Task<HttpResponseMessage> PostGetConfiguration() =>
        _client.PostAsJsonAsync("/config/api/GetConfiguration", new { });

    private Task<HttpResponseMessage> PostUpdate(object settings)
    {
        var json = JsonSerializer.Serialize(new { Settings = settings }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return _client.PostAsync("/config/api/UpdateConfiguration", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
    }

    private async Task<SectionDto[]> FetchSections()
    {
        var response = await PostGetConfiguration();
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SectionDto[]>())!;
    }

    private async Task<WebApplication> CreateAppAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.AddConfigWay(x =>
        {
            x.AddOptions<SimpleOptions>("Simple");
            x.AddOptions<NestedOptions>("Nested");
            x.UsePostgreSql(_fixture.ConnectionString);
            x.AddUiEditor();
        });

        var app = builder.Build();
        app.UseConfigWay();
        await app.StartAsync();
        return app;
    }

    private record FieldDto(string Key, string? Value, string? Description);
    private record SectionDto(string Key, string Name, FieldDto[] Fields, SectionDto[] Sections, string? Description);
}
