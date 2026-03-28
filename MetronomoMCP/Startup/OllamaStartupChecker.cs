using MetronomoMCP.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace MetronomoMCP.Startup;

public sealed class OllamaStartupChecker(
    IOptions<OllamaOptions> options,
    ILogger<OllamaStartupChecker> logger)
{
    private readonly OllamaOptions _options = options.Value;

    public async Task CheckAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return;

        logger.LogInformation("Ollama abilitato — verifica presenza su {BaseUrl}...", _options.BaseUrl);

        using var http = new HttpClient { BaseAddress = _options.BaseUrl };

        try
        {
            var response = await http.GetAsync(new Uri("/api/tags", UriKind.Relative), cancellationToken);
            response.EnsureSuccessStatusCode();

            var tags = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>(cancellationToken: cancellationToken);

            if (tags?.Models is null || tags.Models.Count == 0)
            {
                logger.LogWarning("Ollama raggiungibile ma nessun modello installato.");
                return;
            }

            var psResponse = await http.GetAsync(new Uri("/api/ps", UriKind.Relative), cancellationToken);
            List<OllamaRunningModel> running = [];
            if (psResponse.IsSuccessStatusCode)
            {
                var ps = await psResponse.Content.ReadFromJsonAsync<OllamaPsResponse>(cancellationToken: cancellationToken);
                running = ps?.Models ?? [];
            }

            logger.LogInformation("Ollama raggiungibile. Modelli installati: {Count}", tags.Models.Count);
            foreach (var model in tags.Models)
            {
                var isRunning = running.Exists(r => r.Name == model.Name);
                logger.LogInformation("  • {Name}  [{Size}]  {Status}",
                    model.Name,
                    FormatSize(model.Size),
                    isRunning ? "▶ IN ESECUZIONE" : "○ fermo");
            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning("Ollama non raggiungibile su {BaseUrl}: {Message}", _options.BaseUrl, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Risposta inattesa da Ollama su {BaseUrl}: {Message}", _options.BaseUrl, ex.Message);
        }
    }

    private static string FormatSize(long bytes) =>
        bytes switch
        {
            >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} GB",
            >= 1_048_576     => $"{bytes / 1_048_576.0:F1} MB",
            _                => $"{bytes / 1024.0:F1} KB"
        };
}

// ── JSON models ──────────────────────────────────────────────────────────────

[SuppressMessage("Performance", "CA1812", Justification = "Instantiated by JSON deserializer")]
internal sealed class OllamaTagsResponse
{
    [JsonPropertyName("models")]
    public List<OllamaModel> Models { get; init; } = [];
}

[SuppressMessage("Performance", "CA1812", Justification = "Instantiated by JSON deserializer")]
internal sealed class OllamaModel
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; init; }
}

[SuppressMessage("Performance", "CA1812", Justification = "Instantiated by JSON deserializer")]
internal sealed class OllamaPsResponse
{
    [JsonPropertyName("models")]
    public List<OllamaRunningModel> Models { get; init; } = [];
}

[SuppressMessage("Performance", "CA1812", Justification = "Instantiated by JSON deserializer")]
internal sealed class OllamaRunningModel
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}
