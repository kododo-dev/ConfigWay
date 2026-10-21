using Kododo.ConfigWay.Core.Configuration;
using Kododo.ConfigWay.Core.Model;

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
    /// Reloads all ConfigWay overrides from the store, including the providers added by extension packages,
    /// and notifies <see cref="Microsoft.Extensions.Options.IOptionsMonitor{TOptions}"/> subscribers.
    /// </summary>
    Task ReloadAllAsync(CancellationToken stoppingToken);
}

internal sealed class ConfigurationProvider(
    Configuration configuration,
    IReadOnlyList<StoreConfigurationProvider>? extensions = null)
    : StoreConfigurationProvider, IConfigurationEditor
{
    private readonly HashSet<string> _exactKeys     = BuildExactKeys(configuration);
    private readonly HashSet<string> _arrayPrefixes = BuildArrayPrefixes(configuration);
    private readonly IReadOnlyList<StoreConfigurationProvider> _extensions = extensions ?? [];
    private readonly SemaphoreSlim _reloadLock = new(1, 1);
    private Dictionary<string, string?> _loaded = new(StringComparer.Ordinal);

    public override void Load()
    {
        var settings = configuration.Store.GetAllAsync().GetAwaiter().GetResult();
        Apply(settings, notify: false);
    }

    async Task IConfigurationEditor.ReloadAllAsync(CancellationToken stoppingToken)
    {
        await _reloadLock.WaitAsync(stoppingToken);
        try
        {
            var settings = await configuration.Store.GetAllAsync(stoppingToken);
            Apply(settings, notify: true);
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    /// <summary>
    /// Reads the store and reloads the providers only when a setting was added, changed or removed since the last
    /// load, e.g. by another instance of the application.
    /// </summary>
    /// <returns><c>true</c> when the values changed and the providers were reloaded.</returns>
    internal async Task<bool> ReloadIfChangedAsync(CancellationToken stoppingToken)
    {
        await _reloadLock.WaitAsync(stoppingToken);
        try
        {
            var settings = await configuration.Store.GetAllAsync(stoppingToken);
            if (!HasChanged(settings))
                return false;

            Apply(settings, notify: true);
            return true;
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    protected override IEnumerable<KeyValuePair<string, string?>> Select(IReadOnlyList<Setting> settings) =>
        settings
            .Where(s => IsAllowedKey(s.Key))
            .Select(s => new KeyValuePair<string, string?>(s.Key, s.Value));

    private void Apply(IReadOnlyList<Setting> settings, bool notify)
    {
        _loaded = Snapshot(settings);

        Apply(settings);
        foreach (var extension in _extensions)
            extension.Apply(settings);

        if (!notify)
            return;

        NotifyReload();
        foreach (var extension in _extensions)
            extension.NotifyReload();
    }

    private bool HasChanged(IReadOnlyList<Setting> settings)
    {
        var current = Snapshot(settings);
        return current.Count != _loaded.Count ||
               current.Any(s => !_loaded.TryGetValue(s.Key, out var value) || value != s.Value);
    }

    private static Dictionary<string, string?> Snapshot(IReadOnlyList<Setting> settings)
    {
        var snapshot = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var setting in settings)
            snapshot[setting.Key] = setting.Value;
        return snapshot;
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
