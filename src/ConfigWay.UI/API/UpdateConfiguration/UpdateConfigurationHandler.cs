using System.Globalization;
using System.Reflection;
using Kododo.ConfigWay.Core.Configuration;
using Kododo.ConfigWay.Core.Model;
using Kododo.Reiho.AspNetCore.API;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Kododo.ConfigWay.UI.API.UpdateConfiguration;

internal class UpdateConfigurationHandler(
    Configuration configuration,
    IConfigurationEditor configurationEditor,
    IConfiguration appConfiguration,
    IServiceProvider services) : IRequestHandler<UpdateConfiguration, string[]>
{
    public async Task<string[]> HandleAsync(
        UpdateConfiguration request,
        CancellationToken stoppingToken)
    {
        var errors = Validate(request.Settings);
        if (errors.Length > 0)
            return errors;

        var keysToDelete = request.KeysToDelete ?? [];
        if (keysToDelete.Length > 0)
            await configuration.Store.DeleteAsync(keysToDelete, stoppingToken);

        await configuration.Store.SetAsync(request.Settings, stoppingToken);
        await configurationEditor.ReloadAllAsync(stoppingToken);
        return [];
    }

    private string[] Validate(Setting[] incoming)
    {
        var errors = new List<string>();

        var changes = incoming.ToDictionary(
            s => s.Key,
            s => s.Value,
            StringComparer.OrdinalIgnoreCase);

        foreach (var options in configuration.Options)
        {
            var relevant = incoming.Any(s =>
                s.Key.StartsWith(options.Key + ":", StringComparison.OrdinalIgnoreCase));

            if (!relevant)
                continue;

            var instance = Activator.CreateInstance(options.Type)!;
            PopulateFromConfiguration(instance, options.Key, options.Type);

            foreach (var (key, value) in changes)
            {
                if (!key.StartsWith(options.Key + ":", StringComparison.OrdinalIgnoreCase))
                    continue;

                var relativePath = key[(options.Key.Length + 1)..];

                try
                {
                    SetNestedValue(instance, options.Type, relativePath, value);
                }
                catch (Exception ex)
                {
                    errors.Add($"{key}: {ex.Message}");
                }
            }

            var validatorType = typeof(IValidateOptions<>).MakeGenericType(options.Type);
            foreach (var validator in services.GetServices(validatorType))
            {
                var method = validatorType.GetMethod(nameof(IValidateOptions<object>.Validate))!;

                ValidateOptionsResult result;
                try
                {
                    result = (ValidateOptionsResult)method.Invoke(validator, [null, instance])!;
                }
                catch (TargetInvocationException tie)
                {
                    var inner = tie.InnerException ?? tie;
                    errors.Add($"{options.Key}: validator '{validator?.GetType().Name}' threw {inner.GetType().Name}: {inner.Message}");
                    continue;
                }
                catch (Exception ex)
                {
                    errors.Add($"{options.Key}: validator '{validator?.GetType().Name}' threw {ex.GetType().Name}: {ex.Message}");
                    continue;
                }

                if (result.Failed)
                    errors.AddRange(result.Failures);
            }
        }

        return [.. errors];
    }

    private void PopulateFromConfiguration(object instance, string sectionKey, Type type)
    {
        foreach (var prop in GetWritableProperties(type))
        {
            var propKey    = $"{sectionKey}:{prop.Name}";
            var underlying = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

            if (IsLeaf(underlying))
            {
                var raw = appConfiguration[propKey];
                if (raw is not null)
                {
                    try { prop.SetValue(instance, ConvertValue(raw, prop.PropertyType)); }
                    catch { }
                }
            }
            else if (IsArrayOrCollection(underlying))
            {
            }
            else
            {
                var nested = Activator.CreateInstance(underlying)!;
                PopulateFromConfiguration(nested, propKey, underlying);
                prop.SetValue(instance, nested);
            }
        }
    }

    private static void SetNestedValue(object instance, Type type, string relativePath, string? value)
    {
        var segments = relativePath.Split(':');
        var current  = instance;
        var currentType = type;

        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (int.TryParse(segments[i], out _))
                return;

            var prop = GetProperty(currentType, segments[i])
                       ?? throw new InvalidOperationException(
                           $"Property '{segments[i]}' not found on '{currentType.Name}'.");

            var underlying = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

            if (i + 1 < segments.Length && int.TryParse(segments[i + 1], out _))
                return;

            var nested = prop.GetValue(current);
            if (nested is null)
            {
                nested = Activator.CreateInstance(underlying)!;
                prop.SetValue(current, nested);
            }

            current     = nested;
            currentType = underlying;
        }

        if (int.TryParse(segments[^1], out _))
            return;

        var leafProp = GetProperty(currentType, segments[^1])
                       ?? throw new InvalidOperationException(
                           $"Property '{segments[^1]}' not found on '{currentType.Name}'.");

        leafProp.SetValue(current, ConvertValue(value, leafProp.PropertyType));
    }

    private static PropertyInfo? GetProperty(Type type, string name) =>
        type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

    private static IEnumerable<PropertyInfo> GetWritableProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite);

    private static bool IsLeaf(Type t) =>
        t == typeof(string) || t == typeof(bool) || IsNumeric(t) || t.IsEnum;

    private static bool IsNumeric(Type t) =>
        t == typeof(int)     || t == typeof(long)   || t == typeof(short)  ||
        t == typeof(float)   || t == typeof(double) || t == typeof(decimal) ||
        t == typeof(byte)    || t == typeof(uint)   || t == typeof(ulong);

    private static bool IsArrayOrCollection(Type t)
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

    private static object? ConvertValue(string? value, Type targetType)
    {
        if (value is null)
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlying.IsEnum)
            return Enum.Parse(underlying, value, ignoreCase: true);

        return Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
    }
}
