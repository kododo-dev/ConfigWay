using Kododo.ConfigWay.Core.Configuration;
using Kododo.Reiho.AspNetCore.API;

namespace Kododo.ConfigWay.FeatureManagement.API.ResetFeatureFlag;

internal sealed class ResetFeatureFlagHandler(
    FlagCatalog catalog,
    Configuration configuration,
    IConfigurationEditor configurationEditor) : IRequestHandler<ResetFeatureFlag>
{
    public async Task HandleAsync(ResetFeatureFlag request, CancellationToken cancellationToken)
    {
        var stored = (await catalog.ReadStoredAsync(cancellationToken)).GetValueOrDefault(request.Id)
            ?? throw RequestException.NotFound($"The flag '{request.Id}' has no override.");

        await configuration.Store.DeleteAsync([stored.Key], cancellationToken);
        await configurationEditor.ReloadAllAsync(cancellationToken);
    }
}
