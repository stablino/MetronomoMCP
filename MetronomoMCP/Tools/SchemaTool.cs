using ModelContextProtocol.Server;
using MetronomoMCP.Data.Abstractions;
using System.ComponentModel;
using System.Text;

namespace MetronomoMCP.Tools;

[McpServerToolType]
public sealed class SchemaTool(IQueryRepository repository)
{
    [McpServerTool(Name = "describe_table")]
    [Description("Descrive la struttura (schema) di una tabella specifica: colonne, tipi e chiavi primarie.")]
    public async Task<string> DescribeTableAsync(
        [Description("Nome della tabella di cui descrivere lo schema (es. 'Customers' o 'dbo.Orders').")]
        string tableName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var columns = await repository.GetTableSchemaAsync(tableName, cancellationToken);

            if (columns.Count == 0)
                return $"Tabella '{tableName}' non trovata o priva di colonne.";

            var sb = new StringBuilder();
            sb.AppendLine($"Schema tabella: {tableName}\n");
            sb.AppendLine($"{"Colonna",-30} {"Tipo",-20} {"Nullable",-10} {"MaxLen",-10} PK");
            sb.AppendLine(new string('-', 80));

            foreach (var col in columns)
            {
                sb.AppendLine(
                    $"{col.ColumnName,-30} " +
                    $"{col.DataType,-20} " +
                    $"{(col.IsNullable ? "YES" : "NO"),-10} " +
                    $"{col.MaxLength?.ToString() ?? "-",-10} " +
                    $"{(col.IsPrimaryKey ? "✓" : "")}");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"ERRORE: {ex.Message}";
        }
    }
}
