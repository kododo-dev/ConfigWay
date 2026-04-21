using Kododo.ConfigWay.Core.Configuration;

namespace Kododo.ConfigWay;

/// <summary>
/// Exposes a method to trigger a live reload of all ConfigWay-managed configuration values.
/// This is typically used by the UI editor after saving changes, so that the running
/// application picks them up immediately without a restart.
/// </summary>
/// <remarks>
/// The implementation is registered as a singleton in the DI container by
/// <see cref="HostApplicationBuilderExtensions.AddConfigWay"/>.
/// Consumers who need to trigger a reload programmatically can resolve this interface
/// from the service provider.
/// </remarks>
public interface IConfigurationEditor
{
    /// <summary>
    /// Reloads all ConfigWay overrides from the store and notifies
    /// <see cref="Microsoft.Extensions.Options.IOptionsMonitor{TOptions}"/> subscribers.
    /// </summary>
    Task ReloadAllAsync(CancellationToken stoppingToken);
}

internal sealed class ConfigurationProvider(Configuration configuration)
    : Microsoft.Extensions.Configuration.ConfigurationProvider, IConfigurationEditor
{
    private readonly HashSet<string> _exactKeys     = BuildExactKeys(configuration);
    private readonly HashSet<string> _arrayPrefixes = BuildArrayPrefixes(configuration);

    public override void Load()
    {
        LoadAsync().GetAwaiter().GetResult();
    }

    async Task IConfigurationEditor.ReloadAllAsync(CancellationToken stoppingToken)
    {
        await LoadAsync(stoppingToken);
        OnReload();
    }

    private async Task LoadAsync(CancellationToken stoppingToken = default)
    {
        var entries = await configuration.Store.GetAllAsync(stoppingToken);
        Data = entries
            .Where(e => IsAllowedKey(e.Key))
            .ToDictionary(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase);
    }

    private bool IsAllowedKey(string key) =>
        _exactKeys.Contains(key) ||
        _arrayPrefixes.Any(p => key.StartsWith(p + ":", StringComparison.OrdinalIgnoreCase));

    private static HashSet<string> BuildExactKeys(Configuration configuration)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var options in configuration.Options)
            CollectExactKeys(options.Key, options.Type, keys);
        return keys;
    }

    private static HashSet<string> BuildArrayPrefixes(Configuration configuration)
    {
        var prefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var options in configuration.Options)
            CollectArrayPrefixes(options.Key, options.Type, prefixes);
        return prefixes;
    }

    private static void CollectExactKeys(string prefix, Type type, HashSet<string> keys)
    {
        foreach (var prop in TypeHelpers.GetWritableProperties(type))
        {
            var propKey    = $"{prefix}:{prop.Name}";
            var underlying = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

            if (TypeHelpers.IsLeaf(underlying))
                keys.Add(propKey);
            else if (!TypeHelpers.IsArrayOrCollection(underlying))
                CollectExactKeys(propKey, underlying, keys);
        }
    }

    private static void CollectArrayPrefixes(string prefix, Type type, HashSet<string> prefixes)
    {
        foreach (var prop in TypeHelpers.GetWritableProperties(type))
        {
            var propKey    = $"{prefix}:{prop.Name}";
            var underlying = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

            if (TypeHelpers.IsArrayOrCollection(underlying))
                prefixes.Add(propKey);
            else if (!TypeHelpers.IsLeaf(underlying))
                CollectArrayPrefixes(propKey, underlying, prefixes);
        }
    }
}
