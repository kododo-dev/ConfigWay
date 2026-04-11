using Microsoft.Extensions.Configuration;

namespace Kododo.ConfigWay;

internal sealed class ConfigurationSource(ConfigurationProvider configurationProvider) : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return configurationProvider;
    }
}