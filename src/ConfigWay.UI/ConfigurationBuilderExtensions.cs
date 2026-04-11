using Kododo.Reiho.AspNetCore.API;

namespace Kododo.ConfigWay.UI;

public static class ConfigurationBuilderExtensions
{
    public static ConfigurationBuilder AddUiEditor(this ConfigurationBuilder builder)
    {
        builder.Services.AddRequestHandlers();
        return builder;
    }
}