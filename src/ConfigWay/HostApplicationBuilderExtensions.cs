using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kododo.ConfigWay;

/// <summary>
/// Extension methods for registering ConfigWay with the application host.
/// </summary>
public static class HostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers ConfigWay with the application, adding the configuration provider
    /// and making it available to the DI container.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">
    /// Optional delegate for configuring ConfigWay — registering option types,
    /// choosing a persistence store, and enabling extension packages such as
    /// <c>ConfigWay.UI</c> or <c>ConfigWay.PostgreSQL</c>.
    /// </param>
    /// <example>
    /// <code>
    /// builder.AddConfigWay(configWay =>
    /// {
    ///     configWay.UsePostgreSql(connectionString);
    ///     configWay.AddUiEditor();
    ///     configWay.AddOptions&lt;SmtpOptions&gt;();
    ///     configWay.AddOptions&lt;FeatureFlags&gt;();
    /// });
    /// </code>
    /// </example>
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
