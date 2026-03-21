using ModelContextProtocol.Server;
using MetronomoMCP.Data.Abstractions;
using System.ComponentModel;
using System.Text;

namespace MetronomoMCP.Tools;

[McpServerToolType]
public sealed class TableListTool(IQueryRepository repository)
{
    [McpServerTool(Name = "list_tables")]
    [Description("Elenca tutte le tabelle e viste disponibili nel database SQL Server.")]
    public async Task<string> ListTablesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var tables = await repository.GetTablesAsync(cancellationToken);

            if (tables.Count == 0)
                return "Nessuna tabella trovata nel database.";

            var sb = new StringBuilder();
            sb.AppendLine($"Tabelle disponibili ({tables.Count}):\n");

            foreach (var t in tables)
                sb.AppendLine($"  [{t.Schema}].[{t.Name}]  ({t.Type})");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"ERRORE: {ex.Message}";
        }
    }
}
