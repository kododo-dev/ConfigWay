using Kododo.Reiho.AspNetCore.API;
using Kododo.Reiho.AspNetCore.SPA;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kododo.ConfigWay.UI;

public static class EndpointRouteBuilderExtensions
{
    extension(IEndpointRouteBuilder endpoints)
    {
        public RouteGroupBuilder UseConfigWay(string path = "/config")
        {
            var configwayEndpoints = endpoints.MapGroup(path);
            
            configwayEndpoints.MapApi();
            configwayEndpoints.MapEmbeddedSpa();
            
            return configwayEndpoints;
        }

        private void MapApi()
        {
            var api = endpoints.MapGroup("/api");
            api.MapRequests();
        }
    }
}