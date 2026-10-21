using System.Net;
using System.Net.Http.Json;
using Kododo.ConfigWay.FeatureManagement;
using Kododo.ConfigWay.PostgreSQL.Tests.Fixtures;
using Kododo.ConfigWay.UI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;
using Xunit;

namespace Kododo.ConfigWay.PostgreSQL.Tests.Integration;

/// <summary>Feature flags kept in PostgreSQL: they survive a restart and reach other replicas.</summary>
[Collection("PostgreSQL")]
public class FeatureFlagsStoreTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SavedFlag_SurvivesARestart()
    {
        await using (var app = await CreateAppAsync())
        {
            var response = await app.GetTestClient().PostAsJsonAsync("/config/api/SaveFeatureFlag", new
            {
                Flag = new { Id = "Beta", Enabled = false, RequirementType = "Any", Filters = Array.Empty<object>() },
            });
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        await using var restarted = await CreateAppAsync();
        var featureManager = restarted.Services.GetRequiredService<IVariantFeatureManager>();

        Assert.False(await featureManager.IsEnabledAsync("Beta"));
    }

    [Fact]
    public async Task SavedFlag_ReachesAnotherReplica()
    {
        await using var app = await CreateAppAsync();
        await using var replica = await CreateAppAsync(reloadInterval: TimeSpan.FromMilliseconds(100));
        var featureManager = replica.Services.GetRequiredService<IVariantFeatureManager>();
        Assert.True(await featureManager.IsEnabledAsync("Beta"));

        var response = await app.GetTestClient().PostAsJsonAsync("/config/api/SaveFeatureFlag", new
        {
            Flag = new { Id = "Beta", Enabled = false, RequirementType = "Any", Filters = Array.Empty<object>() },
        });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (await featureManager.IsEnabledAsync("Beta") && DateTime.UtcNow < deadline)
            await Task.Delay(50);

        Assert.False(await featureManager.IsEnabledAsync("Beta"));
    }

    private async Task<WebApplication> CreateAppAsync(TimeSpan? reloadInterval = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["feature_management:feature_flags:0:id"]      = "Beta",
            ["feature_management:feature_flags:0:enabled"] = "true",
        });
        builder.AddConfigWay(x =>
        {
            x.UsePostgreSql(fixture.ConnectionString);
            x.AddUiEditor();
            x.AddFeatureFlags();
            x.ReloadInterval = reloadInterval;
        });
        builder.Services.AddFeatureManagement();

        var app = builder.Build();
        app.UseConfigWay().AllowAnonymous();
        await app.StartAsync();
        return app;
    }
}
