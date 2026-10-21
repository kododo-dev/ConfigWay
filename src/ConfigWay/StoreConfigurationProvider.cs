using Kododo.ConfigWay.Core.Model;

namespace Kododo.ConfigWay;

/// <summary>
/// Base class for configuration providers that expose values kept in the ConfigWay store.
/// Extension packages derive from it and register the provider with
/// <see cref="ConfigurationBuilder.AddProvider"/>; ConfigWay reads the store once and hands every setting to each
/// provider, so a save or a reload updates all of them together.
/// </summary>
/// <remarks>
/// <see cref="Load"/> does nothing: the data is pushed by ConfigWay when it loads or reloads the store.
/// </remarks>
public abstract class StoreConfigurationProvider : Microsoft.Extensions.Configuration.ConfigurationProvider
{
    /// <summary>
    /// Maps the settings read from the store to the configuration keys this provider exposes.
    /// Settings that do not belong to the provider are skipped.
    /// </summary>
    /// <param name="settings">Every setting in the store.</param>
    /// <returns>The configuration keys and values; keys are compared case-insensitively.</returns>
    protected abstract IEnumerable<KeyValuePair<string, string?>> Select(IReadOnlyList<Setting> settings);

    /// <inheritdoc />
    public override void Load()
    {
    }

    internal void Apply(IReadOnlyList<Setting> settings)
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in Select(settings))
            data[key] = value;
        Data = data;
    }

    internal void NotifyReload() => OnReload();
}
