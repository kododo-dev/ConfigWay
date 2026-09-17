using Kododo.ConfigWay.Core.Model;
using Kododo.ConfigWay.Core.Store;
using Microsoft.Data.SqlClient;

namespace Kododo.ConfigWay.SqlServer;

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

        var sql = $"SELECT [key], [value] FROM {Schema}.{SettingsTable}";

        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(stoppingToken);

        var results = new List<Setting>();

        while (await reader.ReadAsync(stoppingToken))
        {
            var key = reader.GetString(0);
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
        var transaction = (SqlTransaction)await conn.BeginTransactionAsync(stoppingToken);

        try
        {
            const string sqlTemplate = """
                                       MERGE {0}.{1} AS target
                                       USING (SELECT @key AS [key], @value AS [value]) AS source
                                       ON target.[key] = source.[key]
                                       WHEN MATCHED THEN UPDATE SET target.[value] = source.[value]
                                       WHEN NOT MATCHED THEN INSERT ([key], [value]) VALUES (source.[key], source.[value]);
                                       """;
            var sql = string.Format(sqlTemplate, Schema, SettingsTable);

            await using var cmd = new SqlCommand(sql, conn, transaction);
            var keyParam = cmd.Parameters.Add(new SqlParameter("key", System.Data.SqlDbType.NVarChar, 450));
            var valueParam = cmd.Parameters.Add(new SqlParameter("value", System.Data.SqlDbType.NVarChar, -1));
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

        var parameterNames = keys.Select((_, i) => $"@key{i}").ToArray();
        var sql = $"DELETE FROM {Schema}.{SettingsTable} WHERE [key] IN ({string.Join(", ", parameterNames)})";

        await using var cmd = new SqlCommand(sql, conn);
        var i = 0;
        foreach (var key in keys)
            cmd.Parameters.AddWithValue(parameterNames[i++], key);

        await cmd.ExecuteNonQueryAsync(stoppingToken);
    }

    private static async Task EnsureSchemaExistsAsync(SqlConnection conn, CancellationToken ct)
    {
        var sql = $"""
                   IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = '{Schema}')
                       EXEC('CREATE SCHEMA {Schema}');
                   """;

        await using var cmd = new SqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task EnsureTableExistsAsync(SqlConnection conn, CancellationToken ct)
    {
        var sql = $"""
                   IF NOT EXISTS (
                       SELECT 1 FROM sys.tables t
                       JOIN sys.schemas s ON t.schema_id = s.schema_id
                       WHERE s.name = '{Schema}' AND t.name = '{SettingsTable}'
                   )
                   BEGIN
                       CREATE TABLE {Schema}.{SettingsTable} (
                           [key]   NVARCHAR(450)  NOT NULL,
                           [value] NVARCHAR(MAX)  NULL,
                           CONSTRAINT pk_{SettingsTable} PRIMARY KEY ([key])
                       );
                   END
                   """;

        await using var cmd = new SqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken stoppingToken)
    {
        var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(stoppingToken);
        return conn;
    }
}
