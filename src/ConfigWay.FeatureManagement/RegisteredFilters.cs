using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;

namespace Kododo.ConfigWay.FeatureManagement;

/// <summary>The feature filters the application registered, named as feature management names them.</summary>
internal sealed class RegisteredFilters(IServiceProvider services)
{
    /// <summary>Handled by feature management itself, without a registered filter.</summary>
    private static readonly string[] BuiltIn = ["AlwaysOn", "On"];

    /// <summary>Whether the application registered feature management at all.</summary>
    public bool IsFeatureManagementRegistered => services.GetService<IVariantFeatureManager>() is not null;

    /// <summary>
    /// The aliases of the registered filters: the <see cref="FilterAliasAttribute"/>, or the type name without a
    /// <c>Filter</c> suffix; <c>null</c> when they cannot be listed, e.g. a filter misses a dependency.
    /// </summary>
    public string[]? Aliases()
    {
        try
        {
            return services.GetServices<IFeatureFilterMetadata>()
                .Select(f => f.GetType())
                .Select(t => t.GetCustomAttribute<FilterAliasAttribute>()?.Alias ?? TrimSuffix(t.Name, "Filter"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Whether feature management finds a filter for <paramref name="name"/>: the full alias, or for a name without
    /// a namespace, the last segment of an alias (<c>Percentage</c> for <c>Microsoft.Percentage</c>).
    /// </summary>
    public static bool Matches(string name, IEnumerable<string> aliases) =>
        BuiltIn.Contains(name, StringComparer.OrdinalIgnoreCase) ||
        aliases.Any(alias =>
            string.Equals(alias, name, StringComparison.OrdinalIgnoreCase) ||
            (!name.Contains('.') &&
             string.Equals(alias[(alias.LastIndexOf('.') + 1)..], name, StringComparison.OrdinalIgnoreCase)));

    private static string TrimSuffix(string name, string suffix) =>
        name.EndsWith(suffix, StringComparison.Ordinal) && name.Length > suffix.Length ? name[..^suffix.Length] : name;
}
