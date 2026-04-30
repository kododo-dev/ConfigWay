using Kododo.Reiho.AspNetCore.API;
using Kododo.Reiho.AspNetCore.SPA;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kododo.ConfigWay.UI;

/// <summary>
/// Extension methods for mounting the ConfigWay web UI in the ASP.NET Core middleware pipeline.
/// </summary>
public static class EndpointRouteBuilderExtensions
{
    extension(IEndpointRouteBuilder endpoints)
    {
        /// <summary>
        /// Mounts the ConfigWay web UI and its API at the specified path.
        /// </summary>
        /// <param name="path">
        /// The base path for the ConfigWay UI. Defaults to <c>/config</c>.
        /// The API is available at <c>{path}/api</c> and the SPA at <c>{path}</c>.
        /// </param>
        /// <returns>
        /// A <see cref="RouteGroupBuilder"/> that can be used to apply additional
        /// middleware such as authentication or authorization policies.
        /// </returns>
        /// <example>
        /// <code>
        /// // Secure the UI with an Admin role requirement
        /// app.UseConfigWay("/config").RequireAuthorization("Admin");
        /// </code>
        /// </example>
        public RouteGroupBuilder UseConfigWay(string path = "/config")
        {
            var configwayEndpoints = endpoints.MapGroup(path);

            configwayEndpoints.MapApi();
            configwayEndpoints.MapEmbeddedSpa(typeof(EndpointRouteBuilderExtensions).Assembly);

            return configwayEndpoints;
        }

        private void MapApi()
        {
            var api = endpoints.MapGroup("/api");
            api.MapRequests(typeof(EndpointRouteBuilderExtensions).Assembly);
        }
    }
}
