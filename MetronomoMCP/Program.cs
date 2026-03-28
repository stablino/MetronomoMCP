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
    // Forza la working directory alla directory dell'eseguibile
    // (evita problemi quando Claude Desktop lancia il processo da working directory diversa)
    Directory.SetCurrentDirectory(AppContext.BaseDirectory);

    // Leggi il TransportMode prima di costruire l'host, per scegliere il builder corretto
    var prelimConfig = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: false)
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
        .AddCommandLine(args)
        .Build();

    var transportMode = prelimConfig[$"{McpOptions.SectionName}:TransportMode"] ?? "stdio";
    var httpUrl       = prelimConfig[$"{McpOptions.SectionName}:HttpUrl"] ?? "http://localhost:5100";

    IHost app;

    if (transportMode == "stdio")
    {
        // ── Generic host — nessuna porta HTTP aperta, compatibile Claude Desktop ──
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddSerilog((_, cfg) =>
            cfg.ReadFrom.Configuration(builder.Configuration));

        ConfigureCommonServices(builder.Services);

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly()
            .WithResourcesFromAssembly()
            .WithPromptsFromAssembly();

        app = builder.Build();
    }
    else
    {
        // ── WebApplication — HTTP transport per OllamaChatter ─────────────────────
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseUrls(httpUrl);

        builder.Services.AddSerilog((_, cfg) =>
            cfg.ReadFrom.Configuration(builder.Configuration));

        ConfigureCommonServices(builder.Services);

        builder.Services
            .AddMcpServer()
            .WithHttpTransport()
            .WithToolsFromAssembly()
            .WithResourcesFromAssembly()
            .WithPromptsFromAssembly();

        var webApp = builder.Build();

        webApp.MapMcp("/mcp").AllowAnonymous();
        webApp.MapGet("/healthz", () => "MetronomoMCP OK").AllowAnonymous();

        webApp.Lifetime.ApplicationStarted.Register(() =>
        {
            foreach (var url in webApp.Urls)
            {
                Log.Information("MetronomoMCP in ascolto su {Url}/mcp", url);
                Log.Information("Diagnostica disponibile su {Url}/healthz", url);
            }
        });

        app = webApp;
    }

    // ── Avvio comune ──────────────────────────────────────────────────────────────

    // Verifica Ollama (se abilitato)
    await app.Services.GetRequiredService<OllamaStartupChecker>().CheckAsync();

    // Test connessione database all'avvio
    var dbOptions = app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
    var logger    = app.Services.GetRequiredService<ILogger<SqlQueryRepository>>();
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

// ── Registrazione servizi comuni a entrambi i transport ───────────────────────
void ConfigureCommonServices(IServiceCollection services)
{
    services
        .AddOptions<DatabaseOptions>()
        .BindConfiguration(DatabaseOptions.SectionName)
        .ValidateDataAnnotations()
        .ValidateOnStart();

    services
        .AddOptions<McpOptions>()
        .BindConfiguration(McpOptions.SectionName);

    services
        .AddOptions<OllamaOptions>()
        .BindConfiguration(OllamaOptions.SectionName);

    services.AddSingleton<OllamaStartupChecker>();
    services.AddSingleton<IQueryRepository, SqlQueryRepository>();
}
