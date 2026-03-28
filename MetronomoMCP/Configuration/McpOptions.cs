namespace MetronomoMCP.Configuration;

public sealed class McpOptions
{
    public const string SectionName = "Mcp";

    public string ServerName    { get; init; } = "MetronomoMCP";
    public string ServerVersion  { get; init; } = "1.0.0";
    /// <summary>stdio | http</summary>
    public string TransportMode { get; init; } = "stdio";
    public string ContextFilePath         { get; init; } = "context.md";
    public bool   ContextEnabled          { get; init; } = true;
    public string PlanningContextFilePath { get; init; } = "context-planning.md";
    public bool   PlanningContextEnabled  { get; init; } = true;
}
