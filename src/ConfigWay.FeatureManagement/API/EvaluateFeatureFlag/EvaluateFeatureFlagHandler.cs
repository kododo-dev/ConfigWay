using Kododo.Reiho.AspNetCore.API;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.FeatureFilters;

namespace Kododo.ConfigWay.FeatureManagement.API.EvaluateFeatureFlag;

internal sealed class EvaluateFeatureFlagHandler(FlagCatalog catalog, IServiceProvider services)
    : IRequestHandler<EvaluateFeatureFlag, FlagEvaluation>
{
    public async Task<FlagEvaluation> HandleAsync(EvaluateFeatureFlag request, CancellationToken cancellationToken)
    {
        var featureManager = services.GetService<IVariantFeatureManager>()
            ?? throw RequestException.Conflict(
                "Feature management is not registered: call services.AddFeatureManagement() in the application.");

        if (!catalog.ReadBase().ContainsKey(request.Id) &&
            !(await catalog.ReadStoredAsync(cancellationToken)).ContainsKey(request.Id))
            throw RequestException.NotFound($"The flag '{request.Id}' does not exist.");

        var context = new TargetingContext
        {
            UserId = string.IsNullOrWhiteSpace(request.UserId) ? null : request.UserId.Trim(),
            Groups = (request.Groups ?? []).Where(g => !string.IsNullOrWhiteSpace(g)).Select(g => g.Trim()).ToArray(),
        };

        // The context is passed in, so the contextual targeting filter checks this user instead of the current one.
        var enabled = await featureManager.IsEnabledAsync(request.Id, context, cancellationToken);
        var variant = await featureManager.GetVariantAsync(request.Id, context, cancellationToken);
        return new FlagEvaluation(enabled, variant?.Name);
    }
}
