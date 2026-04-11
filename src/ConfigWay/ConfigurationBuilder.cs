using Kododo.ConfigWay.Core.Configuration;
using Kododo.ConfigWay.Core.Store;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kododo.ConfigWay;

public sealed class ConfigurationBuilder(IHostApplicationBuilder builder)
{
    public IServiceCollection Services => builder.Services;

    public IStore Store { get; set; } = new InMemoryStore();

    internal Dictionary<string, Type> OptionTypes { get; } = [];

    public ConfigurationBuilder AddOptions<TOptions>(string? sectionName = null) where TOptions : class, new()
    {
        // TrimEnd(char[]) removes individual characters from the set, not a suffix.
        // Use a proper suffix-strip instead.
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
