using Kododo.Reiho.AspNetCore.API;
using Kododo.Way.Shell;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Kododo.ConfigWay.UI;

/// <summary>
/// Extension methods for mounting the ConfigWay web UI in the ASP.NET Core middleware pipeline.
/// </summary>
public static class EndpointRouteBuilderExtensions
{
    /// <summary>The editor as a Way.Shell module: <c>SPA/dist</c> is built with the wayModule() preset.</summary>
    internal static readonly WayModule Module =
        new("config", "Configuration", typeof(EndpointRouteBuilderExtensions).Assembly, "SPA/dist") { Icon = "settings" };

    extension(IEndpointRouteBuilder endpoints)
    {
        /// <summary>
        /// Mounts the ConfigWay web UI and its API at the specified path, in Way.Shell.
        /// </summary>
        /// <param name="path">
        /// The base path for the ConfigWay UI. Defaults to <c>/config</c>.
        /// The API is available at <c>{path}/api</c> and the UI at <c>{path}</c>.
        /// </param>
        /// <returns>
        /// A <see cref="RouteGroupBuilder"/> that can be used to apply additional
        /// middleware such as authentication or authorization policies.
        /// Outside the Development environment the UI and the API answer <c>403</c> until authorization is required
        /// here, anonymous access is allowed explicitly (<c>AllowAnonymous()</c>) or <c>ShellOptions.AllowAnonymous</c>
        /// is set.
        /// </returns>
        /// <example>
        /// <code>
        /// // Secure the UI with an Admin role requirement
        /// app.UseConfigWay("/config").RequireAuthorization("Admin");
        /// </code>
        /// </example>
        public RouteGroupBuilder UseConfigWay(string path = "/config")
        {
            var configwayEndpoints = endpoints.MapWay(Module, path);
            var api = configwayEndpoints.MapGroup("/api");
            api.MapRequests(typeof(EndpointRouteBuilderExtensions).Assembly);
            foreach (var extension in endpoints.ServiceProvider.GetServices<UiExtension>())
                api.MapRequests(extension.Requests);
            return configwayEndpoints;
        }
    }
}
