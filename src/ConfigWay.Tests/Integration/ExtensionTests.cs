using System.Net;
using System.Net.Http.Json;
using Kododo.ConfigWay.Core.Model;
using Kododo.ConfigWay.UI;
using Kododo.Reiho.AspNetCore.API;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Kododo.ConfigWay.Tests.Integration;

/// <summary>The extension points used by extension packages: providers, editor extensions and the reload interval.</summary>
public class ExtensionTests
{
    private static async Task<(WebApplication App, HttpClient Client)> StartAsync(Action<ConfigurationBuilder> configure)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.AddConfigWay(x =>
        {
            x.AddOptions<SimpleOptions>("Simple");
            x.AddUiEditor();
            configure(x);
        });
        var app = builder.Build();
        app.UseConfigWay().AllowAnonymous();
        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    [Fact]
    public async Task GetCapabilities_WithoutExtensions_ReturnsNoExtensions()
    {
        var (app, client) = await StartAsync(_ => { });
        await using var _ = app;

        var capabilities = await PostAsync<CapabilitiesBody>(client, "GetCapabilities");

        Assert.Empty(capabilities.Extensions);
    }

    [Fact]
    public async Task GetCapabilities_ReturnsTheExtensionIds()
    {
        var (app, client) = await StartAsync(x => x.AddUiExtension("test", typeof(ExtensionTests).Assembly));
        await using var _ = app;

        var capabilities = await PostAsync<CapabilitiesBody>(client, "GetCapabilities");

        Assert.Equal(["test"], capabilities.Extensions);
    }

    [Fact]
    public async Task ExtensionRequests_AreHandledUnderTheEditorsApi()
    {
        var (app, client) = await StartAsync(x => x.AddUiExtension("test", typeof(ExtensionTests).Assembly));
        await using var _ = app;

        var response = await client.PostAsJsonAsync("/config/api/PingExtension", new { Name = "flags" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("pong flags", await response.Content.ReadFromJsonAsync<string>());
    }

    [Fact]
    public async Task ExtensionRequests_WithoutTheExtension_AreNotFound()
    {
        var (app, client) = await StartAsync(_ => { });
        await using var _ = app;

        var response = await client.PostAsJsonAsync("/config/api/PingExtension", new { Name = "flags" });

        // No API endpoint: the POST falls through to the shell's GET-only routes.
        Assert.False(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Provider_ExposesItsKeysAndIsReloadedWithConfigWay()
    {
        var (app, _) = await StartAsync(x => x.AddProvider(new PrefixProvider("ext/", "Ext:")));
        await using var __ = app;
        var configuration = app.Services.GetRequiredService<IConfiguration>();
        Assert.Null(configuration["Ext:Beta"]);

        var store = app.Services.GetRequiredService<Core.Configuration.Configuration>().Store;
        await store.SetAsync([new Setting("ext/Beta", "on")]);
        await app.Services.GetRequiredService<IConfigurationEditor>().ReloadAllAsync(CancellationToken.None);

        Assert.Equal("on", configuration["Ext:Beta"]);
        Assert.Null(configuration["ext/Beta"]);
    }

    [Fact]
    public async Task Provider_IsExcludedFromTheDefaultsShownInTheEditor()
    {
        var (app, client) = await StartAsync(x => x.AddProvider(new PrefixProvider("ext/", "Simple:")));
        await using var __ = app;

        var store = app.Services.GetRequiredService<Core.Configuration.Configuration>().Store;
        await store.SetAsync([new Setting("ext/Name", "from-extension")]);
        await app.Services.GetRequiredService<IConfigurationEditor>().ReloadAllAsync(CancellationToken.None);

        var json = await (await client.PostAsJsonAsync("/config/api/GetConfiguration", new { }))
            .Content.ReadAsStringAsync();

        Assert.Contains("\"value\":\"from-extension\"", json);
        Assert.DoesNotContain("\"defaultValue\":\"from-extension\"", json);
    }

    [Fact]
    public async Task ReloadInterval_PicksUpChangesMadeByAnotherInstance()
    {
        var (app, _) = await StartAsync(x => x.ReloadInterval = TimeSpan.FromMilliseconds(50));
        await using var __ = app;
        var monitor = app.Services.GetRequiredService<IOptionsMonitor<SimpleOptions>>();
        Assert.Equal(string.Empty, monitor.CurrentValue.Name);

        // Another instance writes to the shared store without reloading this one.
        var store = app.Services.GetRequiredService<Core.Configuration.Configuration>().Store;
        await store.SetAsync([new Setting("Simple:Name", "Bob")]);

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (monitor.CurrentValue.Name != "Bob" && DateTime.UtcNow < deadline)
            await Task.Delay(20);

        Assert.Equal("Bob", monitor.CurrentValue.Name);
    }

    [Fact]
    public async Task WithoutReloadInterval_ChangesMadeByAnotherInstanceAreNotPickedUp()
    {
        var (app, _) = await StartAsync(_ => { });
        await using var __ = app;
        var monitor = app.Services.GetRequiredService<IOptionsMonitor<SimpleOptions>>();

        var store = app.Services.GetRequiredService<Core.Configuration.Configuration>().Store;
        await store.SetAsync([new Setting("Simple:Name", "Bob")]);
        await Task.Delay(200);

        Assert.Equal(string.Empty, monitor.CurrentValue.Name);
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string request)
    {
        var response = await client.PostAsJsonAsync($"/config/api/{request}", new { });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private sealed record CapabilitiesBody(string[] Extensions);
}

/// <summary>A request of the test extension.</summary>
internal record PingExtension(string Name) : IRequest<string>;

internal sealed class PingExtensionHandler : IRequestHandler<PingExtension, string>
{
    public Task<string> HandleAsync(PingExtension request, CancellationToken cancellationToken) =>
        Task.FromResult($"pong {request.Name}");
}

/// <summary>Exposes the settings whose key starts with a store prefix under a configuration prefix.</summary>
file sealed class PrefixProvider(string storePrefix, string configurationPrefix) : StoreConfigurationProvider
{
    protected override IEnumerable<KeyValuePair<string, string?>> Select(IReadOnlyList<Setting> settings) =>
        settings
            .Where(s => s.Key.StartsWith(storePrefix, StringComparison.OrdinalIgnoreCase))
            .Select(s => new KeyValuePair<string, string?>(configurationPrefix + s.Key[storePrefix.Length..], s.Value));
}
