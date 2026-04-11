namespace Kododo.ConfigWay.PostgreSQL;

public static class ConfigurationBuilderExtensions
{
    public static ConfigurationBuilder UsePostgreSql(this ConfigurationBuilder builder, string connectionString)
    {
        builder.Store = new Store(connectionString);
        return builder;
    }
}