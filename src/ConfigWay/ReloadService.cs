using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kododo.ConfigWay;

/// <summary>
/// Checks the store every <see cref="ConfigurationBuilder.ReloadInterval"/> and reloads the configuration when
/// another instance of the application changed it.
/// </summary>
internal sealed class ReloadService(
    ConfigurationProvider configurationProvider,
    TimeSpan interval,
    ILogger<ReloadService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    if (await configurationProvider.ReloadIfChangedAsync(stoppingToken))
                        logger.LogInformation("ConfigWay settings changed in the store; the configuration was reloaded.");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // The store may be unavailable for a while; the current values stay and the next tick tries again.
                    logger.LogWarning(ex, "ConfigWay could not check the store for changes.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
