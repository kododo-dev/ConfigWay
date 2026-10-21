using Kododo.ConfigWay.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;

namespace Kododo.ConfigWay.FeatureManagement;

/// <summary>
/// Extension methods for managing Microsoft.FeatureManagement feature flags with ConfigWay.
/// </summary>
public static class ConfigurationBuilderExtensions
{
    /// <summary>
    /// Lets ConfigWay manage feature flags: flags saved in the editor are kept in the ConfigWay store and take the
    /// place of the flags with the same id in the application's configuration (<c>feature_management</c> section of
    /// appsettings, environment variables, ...). The editor gets a Flags view.
    /// </summary>
    /// <remarks>
    /// Feature management itself is registered by the application, which also picks the filters and the targeting
    /// context: <c>builder.Services.AddFeatureManagement()</c>. This method sets it up to merge flags from several
    /// configuration sources by id (<see cref="ConfigurationFeatureDefinitionProviderOptions.CustomConfigurationMergingEnabled"/>).
    /// </remarks>
    /// <param name="builder">The ConfigWay configuration builder.</param>
    /// <returns>The same builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// builder.AddConfigWay(x =>
    /// {
    ///     x.UsePostgreSql(connectionString);
    ///     x.AddUiEditor();
    ///     x.AddFeatureFlags();
    /// });
    /// builder.Services.AddFeatureManagement();
    /// </code>
    /// </example>
    public static ConfigurationBuilder AddFeatureFlags(this ConfigurationBuilder builder)
    {
        if (builder.Services.Any(s => s.ServiceType == typeof(FlagCatalog)))
            return builder;

        builder.AddProvider(new FeatureFlagsProvider());
        builder.AddUiExtension("flags", typeof(ConfigurationBuilderExtensions).Assembly);
        builder.Services.AddSingleton<FlagCatalog>();
        builder.Services.AddScoped<RegisteredFilters>();
        builder.Services.Configure<ConfigurationFeatureDefinitionProviderOptions>(
            o => o.CustomConfigurationMergingEnabled = true);
        return builder;
    }
}
