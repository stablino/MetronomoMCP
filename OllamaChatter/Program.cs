#pragma warning disable CA1303 // stringhe UI hardcoded — nessuna localizzazione richiesta

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

// ── Configurazione ─────────────────────────────────────────────────────────────
var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var ollamaHost   = config["OllamaHost"] ?? "http://localhost:11434";
var mcpCommand   = config["MetronomoMCP:Command"] ?? "dotnet";
var mcpArgs      = config.GetSection("MetronomoMCP:Arguments").Get<string[]>()
                   ?? (string[])["run", "--project", "../MetronomoMCP"];
var systemPrompt = config["SystemPrompt"]
                   ?? "Sei un assistente con accesso a un database SQL Server tramite MetronomoMCP.";

// ── Banner ─────────────────────────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("  ╔══════════════════════════════════════════════╗");
Console.WriteLine("  ║     OllamaChatter + MetronomoMCP tools       ║");
Console.WriteLine("  ╚══════════════════════════════════════════════╝");
Console.WriteLine();

// ── MCP Client → MetronomoMCP ──────────────────────────────────────────────────
Console.Write("  Avvio MetronomoMCP... ");
await using var mcpClient = await McpClient.CreateAsync(
    new StdioClientTransport(
        new StdioClientTransportOptions
        {
            Command         = mcpCommand,
            Arguments       = mcpArgs,
            Name            = "MetronomoMCP",
            WorkingDirectory = Directory.GetCurrentDirectory(),
        },
        NullLoggerFactory.Instance),
    new McpClientOptions
    {
        ClientInfo = new Implementation { Name = "OllamaChatter", Version = "1.0.0" }
    },
    NullLoggerFactory.Instance);
Console.WriteLine("OK");

// ── Tool discovery ─────────────────────────────────────────────────────────────
var mcpTools = await mcpClient.ListToolsAsync();
Console.WriteLine($"  Tool disponibili ({mcpTools.Count}):");
foreach (var t in mcpTools)
    Console.WriteLine($"    • {t.Name} — {t.Description}");

// Converti in formato OpenAI tools
var oaiTools = mcpTools
    .Select(t => new OAITool("function", new OAIFunction(
        t.Name,
        t.Description ?? string.Empty,
        t.ProtocolTool.InputSchema)))
    .ToList();

// ── Ollama ─────────────────────────────────────────────────────────────────────
using var http = new HttpClient { BaseAddress = new Uri(ollamaHost) };
Console.Write($"\n  Connessione a Ollama ({ollamaHost})... ");

List<OllamaModel> ollamaModels;
try
{
    var r = await http.GetAsync(new Uri("/api/tags", UriKind.Relative));
    r.EnsureSuccessStatusCode();
    var tags = await r.Content.ReadFromJsonAsync<OllamaTagsResp>();
    ollamaModels = tags?.Models ?? [];
    Console.WriteLine("OK");
}
catch (HttpRequestException ex)
{
    Console.WriteLine($"ERRORE: {ex.Message}");
    return 1;
}

if (ollamaModels.Count == 0)
{
    Console.WriteLine("  Nessun modello disponibile in Ollama.");
    return 1;
}

// ── Selezione modello ──────────────────────────────────────────────────────────
string selectedModel;
if (ollamaModels.Count == 1)
{
    selectedModel = ollamaModels[0].Name;
    Console.WriteLine($"  Modello: {selectedModel}");
}
else
{
    Console.WriteLine($"\n  Modelli disponibili ({ollamaModels.Count}):");
    for (var i = 0; i < ollamaModels.Count; i++)
        Console.WriteLine($"    [{i + 1}] {ollamaModels[i].Name}  ({FormatSize(ollamaModels[i].Size)})");

    int choice;
    while (true)
    {
        Console.Write("\n  Seleziona modello: ");
        var inp = Console.ReadLine()?.Trim();
        if (int.TryParse(inp, out choice) && choice >= 1 && choice <= ollamaModels.Count) break;
        Console.WriteLine("  Scelta non valida.");
    }
    selectedModel = ollamaModels[choice - 1].Name;
}

// ── REPL ───────────────────────────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine(new string('─', 60));
Console.WriteLine($"  Modello: {selectedModel}  |  Tool MCP: {mcpTools.Count}");
Console.WriteLine("  'clear' = nuova conversazione  |  'exit' = esci");
Console.WriteLine(new string('─', 60));

var messages = new List<OAIMessage> { OAIMessage.FromSystem(systemPrompt) };
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

