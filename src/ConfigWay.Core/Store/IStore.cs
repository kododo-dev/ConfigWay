using Kododo.ConfigWay.Core.Model;

namespace Kododo.ConfigWay.Core.Store;

/// <summary>
/// Defines the persistence contract for ConfigWay overrides.
/// Implement this interface to use a custom storage backend (e.g. a database, a file, Redis).
/// </summary>
public interface IStore
{
    /// <summary>
    /// Called once at startup before the configuration provider is registered.
    /// Use this to run schema migrations, warm up connections, or perform any
    /// one-time initialisation required by the store.
    /// </summary>
    Task InitializeAsync(CancellationToken stoppingToken = default);

    /// <summary>
    /// Returns all settings currently persisted in the store.
    /// ConfigWay filters the returned collection to keys that belong to registered option types.
    /// </summary>
    Task<IReadOnlyList<Setting>> GetAllAsync(CancellationToken stoppingToken = default);

    /// <summary>
    /// Persists the provided settings. Existing entries for the same keys are overwritten.
    /// A <see cref="Setting"/> with a <see langword="null"/> <see cref="Setting.Value"/>
    /// stores an explicit <see langword="null"/> override.
    /// </summary>
    Task SetAsync(IReadOnlyCollection<Setting> settings, CancellationToken stoppingToken = default);

    /// <summary>
    /// Removes the settings with the specified keys, effectively restoring those
    /// configuration values to whatever the base configuration provides.
    /// Non-existent keys are silently ignored.
    /// </summary>
    Task DeleteAsync(IReadOnlyCollection<string> keys, CancellationToken stoppingToken = default);
}
