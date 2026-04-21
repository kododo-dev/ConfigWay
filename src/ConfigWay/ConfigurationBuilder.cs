using Kododo.ConfigWay.Core.Configuration;
using Kododo.ConfigWay.Core.Store;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kododo.ConfigWay;

/// <summary>
/// Fluent builder used to configure ConfigWay during application startup.
/// An instance is passed to the <c>configure</c> delegate of
/// <see cref="HostApplicationBuilderExtensions.AddConfigWay"/>.
/// </summary>
public sealed class ConfigurationBuilder(IHostApplicationBuilder builder)
{
    /// <summary>
    /// Provides access to the application's <see cref="IServiceCollection"/>
    /// so that extension packages (e.g. <c>ConfigWay.UI</c>) can register
    /// their own services.
    /// </summary>
    public IServiceCollection Services => builder.Services;

    /// <summary>
    /// Gets or sets the persistence store used to read and write configuration overrides.
    /// Defaults to <see cref="InMemoryStore"/>.
    /// Replace this with a persistent implementation (e.g. <c>ConfigWay.PostgreSQL</c>)
    /// to retain overrides across application restarts.
    /// </summary>
    public IStore Store { get; set; } = new InMemoryStore();

    internal Dictionary<string, Type> OptionTypes { get; } = [];

    /// <summary>
    /// Registers an options class so that ConfigWay can read, display, and persist
    /// its values through the UI editor.
    /// </summary>
    /// <typeparam name="TOptions">
    /// The options class to register. Must have a public parameterless constructor.
    /// The class is also bound to <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/>
    /// via the standard <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.
    /// </typeparam>
    /// <param name="sectionName">
    /// The configuration section name. When omitted, the class name is used with any
    /// trailing <c>Options</c> suffix stripped (e.g. <c>SmtpOptions</c> → <c>Smtp</c>).
    /// </param>
    /// <returns>The same builder instance for method chaining.</returns>
    public ConfigurationBuilder AddOptions<TOptions>(string? sectionName = null) where TOptions : class, new()
    {
        sectionName ??= StripOptionsSuffix(typeof(TOptions).Name);
        OptionTypes[sectionName] = typeof(TOptions);
        builder.Services.Configure<TOptions>(builder.Configuration.GetSection(sectionName));
        return this;
    }

    internal Configuration Build()
    {
        return new Configuration(
            Store,
            OptionTypes
                .Select(kvp => new Options(kvp.Key, kvp.Value))
                .ToArray());
    }

    private static string StripOptionsSuffix(string name)
    {
        const string suffix = "Options";
        return name.EndsWith(suffix, StringComparison.Ordinal) && name.Length > suffix.Length
            ? name[..^suffix.Length]
            : name;
    }
}
