namespace MetronomoMCP.Configuration;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    public bool Enabled { get; init; }
    public Uri BaseUrl { get; init; } = new("http://localhost:11434");
}
