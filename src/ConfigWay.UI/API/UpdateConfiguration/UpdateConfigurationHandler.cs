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
                    // Surface the original validator error as a regular validation failure
                    // instead of letting a reflective call crash the request with a 500.
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
                    catch { /* leave default if conversion fails */ }
                }
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
            var prop = GetProperty(currentType, segments[i])
                       ?? throw new InvalidOperationException(
                           $"Property '{segments[i]}' not found on '{currentType.Name}'.");

            var underlying = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            
            var nested = prop.GetValue(current);
            if (nested is null)
            {
                nested = Activator.CreateInstance(underlying)!;
                prop.SetValue(current, nested);
            }

            current     = nested;
            currentType = underlying;
        }

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

    private static bool IsLeaf(Type t) => t == typeof(string);

    private static object? ConvertValue(string? value, Type targetType)
    {
        if (value is null)
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
        return Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
    }
}