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
    private readonly IConfigurationRoot _baseConfig = BuildBaseConfig(appConfiguration);

    private static IConfigurationRoot BuildBaseConfig(IConfiguration cfg)
    {
        var root = cfg as IConfigurationRoot
            ?? throw new InvalidOperationException(
                $"{nameof(GetConfigurationHandler)} requires IConfiguration to implement IConfigurationRoot.");
        return new ConfigurationRoot(
            root.Providers.Where(p => p is not Kododo.ConfigWay.ConfigurationProvider).ToList());
    }

    public Task<Section[]> HandleAsync(GetConfiguration request, CancellationToken cancellationToken)
    {
        var sections = configuration.Options
            .Select(o =>
            {
                var typeDisplay = o.Type.GetCustomAttribute<DisplayAttribute>();
                return BuildSection(
                    localKey:    o.Key,
                    fullKey:     o.Key,
                    type:        o.Type,
                    displayName: typeDisplay?.GetName() ?? o.Key,
                    description: typeDisplay?.GetDescription());
            })
            .ToArray();
        return Task.FromResult(sections);
    }

    private Section BuildSection(string localKey, string fullKey, Type type, string displayName, string? description)
    {
        var (fields, sections, arrays) = CollectContent(fullKey, type);
        return new Section(
            Key:         localKey,
            Name:        displayName,
            Sections:    sections,
            Fields:      fields,
            Arrays:      arrays,
            Description: description);
    }

    private (Field[] fields, Section[] sections, ArrayField[] arrays) CollectContent(string fullKey, Type type)
    {
        var fields   = new List<Field>();
        var sections = new List<Section>();
        var arrays   = new List<ArrayField>();

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.CanRead && p.CanWrite))
        {
            var propFullKey = $"{fullKey}:{prop.Name}";
            var underlying  = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

            var propDisplay     = prop.GetCustomAttribute<DisplayAttribute>();
            var propName        = propDisplay?.GetName()        ?? prop.Name;
            var propDescription = propDisplay?.GetDescription();

            if (IsLeaf(underlying))
            {
                var isSensitive = IsSensitiveProperty(prop);
                var rawValue    = appConfiguration[propFullKey];
                var rawDefault  = _baseConfig[propFullKey];
                fields.Add(new Field(
                    Key:          prop.Name,
                    Name:         propName,
                    Type:         MapFieldType(underlying),
                    Value:        isSensitive ? (rawValue  is null ? null : "***") : rawValue,
                    DefaultValue: isSensitive ? (rawDefault is null ? null : "***") : rawDefault,
                    IsSensitive:  isSensitive,
                    HasOverride:  rawValue != rawDefault,
                    Description:  propDescription,
                    Options:      GetEnumOptions(underlying)));
            }
            else if (IsArrayType(underlying, out var elementType))
            {
                arrays.Add(BuildArrayField(
                    localKey:    prop.Name,
                    fullKey:     propFullKey,
                    elementType: elementType,
                    displayName: propName,
                    description: propDescription));
            }
            else
            {
                sections.Add(BuildSection(
                    localKey:    prop.Name,
                    fullKey:     propFullKey,
                    type:        underlying,
                    displayName: propName,
                    description: propDescription));
            }
        }

        return (fields.ToArray(), sections.ToArray(), arrays.ToArray());
    }

    private ArrayField BuildArrayField(
        string localKey, string fullKey, Type elementType, string displayName, string? description)
    {
        var underlyingElement = Nullable.GetUnderlyingType(elementType) ?? elementType;
        var isSimple          = IsLeaf(underlyingElement);

        var allIndices = appConfiguration.GetSection(fullKey)
            .GetChildren()
            .Select(c => int.TryParse(c.Key, out var i) ? i : (int?)null)
            .Where(i => i.HasValue)
            .Select(i => i!.Value)
            .OrderBy(i => i)
            .ToArray();

        var baseIndices = new HashSet<int>(
            _baseConfig.GetSection(fullKey)
                .GetChildren()
                .Select(c => int.TryParse(c.Key, out var i) ? i : (int?)null)
                .Where(i => i.HasValue)
                .Select(i => i!.Value));

        var items = allIndices
            .Select(idx => BuildArrayItem(
                index:           idx,
                itemFullPrefix:  $"{fullKey}:{idx}",
                elementType:     underlyingElement,
                isSimple:        isSimple,
                isDeletable:     !baseIndices.Contains(idx)))
            .ToArray();

        var template = BuildArrayTemplate(underlyingElement, isSimple);

        return new ArrayField(
            Key:         localKey,
            Name:        displayName,
            Description: description,
            IsSimple:    isSimple,
            Items:       items,
            Template:    template);
    }

    private ArrayItem BuildArrayItem(
        int index, string itemFullPrefix, Type elementType, bool isSimple, bool isDeletable)
    {
        if (isSimple)
        {
            return new ArrayItem(
                Index:        index,
                IsDeletable:  isDeletable,
                Value:        appConfiguration[itemFullPrefix],
                DefaultValue: _baseConfig[itemFullPrefix],
                Type:         MapFieldType(elementType),
                Options:      GetEnumOptions(elementType),
                Fields:       [],
                Sections:     [],
                Arrays:       []);
        }

        var (fields, sections, arrays) = CollectContent(itemFullPrefix, elementType);
        return new ArrayItem(
            Index:        index,
            IsDeletable:  isDeletable,
            Value:        null,
            DefaultValue: null,
            Type:         null,
            Options:      null,
            Fields:       fields,
            Sections:     sections,
            Arrays:       arrays);
    }

    private ArrayItem BuildArrayTemplate(Type elementType, bool isSimple)
    {
        if (isSimple)
        {
            return new ArrayItem(
                Index:        -1,
                IsDeletable:  true,
                Value:        null,
                DefaultValue: null,
                Type:         MapFieldType(elementType),
                Options:      GetEnumOptions(elementType),
                Fields:       [],
                Sections:     [],
                Arrays:       []);
        }

        var (fields, sections, arrays) = CollectContent("__template", elementType);
        var emptyFields = fields.Select(f => f with { Value = null, DefaultValue = null, HasOverride = false }).ToArray();

        return new ArrayItem(
            Index:        -1,
            IsDeletable:  true,
            Value:        null,
            DefaultValue: null,
            Type:         null,
            Options:      null,
            Fields:       emptyFields,
            Sections:     sections,
            Arrays:       arrays);
    }

    private static bool IsSensitiveProperty(PropertyInfo prop) =>
        prop.GetCustomAttribute<DataTypeAttribute>()?.DataType == DataType.Password;

    private static bool IsLeaf(Type t) =>
        t == typeof(string) || t == typeof(bool) || IsNumeric(t) || t.IsEnum;

    private static bool IsNumeric(Type t) =>
        t == typeof(int)   || t == typeof(long)    || t == typeof(short)   ||
        t == typeof(float) || t == typeof(double)  || t == typeof(decimal) ||
        t == typeof(byte)  || t == typeof(uint)    || t == typeof(ulong);

    private static bool IsArrayType(Type t, out Type elementType)
    {
        if (t.IsArray)
        {
            elementType = t.GetElementType()!;
            return true;
        }

        if (t.IsGenericType)
        {
            var gtd = t.GetGenericTypeDefinition();
            if (gtd == typeof(List<>)                ||
                gtd == typeof(IList<>)               ||
                gtd == typeof(IEnumerable<>)         ||
                gtd == typeof(IReadOnlyList<>)       ||
                gtd == typeof(ICollection<>)         ||
                gtd == typeof(IReadOnlyCollection<>))
            {
                elementType = t.GetGenericArguments()[0];
                return true;
            }
        }

        elementType = null!;
        return false;
    }

    private static FieldType MapFieldType(Type t)
    {
        if (t == typeof(bool)) return FieldType.Bool;
        if (IsNumeric(t))      return FieldType.Number;
        if (t.IsEnum)          return FieldType.Enum;
        return FieldType.String;
    }

    private static EnumOption[]? GetEnumOptions(Type t)
    {
        if (!t.IsEnum) return null;
        return t.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f =>
            {
                var display = f.GetCustomAttribute<DisplayAttribute>();
                return new EnumOption(Value: f.Name, Label: display?.GetName() ?? f.Name);
            })
            .ToArray();
    }
}
