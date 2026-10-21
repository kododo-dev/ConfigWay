using System.Text.Json.Nodes;
using Kododo.ConfigWay.Core.Configuration;
using Microsoft.Extensions.Configuration;

namespace Kododo.ConfigWay.FeatureManagement;

/// <summary>
/// Reads the flags the application has: those defined by its configuration sources (appsettings, environment
/// variables, ...) and those kept in the ConfigWay store, which take their place.
/// </summary>
internal sealed class FlagCatalog(IConfiguration appConfiguration, Configuration configuration)
{
    /// <summary>
    /// The flags of the configuration sources other than ConfigWay, merged by id as feature management merges them:
    /// a later source wins, and within one source the Microsoft schema wins over the .NET schema.
    /// </summary>
    public IReadOnlyDictionary<string, BaseFlag> ReadBase()
    {
        var root = appConfiguration as IConfigurationRoot
            ?? throw new InvalidOperationException("Feature flags require IConfiguration to implement IConfigurationRoot.");

        var flags = new Dictionary<string, BaseFlag>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in root.Providers.Where(p => p is not StoreConfigurationProvider))
        {
            if (ConfigurationJson.Read(provider, "FeatureManagement") is JsonObject dotnetSchema)
            {
                foreach (var (id, node) in dotnetSchema)
                {
                    flags[id] = new BaseFlag(
                        id,
                        FlagJson.FromDotnetSchema(id, node),
                        IsDotnetSchema: true,
                        HasDroppedVariants: FlagJson.HasDotnetVariants(node));
                }
            }

            foreach (var json in ConfigurationJson.Read(provider, "feature_management:feature_flags").Items().OfType<JsonObject>())
            {
                if (json.Get("id").AsText() is { Length: > 0 } id)
                    flags[id] = new BaseFlag(id, json, IsDotnetSchema: false, HasDroppedVariants: false);
            }
        }

        return flags;
    }

    /// <summary>The flags kept in the store, by id.</summary>
    public async Task<IReadOnlyDictionary<string, StoredFlag>> ReadStoredAsync(CancellationToken cancellationToken)
    {
        var settings = await configuration.Store.GetAllAsync(cancellationToken);
        var flags = new Dictionary<string, StoredFlag>(StringComparer.OrdinalIgnoreCase);
        foreach (var flag in StoredFlags.Read(settings))
            flags[flag.Id] = flag;
        return flags;
    }
}

/// <summary>A flag defined by a configuration source of the application.</summary>
/// <param name="Id">The id as the source writes it.</param>
/// <param name="Json">The flag in the Microsoft schema; values are text, as configuration has them.</param>
/// <param name="IsDotnetSchema">Whether the source defines it in the older .NET schema (<c>FeatureManagement</c>).</param>
/// <param name="HasDroppedVariants">Whether it has .NET schema variants, which an override does not keep.</param>
internal sealed record BaseFlag(string Id, JsonObject Json, bool IsDotnetSchema, bool HasDroppedVariants);
