using System.ComponentModel;
using MetronomoMCP.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace MetronomoMCP.Prompts;

public sealed class ContextPrompt(
    IOptions<McpOptions> mcpOptions,
    ILogger<ContextPrompt> logger)
{
    [McpServerPrompt(Name = "carica-contesti")]
    [Description("Carica il contesto business e di pianificazione nel contesto della conversazione.")]
    public async Task<IEnumerable<ChatMessage>> GetContextsAsync(CancellationToken cancellationToken = default)
    {
        var options = mcpOptions.Value;
        var parts   = new List<string>();

        await AppendFileAsync(options.ContextFilePath,         options.ContextEnabled,         "business",    parts, cancellationToken);
        await AppendFileAsync(options.PlanningContextFilePath, options.PlanningContextEnabled, "pianificazione", parts, cancellationToken);

        if (parts.Count == 0)
            return [];

        var content = string.Join("\n\n---\n\n", parts);
        return [new ChatMessage(ChatRole.User, content)];
    }

    private async Task AppendFileAsync(
        string filePath, bool enabled, string label,
        List<string> parts, CancellationToken cancellationToken)
    {
        if (!enabled)
        {
            logger.LogInformation("Contesto {Label} disabilitato — saltato.", label);
            return;
        }

        var path = Path.IsPathRooted(filePath)
            ? filePath
            : Path.Combine(AppContext.BaseDirectory, filePath);

        if (!File.Exists(path))
        {
            logger.LogWarning("File contesto {Label} non trovato: {Path}", label, path);
            return;
        }

        logger.LogInformation("Lettura contesto {Label}: {Path}", label, path);
        var text = await File.ReadAllTextAsync(path, cancellationToken);
        if (!string.IsNullOrWhiteSpace(text))
            parts.Add(text);
    }
}
