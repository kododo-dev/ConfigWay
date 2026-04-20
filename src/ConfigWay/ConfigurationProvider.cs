using System.Reflection;
using Kododo.ConfigWay.Core.Configuration;

namespace Kododo.ConfigWay;

public interface IConfigurationEditor
{
    Task ReloadAllAsync(CancellationToken stoppingToken);
}

internal sealed class ConfigurationProvider(Configuration configuration)
    : Microsoft.Extensions.Configuration.ConfigurationProvider, IConfigurationEditor
{
    private readonly HashSet<string> _exactKeys     = BuildExactKeys(configuration);
    private readonly HashSet<string> _arrayPrefixes = BuildArrayPrefixes(configuration);

    public override void Load()
    {
        LoadAsync().GetAwaiter().GetResult();
    }

    async Task IConfigurationEditor.ReloadAllAsync(CancellationToken stoppingToken)
    {
        await LoadAsync(stoppingToken);
        OnReload();
    }

    private async Task LoadAsync(CancellationToken stoppingToken = default)
    {
        var entries = await configuration.Store.GetAllAsync(stoppingToken);
        Data = entries
            .Where(e => IsAllowedKey(e.Key))
            .ToDictionary(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase);
    }

    private bool IsAllowedKey(string key) =>
        _exactKeys.Contains(key) ||
        _arrayPrefixes.Any(p => key.StartsWith(p + ":", StringComparison.OrdinalIgnoreCase));

    private static HashSet<string> BuildExactKeys(Configuration configuration)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var options in configuration.Options)
            CollectExactKeys(options.Key, options.Type, keys);
        return keys;
    }

    private static HashSet<string> BuildArrayPrefixes(Configuration configuration)
    {
        var prefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var options in configuration.Options)
            CollectArrayPrefixes(options.Key, options.Type, prefixes);
        return prefixes;
    }

    private static void CollectExactKeys(string prefix, Type type, HashSet<string> keys)
    {
        foreach (var prop in GetWritableProperties(type))
        {
            var propKey    = $"{prefix}:{prop.Name}";
            var underlying = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

            if (IsLeaf(underlying))
                keys.Add(propKey);
            else if (!IsArrayType(underlying))
                CollectExactKeys(propKey, underlying, keys);
        }
    }

    private static void CollectArrayPrefixes(string prefix, Type type, HashSet<string> prefixes)
    {
        foreach (var prop in GetWritableProperties(type))
        {
            var propKey    = $"{prefix}:{prop.Name}";
            var underlying = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

            if (IsArrayType(underlying))
                prefixes.Add(propKey);
            else if (!IsLeaf(underlying))
                CollectArrayPrefixes(propKey, underlying, prefixes);
        }
    }

    private static bool IsLeaf(Type t) =>
        t == typeof(string) || t == typeof(bool) || IsNumeric(t) || t.IsEnum;

    private static bool IsNumeric(Type t) =>
        t == typeof(int)   || t == typeof(long)    || t == typeof(short)   ||
        t == typeof(float) || t == typeof(double)  || t == typeof(decimal) ||
        t == typeof(byte)  || t == typeof(uint)    || t == typeof(ulong);

    private static bool IsArrayType(Type t)
    {
        if (t.IsArray) return true;
        if (!t.IsGenericType) return false;
        var gtd = t.GetGenericTypeDefinition();
        return gtd == typeof(List<>)                ||
               gtd == typeof(IList<>)               ||
               gtd == typeof(IEnumerable<>)         ||
               gtd == typeof(IReadOnlyList<>)       ||
               gtd == typeof(ICollection<>)         ||
               gtd == typeof(IReadOnlyCollection<>);
    }

    private static IEnumerable<PropertyInfo> GetWritableProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite);
}
