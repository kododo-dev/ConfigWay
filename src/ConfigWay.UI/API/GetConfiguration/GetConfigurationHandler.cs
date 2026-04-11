using System.Reflection;
using Kododo.ConfigWay.Core.Configuration;
using Kododo.ConfigWay.UI.DTO;
using Kododo.Reiho.AspNetCore.API;
using Microsoft.Extensions.Configuration;

namespace Kododo.ConfigWay.UI.API.GetConfiguration;

internal sealed class GetConfigurationHandler(Configuration configuration, IConfiguration appConfiguration) : IRequestHandler<GetConfiguration, Section[]>
{
    public Task<Section[]> HandleAsync(GetConfiguration request, CancellationToken cancellationToken)
    {
        var sections = configuration.Options
            .Select(o => BuildSection(o.Key, o.Key, o.Type))
            .ToArray();
        
        return Task.FromResult(sections);
    }
    
    private Section BuildSection(string key, string name, Type type)
    {
        var fields   = new List<Field>();
        var sections = new List<Section>();

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.CanRead && p.CanWrite))
        {
            var propKey    = $"{key}:{prop.Name}";
            var underlying = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

            if (IsLeaf(underlying))
                fields.Add(new Field(
                    Key:   propKey,
                    Name:  prop.Name,
                    Type:  FieldType.String,
                    Value: appConfiguration[propKey]));
            else
                sections.Add(BuildSection(propKey, prop.Name, underlying));
        }

        return new Section(key, name, sections.ToArray(), fields.ToArray());
    }

    private static bool IsLeaf(Type t) =>
        t == typeof(string);
}