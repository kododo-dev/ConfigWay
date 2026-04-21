namespace Kododo.ConfigWay.PostgreSQL;

/// <summary>
/// Extension methods for using PostgreSQL as the ConfigWay persistence store.
/// </summary>
public static class ConfigurationBuilderExtensions
{
    /// <summary>
    /// Configures ConfigWay to persist overrides in a PostgreSQL database.
    /// The required table is created automatically on first startup via
    /// <see cref="Core.Store.IStore.InitializeAsync"/>.
    /// </summary>
    /// <param name="builder">The ConfigWay configuration builder.</param>
    /// <param name="connectionString">
    /// A valid Npgsql connection string, e.g.
    /// <c>Host=localhost;Database=myapp;Username=postgres;Password=secret</c>.
    /// </param>
    /// <returns>The same builder instance for method chaining.</returns>
    public static ConfigurationBuilder UsePostgreSql(this ConfigurationBuilder builder, string connectionString)
    {
        builder.Store = new Store(connectionString);
        return builder;
    }
}
