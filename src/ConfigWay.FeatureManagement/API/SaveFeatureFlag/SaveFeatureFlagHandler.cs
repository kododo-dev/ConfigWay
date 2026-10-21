using Kododo.ConfigWay.Core.Configuration;
using Kododo.ConfigWay.Core.Model;
using Kododo.Reiho.AspNetCore.API;

namespace Kododo.ConfigWay.FeatureManagement.API.SaveFeatureFlag;

internal sealed class SaveFeatureFlagHandler(
    FlagCatalog catalog,
    RegisteredFilters filters,
    Configuration configuration,
    IConfigurationEditor configurationEditor) : IRequestHandler<SaveFeatureFlag>
{
    public async Task HandleAsync(SaveFeatureFlag request, CancellationToken cancellationToken)
    {
        CheckFilters(request.Flag);

        var configured = catalog.ReadBase().GetValueOrDefault(request.Flag.Id);
        var stored = (await catalog.ReadStoredAsync(cancellationToken)).GetValueOrDefault(request.Flag.Id);

        if (request.IsNew && (configured is not null || stored is not null))
            throw RequestException.Invalid("flag.id", $"A flag named '{request.Flag.Id}' already exists.");
        if (!request.IsNew && configured is null && stored is null)
            throw RequestException.NotFound($"The flag '{request.Flag.Id}' does not exist.");

        // Ids are compared ignoring case: an existing flag keeps the id it has.
        var id = stored?.Id ?? configured?.Id ?? request.Flag.Id;
        var json = FlagJson.Apply(request.Flag with { Id = id }, stored?.Json ?? configured?.Json);

        await configuration.Store.SetAsync(
            [new Setting(stored?.Key ?? StoredFlags.KeyFor(id), json.ToJsonString())],
            cancellationToken);
        await configurationEditor.ReloadAllAsync(cancellationToken);
    }

    /// <summary>
    /// Turns down filters the application has not registered: feature management throws whenever it checks a flag
    /// with such a filter, so a typo would break every page that checks the flag.
    /// </summary>
    private void CheckFilters(Model.FeatureFlag flag)
    {
        if (!filters.IsFeatureManagementRegistered || filters.Aliases() is not { } aliases)
            return;

        var errors = new Dictionary<string, string[]>();
        for (var i = 0; i < flag.Filters.Length; i++)
        {
            var name = flag.Filters[i].Name;
            if (!RegisteredFilters.Matches(name, aliases))
                errors[$"flag.filters[{i}].name"] =
                    [$"The application has not registered a filter named '{name}'; checking the flag would fail."];
        }

        if (errors.Count > 0)
            throw new RequestException("The flag uses filters the application has not registered.") { Errors = errors };
    }
}
