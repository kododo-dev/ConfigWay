using Kododo.Reiho.AspNetCore.API;

namespace Kododo.ConfigWay.UI.API.GetCapabilities;

internal sealed class GetCapabilitiesHandler(IEnumerable<UiExtension> extensions)
    : IRequestHandler<GetCapabilities, Capabilities>
{
    public Task<Capabilities> HandleAsync(GetCapabilities request, CancellationToken cancellationToken) =>
        Task.FromResult(new Capabilities(extensions.Select(e => e.Id).Distinct().ToArray()));
}
