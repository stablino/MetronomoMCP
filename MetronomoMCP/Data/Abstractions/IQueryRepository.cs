namespace MetronomoMCP.Data.Abstractions;

public interface IQueryRepository
{
    Task<QueryResult> ExecuteQueryAsync(
        string sql,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TableInfo>> GetTablesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ColumnInfo>> GetTableSchemaAsync(
        string tableName,
        CancellationToken cancellationToken = default);
}

public sealed record QueryResult(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows,
    int TotalRows,
    bool WasTruncated);

public sealed record TableInfo(
    string Schema,
    string Name,
    string Type);

public sealed record ColumnInfo(
    string ColumnName,
    string DataType,
    bool IsNullable,
    int? MaxLength,
    bool IsPrimaryKey);
