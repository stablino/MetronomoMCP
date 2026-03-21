using ModelContextProtocol.Server;
using MetronomoMCP.Data.Abstractions;
using MetronomoMCP.Validation;
using System.ComponentModel;

namespace MetronomoMCP.Tools;

[McpServerToolType]
public sealed class QueryTool(IQueryRepository repository)
{
    [McpServerTool(Name = "execute_query")]
    [Description("Esegue una query SQL in sola lettura sul database SQL Server e restituisce i risultati.")]
    public async Task<string> ExecuteQueryAsync(
        [Description("Query SQL SELECT da eseguire. Solo operazioni di lettura sono permesse.")]
        string sql,
        CancellationToken cancellationToken = default)
    {
        var validation = SqlReadOnlyValidator.Validate(sql);
        if (!validation.IsValid)
            return $"ERRORE VALIDAZIONE: {validation.ErrorMessage}";

        try
        {
            var result = await repository.ExecuteQueryAsync(sql, cancellationToken);
            return FormatResult(result);
        }
        catch (Exception ex)
        {
            return $"ERRORE ESECUZIONE: {ex.Message}";
        }
    }

    private static string FormatResult(QueryResult result)
    {
        if (result.Rows.Count == 0)
            return "Nessun risultato trovato.";

        var sb = new System.Text.StringBuilder();

        // Header
        sb.AppendLine(string.Join(" | ", result.Columns));
        sb.AppendLine(new string('-', result.Columns.Sum(c => c.Length + 3)));

        // Rows
        foreach (var row in result.Rows)
        {
            sb.AppendLine(string.Join(" | ", row.Select(v => v?.ToString() ?? "NULL")));
        }

        sb.AppendLine($"\n[{result.TotalRows} righe restituite" +
                      (result.WasTruncated ? " — RISULTATI TRONCATI al limite configurato]" : "]"));

        return sb.ToString();
    }
}
