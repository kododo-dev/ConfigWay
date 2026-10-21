using Kododo.ConfigWay.FeatureManagement.Model;
using Kododo.Reiho.AspNetCore.API;

namespace Kododo.ConfigWay.FeatureManagement.API.GetFeatureFlags;

internal sealed class GetFeatureFlagsHandler(FlagCatalog catalog, RegisteredFilters filters)
    : IRequestHandler<GetFeatureFlags, FeatureFlags>
{
    public async Task<FeatureFlags> HandleAsync(GetFeatureFlags request, CancellationToken cancellationToken)
    {
        var configured = catalog.ReadBase();
        var stored = await catalog.ReadStoredAsync(cancellationToken);

        var items = configured.Keys
            .Concat(stored.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(id => ToItem(configured.GetValueOrDefault(id), stored.GetValueOrDefault(id)))
            .OrderBy(i => i.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new FeatureFlags(filters.IsFeatureManagementRegistered, filters.Aliases() ?? [], items);
    }

    private static FeatureFlagItem ToItem(BaseFlag? configured, StoredFlag? stored)
    {
        var configuredFlag = configured is null ? null : FlagJson.ToFlag(configured.Id, configured.Json);
        var json = stored?.Json ?? configured?.Json;
        var flag = stored?.Json is { } storedJson
            ? FlagJson.ToFlag(stored.Id, storedJson)
            : configuredFlag ?? new FeatureFlag(stored!.Id, false, Model.RequirementType.Any, []);

        return new FeatureFlagItem(
            flag.Id,
            flag,
            configuredFlag,
            stored is not null,
            configured?.IsDotnetSchema ?? false,
            json is not null && FlagJson.HasVariants(json),
            stored?.Error);
    }
}
