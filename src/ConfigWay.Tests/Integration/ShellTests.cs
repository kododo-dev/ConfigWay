using System.Net;
using Kododo.ConfigWay.UI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace Kododo.ConfigWay.Tests.Integration;

/// <summary>The editor runs in Way.Shell: the shell's page, the editor's module, and the shell's protection.</summary>
public class ShellTests
{
    private static async Task<(WebApplication App, HttpClient Client)> StartAsync(Action<RouteGroupBuilder>? decide)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseTestServer();
        builder.AddConfigWay(x =>
        {
            x.AddOptions<SimpleOptions>("Simple");
            x.AddUiEditor();
        });
        var app = builder.Build();
        var group = app.UseConfigWay("/settings");
        decide?.Invoke(group);
        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    [Fact]
    public async Task Page_IsTheShellWithTheEditorsModule()
    {
        var (app, client) = await StartAsync(g => g.AllowAnonymous());
        await using var _ = app;

        var html = await client.GetStringAsync("/settings/");

        Assert.Contains("""<base href="/settings/">""", html);
        Assert.Contains("""<script type="importmap">""", html);
        Assert.Contains("\"moduleUrl\":\"/settings/_way/way.js?v=", html);
        Assert.Contains("\"apiBase\":\"/settings/api\"", html);
        Assert.Contains("<title>Configuration</title>", html);
    }

    [Fact]
    public async Task Module_IsServedFromTheEditorsAssembly()
    {
        var (app, client) = await StartAsync(g => g.AllowAnonymous());
        await using var _ = app;

        var module = await client.GetAsync("/settings/_way/way.js");

        Assert.Equal(HttpStatusCode.OK, module.StatusCode);
        Assert.Contains("contract:2", (await module.Content.ReadAsStringAsync()).Replace(" ", ""));
    }

    [Theory]
    [InlineData("/settings/")]
    [InlineData("/settings/_way/way.js")]
    public async Task WithoutADecisionOnAuthorization_ItIsForbidden_OutsideDevelopment(string url)
    {
        var (app, client) = await StartAsync(decide: null);
        await using var _ = app;

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/settings/api/GetConfiguration", JsonContent())).StatusCode);
    }

    private static StringContent JsonContent() => new("{}", System.Text.Encoding.UTF8, "application/json");
}
