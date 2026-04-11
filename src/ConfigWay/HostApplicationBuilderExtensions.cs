using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kododo.ConfigWay;

public static class HostApplicationBuilderExtensions
{
    public static void AddConfigWay(this IHostApplicationBuilder builder, Action<ConfigurationBuilder>? configure = null)
    {
        var configurationBuilder = new ConfigurationBuilder(builder);
        configure?.Invoke(configurationBuilder);
        var configuration = configurationBuilder.Build();
        configuration.Store.InitializeAsync().GetAwaiter().GetResult();
        var configurationProvider = new ConfigurationProvider(configuration);
        builder.Services.AddSingleton(configuration);
        builder.Services.AddSingleton(configurationProvider);
        builder.Services.AddSingleton<IConfigurationEditor>(x => configurationProvider);
        builder.Configuration.Add(new ConfigurationSource(configurationProvider));
    }
}