while (!cts.Token.IsCancellationRequested)
{
    Console.WriteLine();
    Console.Write("Tu: ");
    var userInput = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(userInput)) continue;
    if (userInput.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;
    if (userInput.Equals("clear", StringComparison.OrdinalIgnoreCase))
    {
        messages = [OAIMessage.FromSystem(systemPrompt)];
        Console.WriteLine("  [nuova conversazione]");
        continue;
    }

    messages.Add(OAIMessage.FromUser(userInput));

    try
    {
        // ── Tool-calling loop ───────────────────────────────────────────────────
        while (!cts.Token.IsCancellationRequested)
        {
            var reqBody = new { model = selectedModel, messages, tools = oaiTools, stream = false };
            using var httpResp = await http.PostAsJsonAsync(
                new Uri("/v1/chat/completions", UriKind.Relative), reqBody, cts.Token);
            httpResp.EnsureSuccessStatusCode();

            var completion = await httpResp.Content
                .ReadFromJsonAsync<OAIChatResponse>(cancellationToken: cts.Token);
            var msg = completion?.Choices?.FirstOrDefault()?.Message;
            if (msg is null) break;

            messages.Add(msg);

            // Tool calls → esegui e torna in cima al loop
            if (msg.ToolCalls is { Count: > 0 } toolCalls)
            {
                Console.WriteLine($"\n  [tool calls: {string.Join(", ", toolCalls.Select(tc => tc.Function.Name))}]");

                foreach (var tc in toolCalls)
                {
                    Console.Write($"  ↳ {tc.Function.Name}... ");
                    try
                    {
                        var toolArgs = ParseArgs(tc.Function.Arguments);
                        var result = await mcpClient.CallToolAsync(
                            tc.Function.Name, toolArgs, cancellationToken: cts.Token);

                        var content = ExtractText(result.Content);
                        Console.WriteLine(result.IsError == true ? "ERRORE" : "OK");
                        messages.Add(OAIMessage.FromToolResult(tc.Id, content));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ERRORE: {ex.Message}");
                        messages.Add(OAIMessage.FromToolResult(tc.Id, $"Errore: {ex.Message}"));
                    }
                }
                continue; // richiedi la risposta finale ad Ollama
            }

            // Risposta testuale finale
            Console.WriteLine($"\nOllama: {msg.Content}");
            break;
        }
    }
    catch (OperationCanceledException) { break; }
    catch (Exception ex)
    {
        Console.WriteLine($"\n  [ERRORE] {ex.Message}");
        // rimuove l'ultimo user message per non sporcare la history
        if (messages.Count > 1) messages.RemoveAt(messages.Count - 1);
    }
}

Console.WriteLine("\n  Arrivederci.");
return 0;

// ── Helpers ────────────────────────────────────────────────────────────────────

static IReadOnlyDictionary<string, object?> ParseArgs(string json)
{
    if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, object?>();
    var doc = JsonSerializer.Deserialize<JsonElement>(json);
    return doc.EnumerateObject()
              .ToDictionary(p => p.Name, p => (object?)p.Value);
}

static string ExtractText(IList<ContentBlock> content) =>
    string.Join("\n", content.OfType<TextContentBlock>().Select(c => c.Text));

static string FormatSize(long bytes) => bytes switch
{
    >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} GB",
    >= 1_048_576     => $"{bytes / 1_048_576.0:F1} MB",
    _                => $"{bytes / 1024.0:F1} KB"
};

// ── OpenAI types ───────────────────────────────────────────────────────────────

record OAITool(
    [property: JsonPropertyName("type")]     string Type,
    [property: JsonPropertyName("function")] OAIFunction Function);

record OAIFunction(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("parameters")]  JsonElement Parameters);

record OAIChatResponse(
    [property: JsonPropertyName("choices")] List<OAIChoice>? Choices);

record OAIChoice(
    [property: JsonPropertyName("message")]       OAIMessage Message,
    [property: JsonPropertyName("finish_reason")] string? FinishReason);

record OAIToolCall(
    [property: JsonPropertyName("id")]       string Id,
    [property: JsonPropertyName("type")]     string Type,
    [property: JsonPropertyName("function")] OAIToolCallFunction Function);

record OAIToolCallFunction(
    [property: JsonPropertyName("name")]      string Name,
    [property: JsonPropertyName("arguments")] string Arguments);

// OAIMessage usa class (non record) per supportare JsonIgnore sui singoli field
class OAIMessage
{
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Content { get; init; }

    [JsonPropertyName("tool_call_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolCallId { get; init; }

    [JsonPropertyName("tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<OAIToolCall>? ToolCalls { get; init; }

    public static OAIMessage FromSystem(string content) =>
        new() { Role = "system", Content = content };

    public static OAIMessage FromUser(string content) =>
        new() { Role = "user", Content = content };

    public static OAIMessage FromToolResult(string toolCallId, string content) =>
        new() { Role = "tool", Content = content, ToolCallId = toolCallId };
}

// ── Ollama types ───────────────────────────────────────────────────────────────

record OllamaTagsResp(
    [property: JsonPropertyName("models")] List<OllamaModel> Models);

record OllamaModel(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("size")] long Size);
