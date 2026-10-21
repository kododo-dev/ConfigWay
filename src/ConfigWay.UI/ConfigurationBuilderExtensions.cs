using System.Reflection;
using Kododo.Reiho.AspNetCore.API;
using Kododo.Way.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace Kododo.ConfigWay.UI;

/// <summary>
/// Extension methods for enabling the ConfigWay embedded web UI.
/// </summary>
public static class ConfigurationBuilderExtensions
{
    /// <summary>
    /// Enables the ConfigWay web UI editor by registering the required API request handlers and Way.Shell, which
    /// the UI runs in. Call <see cref="EndpointRouteBuilderExtensions.UseConfigWay"/> in the middleware
    /// pipeline to mount the UI at a specific path; configure the shell (title, links, user) with
    /// <c>services.AddWayShell(o => ...)</c>.
    /// </summary>
    /// <param name="builder">The ConfigWay configuration builder.</param>
    /// <returns>The same builder instance for method chaining.</returns>
    public static ConfigurationBuilder AddUiEditor(this ConfigurationBuilder builder)
    {
        builder.Services.AddRequestHandlers(typeof(ConfigurationBuilderExtensions).Assembly);
        builder.Services.AddWayShell();
        return builder;
    }

    /// <summary>
    /// Adds an extension of the editor, used by extension packages. The API requests of the assembly are handled
    /// under <c>{path}/api</c> next to the editor's own, behind the same authorization, and the extension id is
    /// reported to the UI, which shows the views of the extensions it knows.
    /// </summary>
    /// <param name="builder">The ConfigWay configuration builder.</param>
    /// <param name="id">The extension id known to the UI, e.g. <c>flags</c>.</param>
    /// <param name="requests">The assembly with the extension's API requests and handlers.</param>
    /// <returns>The same builder instance for method chaining.</returns>
    public static ConfigurationBuilder AddUiExtension(this ConfigurationBuilder builder, string id, Assembly requests)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(requests);

        builder.Services.AddRequestHandlers(requests);
        builder.Services.AddSingleton(new UiExtension(id, requests));
        return builder;
    }
}

internal sealed record UiExtension(string Id, Assembly Requests);
