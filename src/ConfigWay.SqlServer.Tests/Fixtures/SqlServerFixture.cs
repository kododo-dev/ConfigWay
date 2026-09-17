using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using Xunit;

namespace Kododo.ConfigWay.SqlServer.Tests.Fixtures;

public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public async Task ResetAsync()
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand("DELETE FROM configway.settings", conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
