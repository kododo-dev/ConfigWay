using Kododo.Reiho.AspNetCore.API;

namespace Kododo.ConfigWay.UI;

/// <summary>
/// Extension methods for enabling the ConfigWay embedded web UI.
/// </summary>
public static class ConfigurationBuilderExtensions
{
    /// <summary>
    /// Enables the ConfigWay web UI editor by registering the required API request handlers.
    /// Call <see cref="EndpointRouteBuilderExtensions.UseConfigWay"/> in the middleware
    /// pipeline to mount the UI at a specific path.
    /// </summary>
    /// <param name="builder">The ConfigWay configuration builder.</param>
    /// <returns>The same builder instance for method chaining.</returns>
    public static ConfigurationBuilder AddUiEditor(this ConfigurationBuilder builder)
    {
        builder.Services.AddRequestHandlers();
        return builder;
    }
}
