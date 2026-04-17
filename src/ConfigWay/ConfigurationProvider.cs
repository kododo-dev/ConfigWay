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
    private readonly HashSet<string> _keys = BuildKeys(configuration);

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
        Data = Enumerable.Where(entries, e => _keys.Contains(e.Key))
            .ToDictionary(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static HashSet<string> BuildKeys(Configuration configuration)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var options in configuration.Options)
            CollectKeys(options.Key, options.Type, keys);
        return keys;
    }
    
    private static void CollectKeys(string prefix, Type type, HashSet<string> keys)
    {
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                 .Where(p => p.CanRead && p.CanWrite))
        {
            var propKey    = $"{prefix}:{prop.Name}";
            var underlying = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

            if (IsLeaf(underlying))
                keys.Add(propKey);
            else
                CollectKeys(propKey, underlying, keys);
        }
    }

    private static bool IsLeaf(Type t) =>
        t == typeof(string) || t == typeof(bool) || IsNumeric(t) || t.IsEnum;

    private static bool IsNumeric(Type t) =>
        t == typeof(int)     || t == typeof(long)   || t == typeof(short)  ||
        t == typeof(float)   || t == typeof(double) || t == typeof(decimal) ||
        t == typeof(byte)    || t == typeof(uint)   || t == typeof(ulong);
}
