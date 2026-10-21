using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kododo.ConfigWay.PostgreSQL.Tests.Fixtures;
using Kododo.ConfigWay.UI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
        await _fixture.ResetAsync();

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
        _app.UseConfigWay().AllowAnonymous();

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
    public async Task GetConfiguration_ReturnsRegisteredOptions()
    {
        var sections = await FetchSections();

        Assert.Equal(2, sections.Length);
        Assert.Equivalent(new[] { "Simple", "Nested" }, sections.Select(s => s.Key), strict: true);
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
        var response = await PostUpdate(new[] { new { key = "Simple:Name", value = "Alice" } });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task UpdateConfiguration_AfterSave_GetReturnsUpdatedValue()
    {
        await PostUpdate(new[] { new { key = "Simple:Name", value = "Alice" } });

        var sections = await FetchSections();
        var field = sections.Single(s => s.Key == "Simple").Fields.Single(f => f.Key == "Name");

        Assert.Equal("Alice", field.Value);
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

        Assert.Equal("Persistent", field.Value);
    }

    [Fact]
    public async Task UpdateConfiguration_MultipleUpdates_LastValueWins()
    {
        await PostUpdate(new[] { new { key = "Simple:Name", value = "First"  } });
        await PostUpdate(new[] { new { key = "Simple:Name", value = "Second" } });

        var sections = await FetchSections();
        var field    = sections.Single(s => s.Key == "Simple").Fields.Single(f => f.Key == "Name");

        Assert.Equal("Second", field.Value);
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

        Assert.Equal("Bob", fields.Single(f => f.Key == "Name").Value);
        Assert.Equal("42", fields.Single(f => f.Key == "Value").Value);
    }

    [Fact]
    public async Task UpdateConfiguration_NestedField_PersistedAndRetrieved()
    {
        await PostUpdate(new[] { new { key = "Nested:Child:ChildProp", value = "deep" } });

        var sections   = await FetchSections();
        var subSection = sections.Single(s => s.Key == "Nested").Sections.Single(s => s.Key == "Child");
        var field      = subSection.Fields.Single(f => f.Key == "ChildProp");

        Assert.Equal("deep", field.Value);
    }

    [Fact]
    public async Task UpdateConfiguration_NullValue_ClearsField()
    {
        await PostUpdate(new[] { new { key = "Simple:Name", value = "Alice" } });
        await PostUpdate(new[] { new { key = "Simple:Name", value = (string?)null } });

        var sections = await FetchSections();
        var field    = sections.Single(s => s.Key == "Simple").Fields.Single(f => f.Key == "Name");

        Assert.Null(field.Value);
    }

    [Fact]
    public async Task UpdateConfiguration_WithReloadInterval_ReachesAnotherReplica()
    {
        await using var replica = await CreateAppAsync(reloadInterval: TimeSpan.FromMilliseconds(100));
        var monitor = replica.Services.GetRequiredService<IOptionsMonitor<SimpleOptions>>();

        await PostUpdate(new[] { new { key = "Simple:Name", value = "FromAnotherReplica" } });

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (monitor.CurrentValue.Name != "FromAnotherReplica" && DateTime.UtcNow < deadline)
            await Task.Delay(50);

        Assert.Equal("FromAnotherReplica", monitor.CurrentValue.Name);
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

    private async Task<WebApplication> CreateAppAsync(TimeSpan? reloadInterval = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.AddConfigWay(x =>
        {
            x.AddOptions<SimpleOptions>("Simple");
            x.AddOptions<NestedOptions>("Nested");
            x.UsePostgreSql(_fixture.ConnectionString);
            x.AddUiEditor();
            x.ReloadInterval = reloadInterval;
        });

        var app = builder.Build();
        app.UseConfigWay().AllowAnonymous();
        await app.StartAsync();
        return app;
    }

    private record FieldDto(string Key, string? Value, string? Description);
    private record SectionDto(string Key, string Name, FieldDto[] Fields, SectionDto[] Sections, string? Description);
}
