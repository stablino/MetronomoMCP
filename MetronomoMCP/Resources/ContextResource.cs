using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using MetronomoMCP.Configuration;

namespace MetronomoMCP.Resources;

[McpServerResourceType]
public sealed class ContextResource(
    IOptions<McpOptions> mcpOptions,
    ILogger<ContextResource> logger)
{
    [McpServerResource(
        UriTemplate = "context://business",
        Name = "business-context",
        MimeType = "text/markdown")]
    [Description("Contesto business del database Mecmatica: struttura, moduli, tabelle principali e relazioni.")]
    public async Task<string> GetBusinessContextAsync(CancellationToken cancellationToken = default)
    {
        var options = mcpOptions.Value;

        if (!options.ContextEnabled)
        {
            logger.LogInformation("ContextEnabled è false — context non servito.");
            return string.Empty;
        }

        var path = Path.IsPathRooted(options.ContextFilePath)
            ? options.ContextFilePath
            : Path.Combine(AppContext.BaseDirectory, options.ContextFilePath);

        if (!File.Exists(path))
        {
            logger.LogWarning("File di contesto non trovato: {Path}", path);
            return $"File di contesto non trovato: {path}";
        }

        logger.LogInformation("Lettura file di contesto: {Path}", path);
        return await File.ReadAllTextAsync(path, cancellationToken);
    }
}
