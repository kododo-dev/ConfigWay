using Kododo.ConfigWay.Core.Model;

namespace Kododo.ConfigWay.Core.Store;

public interface IStore
{
    Task InitializeAsync(CancellationToken stoppingToken = default);
    Task<IReadOnlyList<Setting>> GetAllAsync(CancellationToken stoppingToken = default);
    Task SetAsync(IReadOnlyCollection<Setting> settings, CancellationToken stoppingToken = default);
}
    