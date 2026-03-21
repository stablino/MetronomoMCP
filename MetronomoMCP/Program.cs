using MetronomoMCP.Configuration;
using MetronomoMCP.Data;
using MetronomoMCP.Data.Abstractions;
using MetronomoMCP.Resources;
using MetronomoMCP.Startup;
using MetronomoMCP.Tools;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;

// Bootstrap Serilog prima dell'host per catturare errori di avvio
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    //Log.Information("Avvio MetronomoMCP...");

    // Forza la working directory alla directory dell'eseguibile
    // (evita problemi quando Claude Desktop lancia il processo da working directory diversa)
    Directory.SetCurrentDirectory(AppContext.BaseDirectory);

    var builder = Host.CreateApplicationBuilder(args);

    // Serilog
    builder.Services.AddSerilog((services, config) =>
        config.ReadFrom.Configuration(builder.Configuration));

    // Configurazione tipizzata
    builder.Services
        .AddOptions<DatabaseOptions>()
        .BindConfiguration(DatabaseOptions.SectionName)
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services
        .AddOptions<McpOptions>()
        .BindConfiguration(McpOptions.SectionName);

    builder.Services
        .AddOptions<OllamaOptions>()
        .BindConfiguration(OllamaOptions.SectionName);

    builder.Services.AddSingleton<OllamaStartupChecker>();

    // Repository
    builder.Services.AddSingleton<IQueryRepository, SqlQueryRepository>();

    // MCP Server — trasporto stdio (compatibile Claude Desktop e Ollama)
    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithToolsFromAssembly()
        .WithResourcesFromAssembly();

    var app = builder.Build();

    // Verifica Ollama (se abilitato)
    await app.Services.GetRequiredService<OllamaStartupChecker>().CheckAsync();

    // Test connessione database all'avvio
    var dbOptions = app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
    var logger = app.Services.GetRequiredService<ILogger<SqlQueryRepository>>();
    try
    {
        await using var conn = new SqlConnection(dbOptions.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT @@VERSION";
        var version = await cmd.ExecuteScalarAsync();
        logger.LogInformation("Connessione al database riuscita. {Version}", version);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Connessione al database FALLITA: {Message}", ex.Message);
    }

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "MetronomoMCP terminato inaspettatamente.");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

return 0;
