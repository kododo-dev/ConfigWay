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
        await using var conn = await OpenAsync(stoppingToken);
        await EnsureSchemaExistsAsync(conn, stoppingToken);
        await EnsureTableExistsAsync(conn, stoppingToken);
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
        if (settings.Count == 0)
            return;

        await using var conn = await OpenAsync(stoppingToken);
        await using var transaction = await conn.BeginTransactionAsync(stoppingToken);

        try
        {
            const string sqlTemplate = """
                                       INSERT INTO {0}.{1} (key, value)
                                       VALUES (@key, @value)
                                       ON CONFLICT (key) DO UPDATE
                                           SET value = EXCLUDED.value;
                                       """;
            var sql = string.Format(sqlTemplate, Schema, SettingsTable);

            await using var cmd = new NpgsqlCommand(sql, conn, transaction);
            var keyParam = cmd.Parameters.Add(new NpgsqlParameter("key", NpgsqlTypes.NpgsqlDbType.Text));
            var valueParam = cmd.Parameters.Add(new NpgsqlParameter("value", NpgsqlTypes.NpgsqlDbType.Text));
            await cmd.PrepareAsync(stoppingToken);

            foreach (var setting in settings)
            {
                keyParam.Value = setting.Key;
                valueParam.Value = (object?)setting.Value ?? DBNull.Value;
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

    public async Task DeleteAsync(IReadOnlyCollection<string> keys, CancellationToken stoppingToken = default)
    {
        if (keys.Count == 0)
            return;

        await using var conn = await OpenAsync(stoppingToken);

        var sql = $"DELETE FROM {Schema}.{SettingsTable} WHERE key = ANY(@keys)";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("keys", keys.ToArray());
        await cmd.ExecuteNonQueryAsync(stoppingToken);
    }

    private static async Task EnsureSchemaExistsAsync(NpgsqlConnection conn, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            $"CREATE SCHEMA IF NOT EXISTS \"{Schema}\"", conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task EnsureTableExistsAsync(NpgsqlConnection conn, CancellationToken ct)
    {
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