using System.Collections.Concurrent;
using Kododo.ConfigWay.Core.Model;
using Kododo.ConfigWay.Core.Store;

namespace Kododo.ConfigWay;

internal sealed class InMemoryStore : IStore
{
    private readonly ConcurrentDictionary<string, Setting> _settings = new(StringComparer.OrdinalIgnoreCase);

    public Task InitializeAsync(CancellationToken stoppingToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Setting>> GetAllAsync(CancellationToken stoppingToken = default)
    {
        return Task.FromResult<IReadOnlyList<Setting>>(_settings.Values.ToList());
    }

    public Task SetAsync(IReadOnlyCollection<Setting> settings, CancellationToken stoppingToken = default)
    {
        foreach (var setting in settings)
            _settings[setting.Key] = setting;

        return Task.CompletedTask;
    }

    public Task DeleteAsync(IReadOnlyCollection<string> keys, CancellationToken stoppingToken = default)
    {
        foreach (var key in keys)
            _settings.TryRemove(key, out _);

        return Task.CompletedTask;
    }
}