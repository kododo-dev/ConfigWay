using DotNet.Testcontainers.Builders;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Kododo.ConfigWay.PostgreSQL.Tests.Fixtures;

/// <summary>
/// Starts a single PostgreSQL container per test class.
/// Call <see cref="ResetAsync"/> inside each test's InitializeAsync to truncate data.
/// </summary>
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>
    /// Removes all rows from the settings table between tests.
    /// Safe to call only after <see cref="Kododo.ConfigWay.PostgreSQL.Store"/> InitializeAsync
    /// has been called at least once (table must exist).
    /// </summary>
    public async Task ResetAsync()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM configway.settings", conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
