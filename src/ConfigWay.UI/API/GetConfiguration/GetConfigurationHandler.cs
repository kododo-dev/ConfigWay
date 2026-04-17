using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Kododo.ConfigWay.Core.Configuration;
using Kododo.ConfigWay.UI.DTO;
using Kododo.Reiho.AspNetCore.API;
using Microsoft.Extensions.Configuration;

namespace Kododo.ConfigWay.UI.API.GetConfiguration;

internal sealed class GetConfigurationHandler(Configuration configuration, IConfiguration appConfiguration)
    : IRequestHandler<GetConfiguration, Section[]>
{
    public Task<Section[]> HandleAsync(GetConfiguration request, CancellationToken cancellationToken)
    {
        var sections = configuration.Options
            .Select(o =>
            {
                var typeDisplay = o.Type.GetCustomAttribute<DisplayAttribute>();
                return BuildSection(
                    key:         o.Key,
                    type:        o.Type,
                    displayName: typeDisplay?.GetName() ?? o.Key,
                    description: typeDisplay?.GetDescription());
            })
            .ToArray();
        return Task.FromResult(sections);
    }

    private Section BuildSection(string key, Type type, string displayName, string? description)
    {
        var fields   = new List<Field>();
        var sections = new List<Section>();

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.CanRead && p.CanWrite))
        {
            var propKey    = $"{key}:{prop.Name}";
            var underlying = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

            var propDisplay     = prop.GetCustomAttribute<DisplayAttribute>();
            var propName        = propDisplay?.GetName()        ?? prop.Name;
            var propDescription = propDisplay?.GetDescription();

            if (IsLeaf(underlying))
            {
                string? value = appConfiguration[propKey];
                fields.Add(new Field(
                    Key:         propKey,
                    Name:        propName,
                    Type:        MapFieldType(underlying),
                    Value:       value,
                    Description: propDescription,
                    Options:     GetEnumOptions(underlying)));
            }
            else
            {
                sections.Add(BuildSection(propKey, underlying, propName, propDescription));
            }
        }

        return new Section(
            Key:         key,
            Name:        displayName,
            Sections:    sections.ToArray(),
            Fields:      fields.ToArray(),
            Description: description);
    }

    private static bool IsLeaf(Type t) =>
        t == typeof(string) || t == typeof(bool) || IsNumeric(t) || t.IsEnum;

    private static bool IsNumeric(Type t) =>
        t == typeof(int)     || t == typeof(long)   || t == typeof(short)  ||
        t == typeof(float)   || t == typeof(double) || t == typeof(decimal) ||
        t == typeof(byte)    || t == typeof(uint)   || t == typeof(ulong);

    private static FieldType MapFieldType(Type t)
    {
        if (t == typeof(bool))   return FieldType.Bool;
        if (IsNumeric(t))        return FieldType.Number;
        if (t.IsEnum)            return FieldType.Enum;
        return FieldType.String;
    }

    private static EnumOption[]? GetEnumOptions(Type t)
    {
        if (!t.IsEnum) return null;

        return t.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f =>
            {
                var display = f.GetCustomAttribute<DisplayAttribute>();
                var label   = display?.GetName() ?? f.Name;
                return new EnumOption(Value: f.Name, Label: label);
            })
            .ToArray();
    }
}
