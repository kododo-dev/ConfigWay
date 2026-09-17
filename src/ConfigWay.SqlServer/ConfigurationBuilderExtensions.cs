namespace Kododo.ConfigWay.SqlServer;

/// <summary>
/// Extension methods for using SQL Server as the ConfigWay persistence store.
/// </summary>
public static class ConfigurationBuilderExtensions
{
    /// <summary>
    /// Configures ConfigWay to persist overrides in a SQL Server database.
    /// The required table is created automatically on first startup via
    /// <see cref="Core.Store.IStore.InitializeAsync"/>.
    /// </summary>
    /// <param name="builder">The ConfigWay configuration builder.</param>
    /// <param name="connectionString">
    /// A valid Microsoft.Data.SqlClient connection string, e.g.
    /// <c>Server=localhost;Database=myapp;User Id=sa;Password=secret;TrustServerCertificate=True</c>.
    /// </param>
    /// <returns>The same builder instance for method chaining.</returns>
    public static ConfigurationBuilder UseSqlServer(this ConfigurationBuilder builder, string connectionString)
    {
        builder.Store = new Store(connectionString);
        return builder;
    }
}
