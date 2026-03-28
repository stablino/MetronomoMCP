using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using MetronomoMCP.Configuration;

namespace MetronomoMCP.Resources;

[McpServerResourceType]
public sealed class ContextPlanningResource(
    IOptions<McpOptions> mcpOptions,
    ILogger<ContextPlanningResource> logger)
{
    [McpServerResource(
        UriTemplate = "context://planning",
        Name = "planning-context",
        MimeType = "text/markdown")]
    [Description("Contesto per la pianificazione della produzione: flusso pianificazione, vincoli, query utili.")]
    public async Task<string> GetPlanningContextAsync(CancellationToken cancellationToken = default)
    {
        var options = mcpOptions.Value;

        if (!options.PlanningContextEnabled)
        {
            logger.LogInformation("PlanningContextEnabled è false — planning context non servito.");
            return string.Empty;
        }

        var path = Path.IsPathRooted(options.PlanningContextFilePath)
            ? options.PlanningContextFilePath
            : Path.Combine(AppContext.BaseDirectory, options.PlanningContextFilePath);

        if (!File.Exists(path))
        {
            logger.LogWarning("File di contesto planning non trovato: {Path}", path);
            return $"File di contesto planning non trovato: {path}";
        }

        logger.LogInformation("Lettura file di contesto planning: {Path}", path);
        return await File.ReadAllTextAsync(path, cancellationToken);
    }
}
