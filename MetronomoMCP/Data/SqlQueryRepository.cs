using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MetronomoMCP.Configuration;
using MetronomoMCP.Data.Abstractions;

namespace MetronomoMCP.Data;

public sealed class SqlQueryRepository(
    IOptions<DatabaseOptions> options,
    ILogger<SqlQueryRepository> logger) : IQueryRepository
{
    private readonly DatabaseOptions _options = options.Value;

    public async Task<QueryResult> ExecuteQueryAsync(
        string sql,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Esecuzione query: {Sql}", sql);

        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection)
        {
            CommandTimeout = _options.CommandTimeoutSeconds,
            CommandType = CommandType.Text
        };

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var columns = Enumerable.Range(0, reader.FieldCount)
            .Select(i => reader.GetName(i))
            .ToList();

        var rows = new List<IReadOnlyList<object?>>();
        var wasTruncated = false;

        while (await reader.ReadAsync(cancellationToken))
        {
            if (rows.Count >= _options.MaxRowsReturned)
            {
                wasTruncated = true;
                break;
            }

            var row = new object?[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++)
                row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);

            rows.Add(row);
        }

        logger.LogInformation(
            "Query completata: {RowCount} righe restituite, troncata: {Truncated}",
            rows.Count, wasTruncated);

        return new QueryResult(columns, rows, rows.Count, wasTruncated);
    }

    public async Task<IReadOnlyList<TableInfo>> GetTablesAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE
            FROM INFORMATION_SCHEMA.TABLES
            ORDER BY TABLE_SCHEMA, TABLE_NAME
            """;

        var result = await ExecuteQueryAsync(sql, cancellationToken);

        return result.Rows
            .Select(r => new TableInfo(
                r[0]?.ToString() ?? "",
                r[1]?.ToString() ?? "",
                r[2]?.ToString() ?? ""))
            .ToList();
    }

    public async Task<IReadOnlyList<ColumnInfo>> GetTableSchemaAsync(
        string tableName,
        CancellationToken cancellationToken = default)
    {
        // Sanitize: accetta solo nomi tabella senza caratteri speciali
        if (!System.Text.RegularExpressions.Regex.IsMatch(tableName, @"^[\w\.\[\]]+$"))
            throw new ArgumentException("Nome tabella non valido.", nameof(tableName));

        var sql = $"""
            SELECT
                c.COLUMN_NAME,
                c.DATA_TYPE,
                c.IS_NULLABLE,
                c.CHARACTER_MAXIMUM_LENGTH,
                CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IS_PK
            FROM INFORMATION_SCHEMA.COLUMNS c
            LEFT JOIN (
                SELECT ku.COLUMN_NAME
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                    ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                  AND ku.TABLE_NAME = '{tableName}'
            ) pk ON c.COLUMN_NAME = pk.COLUMN_NAME
            WHERE c.TABLE_NAME = '{tableName}'
            ORDER BY c.ORDINAL_POSITION
            """;

        var result = await ExecuteQueryAsync(sql, cancellationToken);

        return result.Rows
            .Select(r => new ColumnInfo(
                r[0]?.ToString() ?? "",
                r[1]?.ToString() ?? "",
                r[2]?.ToString() == "YES",
                r[3] as int?,
                r[4]?.ToString() == "1"))
            .ToList();
    }
}
