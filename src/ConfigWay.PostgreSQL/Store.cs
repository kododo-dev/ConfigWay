using Kododo.ConfigWay.Core.Model;
using Kododo.ConfigWay.Core.Store;
using Npgsql;

namespace Kododo.ConfigWay.PostgreSQL;

internal sealed class Store(string connectionString) : IStore
{
    private const string Schema = "configway";
    private const string SettingsTable = "settings";
    
    public async Task InitializeAsync(CancellationToken stoppingToken = default)
    {
        await EnsureSchemaExistsAsync(stoppingToken);
        await EnsureTableExistsAsync(stoppingToken);
    }

    public async Task<IReadOnlyList<Setting>> GetAllAsync(CancellationToken stoppingToken = default)
    {
        await using var conn = await OpenAsync(stoppingToken);
 
        var sql = $"SELECT key, value FROM {Schema}.{SettingsTable}";
 
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(stoppingToken);
 
        var results = new List<Setting>();
 
        while (await reader.ReadAsync(stoppingToken))
        {
            var key= reader.GetString(0);
            var value = reader.IsDBNull(1) ? null : reader.GetString(1);
            results.Add(new Setting(key, value));
        }
 
        return results;
    }

    public async Task SetAsync(IReadOnlyCollection<Setting> settings, CancellationToken stoppingToken = default)
    {
        await using var conn = await OpenAsync(stoppingToken);
        await using var transaction = await conn.BeginTransactionAsync(stoppingToken);
 
        try
        {
            var sql = $"""
                       INSERT INTO {Schema}.{SettingsTable} (key, value)
                       VALUES (@key, @value)
                       ON CONFLICT (key) DO UPDATE
                           SET value      = EXCLUDED.value;
                       """;
 
            foreach (var setting in settings)
            {
                await using var cmd = new NpgsqlCommand(sql, conn, transaction);
                cmd.Parameters.AddWithValue("key", setting.Key);
                cmd.Parameters.Add(new NpgsqlParameter("value", NpgsqlTypes.NpgsqlDbType.Text)
                {
                    Value = (object?)setting.Value ?? DBNull.Value,
                });
 
                await cmd.ExecuteNonQueryAsync(stoppingToken);
            }
 
            await transaction.CommitAsync(stoppingToken);
        }
        catch
        {
            await transaction.RollbackAsync(stoppingToken);
            throw;
        }
    }

    private async Task EnsureSchemaExistsAsync(CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            $"CREATE SCHEMA IF NOT EXISTS \"{Schema}\"", conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }
 
    private async Task EnsureTableExistsAsync(CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct);
 
        var sql = $"""
                   CREATE TABLE IF NOT EXISTS {Schema}.{SettingsTable} (
                       key        TEXT        NOT NULL,
                       value      TEXT        NULL,
                       CONSTRAINT pk_{SettingsTable} PRIMARY KEY (key)
                   );
                   """;
 
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }
    
    private async Task<NpgsqlConnection> OpenAsync(CancellationToken stoppingToken)
    {
        var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(stoppingToken);
        return conn;
    }
}