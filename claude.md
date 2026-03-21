# MetronomoMCP — Project Specification

## Panoramica

**MetronomoMCP** è un server MCP (Model Context Protocol) implementato come applicazione console in **C# su .NET 10**, progettato per esporre capacità di ricerca su un database **SQL Server** a LLM come Claude (Anthropic) e, in futuro, Ollama e altri modelli compatibili con MCP.

Il progetto garantisce **esclusivamente operazioni di lettura** sul database, prevenendo qualsiasi modifica ai dati.

---

## Obiettivi

- Esporre un MCP server accessibile da Claude Desktop, Claude Code e futuri client Ollama
- Consentire query SQL complesse in sola lettura su un database SQL Server
- Seguire le best practice .NET 10 per manutenibilità, testabilità e sicurezza
- Configurazione semplice (nessuna segretazione in questa fase)

---

## Stack Tecnologico

| Componente         | Scelta                                      |
|--------------------|---------------------------------------------|
| Runtime            | .NET 10 (Console App)                       |
| Linguaggio         | C# 13                                       |
| Protocollo         | Model Context Protocol (MCP) via stdio/SSE  |
| Database           | SQL Server (Microsoft.Data.SqlClient)        |
| MCP SDK            | `ModelContextProtocol` (NuGet ufficiale)    |
| Logging            | `Microsoft.Extensions.Logging` + Serilog    |
| Configurazione     | `appsettings.json` + `IOptions<T>`          |
| DI Container       | `Microsoft.Extensions.DependencyInjection`  |
| Testing            | xUnit + Moq + FluentAssertions              |

---

## Struttura del Progetto

```
MetronomoMCP/
├── src/
│   └── MetronomoMCP/
│       ├── MetronomoMCP.csproj
│       ├── Program.cs
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       │
│       ├── Configuration/
│       │   └── DatabaseOptions.cs
│       │
│       ├── Data/
│       │   ├── Abstractions/
│       │   │   └── IQueryRepository.cs
│       │   └── SqlQueryRepository.cs
│       │
│       ├── Tools/
│       │   ├── QueryTool.cs
│       │   ├── SchemaTool.cs
│       │   └── TableListTool.cs
│       │
│       └── Validation/
│           └── SqlReadOnlyValidator.cs
│
└── tests/
    └── MetronomoMCP.Tests/
        ├── MetronomoMCP.Tests.csproj
        ├── Tools/
        │   ├── QueryToolTests.cs
        │   └── SchemaToolTests.cs
        └── Validation/
            └── SqlReadOnlyValidatorTests.cs
```

---

## File Principali

### `MetronomoMCP.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>13</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisMode>All</AnalysisMode>
    <AssemblyName>MetronomoMCP</AssemblyName>
    <RootNamespace>MetronomoMCP</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="ModelContextProtocol" Version="*" />
    <PackageReference Include="Microsoft.Data.SqlClient" Version="*" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="*" />
    <PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" Version="*" />
    <PackageReference Include="Serilog.Extensions.Hosting" Version="*" />
    <PackageReference Include="Serilog.Sinks.Console" Version="*" />
    <PackageReference Include="Serilog.Sinks.File" Version="*" />
  </ItemGroup>
</Project>
```

---

### `appsettings.json`

```json
{
  "Database": {
    "ConnectionString": "Server=localhost;Database=MyDatabase;User Id=sa;Password=YourPassword;TrustServerCertificate=True;",
    "CommandTimeoutSeconds": 30,
    "MaxRowsReturned": 1000
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": {
          "path": "logs/metronomo-.log",
          "rollingInterval": "Day"
        }
      }
    ]
  },
  "Mcp": {
    "ServerName": "MetronomoMCP",
    "ServerVersion": "1.0.0",
    "TransportMode": "stdio"
  }
}
```

---

### `Configuration/DatabaseOptions.cs`

```csharp
namespace MetronomoMCP.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string ConnectionString { get; init; } = string.Empty;
    public int CommandTimeoutSeconds { get; init; } = 30;
    public int MaxRowsReturned { get; init; } = 1000;
}
```

---

### `Validation/SqlReadOnlyValidator.cs`

```csharp
namespace MetronomoMCP.Validation;

/// <summary>
/// Validates that a SQL query contains only read-only statements.
/// Prevents INSERT, UPDATE, DELETE, DROP, TRUNCATE, EXEC, etc.
/// </summary>
public static class SqlReadOnlyValidator
{
    private static readonly HashSet<string> ForbiddenKeywords =
    [
        "INSERT", "UPDATE", "DELETE", "DROP", "TRUNCATE",
        "ALTER", "CREATE", "EXEC", "EXECUTE", "MERGE",
        "GRANT", "REVOKE", "DENY", "BULK"
    ];

    public static ValidationResult Validate(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return ValidationResult.Fail("La query SQL non può essere vuota.");

        var upperSql = sql.ToUpperInvariant();

        foreach (var keyword in ForbiddenKeywords)
        {
            // Match keyword as a whole word (not substring of identifiers)
            if (System.Text.RegularExpressions.Regex.IsMatch(
                    upperSql, $@"\b{keyword}\b"))
            {
                return ValidationResult.Fail(
                    $"La query contiene il comando non consentito: '{keyword}'. " +
                    "Solo operazioni di lettura (SELECT) sono permesse.");
            }
        }

        return ValidationResult.Ok();
    }
}

public sealed record ValidationResult(bool IsValid, string? ErrorMessage)
{
    public static ValidationResult Ok() => new(true, null);
    public static ValidationResult Fail(string message) => new(false, message);
}
```

---

### `Data/Abstractions/IQueryRepository.cs`

```csharp
namespace MetronomoMCP.Data.Abstractions;

public interface IQueryRepository
{
    Task<QueryResult> ExecuteQueryAsync(
        string sql,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TableInfo>> GetTablesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ColumnInfo>> GetTableSchemaAsync(
        string tableName,
        CancellationToken cancellationToken = default);
}

public sealed record QueryResult(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows,
    int TotalRows,
    bool WasTruncated);

public sealed record TableInfo(
    string Schema,
    string Name,
    string Type);

public sealed record ColumnInfo(
    string ColumnName,
    string DataType,
    bool IsNullable,
    int? MaxLength,
    bool IsPrimaryKey);
```

---

### `Data/SqlQueryRepository.cs`

```csharp
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MetronomoMCP.Configuration;
using MetronomoMCP.Data.Abstractions;

namespace MetronomoMCP.Data;

public sealed class SqlQueryRepository(
    IOptions<DatabaseOptions> options,
    ILogger<SqlQueryRepository> logger) : IQueryRepository
{
    private readonly DatabaseOptions _options = options.Value;

    public async Task<QueryResult> ExecuteQueryAsync(
        string sql,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Esecuzione query: {Sql}", sql);

        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection)
        {
            CommandTimeout = _options.CommandTimeoutSeconds,
            CommandType = CommandType.Text
        };

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var columns = Enumerable.Range(0, reader.FieldCount)
            .Select(i => reader.GetName(i))
            .ToList();

        var rows = new List<IReadOnlyList<object?>>();
        var wasTruncated = false;

        while (await reader.ReadAsync(cancellationToken))
        {
            if (rows.Count >= _options.MaxRowsReturned)
            {
                wasTruncated = true;
                break;
            }

            var row = new object?[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++)
                row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);

            rows.Add(row);
        }

        logger.LogInformation(
            "Query completata: {RowCount} righe restituite, troncata: {Truncated}",
            rows.Count, wasTruncated);

        return new QueryResult(columns, rows, rows.Count, wasTruncated);
    }

    public async Task<IReadOnlyList<TableInfo>> GetTablesAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE
            FROM INFORMATION_SCHEMA.TABLES
            ORDER BY TABLE_SCHEMA, TABLE_NAME
            """;

        var result = await ExecuteQueryAsync(sql, cancellationToken);

        return result.Rows
            .Select(r => new TableInfo(
                r[0]?.ToString() ?? "",
                r[1]?.ToString() ?? "",
                r[2]?.ToString() ?? ""))
            .ToList();
    }

    public async Task<IReadOnlyList<ColumnInfo>> GetTableSchemaAsync(
        string tableName,
        CancellationToken cancellationToken = default)
    {
        // Sanitize: accetta solo nomi tabella senza caratteri speciali
        if (!System.Text.RegularExpressions.Regex.IsMatch(tableName, @"^[\w\.\[\]]+$"))
            throw new ArgumentException("Nome tabella non valido.", nameof(tableName));

        var sql = $"""
            SELECT
                c.COLUMN_NAME,
                c.DATA_TYPE,
                c.IS_NULLABLE,
                c.CHARACTER_MAXIMUM_LENGTH,
                CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IS_PK
            FROM INFORMATION_SCHEMA.COLUMNS c
            LEFT JOIN (
                SELECT ku.COLUMN_NAME
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                    ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                  AND ku.TABLE_NAME = '{tableName}'
            ) pk ON c.COLUMN_NAME = pk.COLUMN_NAME
            WHERE c.TABLE_NAME = '{tableName}'
            ORDER BY c.ORDINAL_POSITION
            """;

        var result = await ExecuteQueryAsync(sql, cancellationToken);

        return result.Rows
            .Select(r => new ColumnInfo(
                r[0]?.ToString() ?? "",
                r[1]?.ToString() ?? "",
                r[2]?.ToString() == "YES",
                r[3] as int?,
                r[4]?.ToString() == "1"))
            .ToList();
    }
}
```

---

### `Tools/QueryTool.cs`

```csharp
using ModelContextProtocol.Server;
using MetronomoMCP.Data.Abstractions;
using MetronomoMCP.Validation;
using System.ComponentModel;

namespace MetronomoMCP.Tools;

[McpServerToolType]
public sealed class QueryTool(IQueryRepository repository)
{
    [McpServerTool(Name = "execute_query")]
    [Description("Esegue una query SQL in sola lettura sul database SQL Server e restituisce i risultati.")]
    public async Task<string> ExecuteQueryAsync(
        [Description("Query SQL SELECT da eseguire. Solo operazioni di lettura sono permesse.")]
        string sql,
        CancellationToken cancellationToken = default)
    {
        var validation = SqlReadOnlyValidator.Validate(sql);
        if (!validation.IsValid)
            return $"ERRORE VALIDAZIONE: {validation.ErrorMessage}";

        try
        {
            var result = await repository.ExecuteQueryAsync(sql, cancellationToken);
            return FormatResult(result);
        }
        catch (Exception ex)
        {
            return $"ERRORE ESECUZIONE: {ex.Message}";
        }
    }

    private static string FormatResult(QueryResult result)
    {
        if (result.Rows.Count == 0)
            return "Nessun risultato trovato.";

        var sb = new System.Text.StringBuilder();

        // Header
        sb.AppendLine(string.Join(" | ", result.Columns));
        sb.AppendLine(new string('-', result.Columns.Sum(c => c.Length + 3)));

        // Rows
        foreach (var row in result.Rows)
        {
            sb.AppendLine(string.Join(" | ", row.Select(v => v?.ToString() ?? "NULL")));
        }

        sb.AppendLine($"\n[{result.TotalRows} righe restituite" +
                      (result.WasTruncated ? " — RISULTATI TRONCATI al limite configurato]" : "]"));

        return sb.ToString();
    }
}
```

---

### `Tools/TableListTool.cs`

```csharp
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
```

---

### `Tools/SchemaTool.cs`

```csharp
using ModelContextProtocol.Server;
using MetronomoMCP.Data.Abstractions;
using System.ComponentModel;
using System.Text;

namespace MetronomoMCP.Tools;

[McpServerToolType]
public sealed class SchemaTool(IQueryRepository repository)
{
    [McpServerTool(Name = "describe_table")]
    [Description("Descrive la struttura (schema) di una tabella specifica: colonne, tipi e chiavi primarie.")]
    public async Task<string> DescribeTableAsync(
        [Description("Nome della tabella di cui descrivere lo schema (es. 'Customers' o 'dbo.Orders').")]
        string tableName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var columns = await repository.GetTableSchemaAsync(tableName, cancellationToken);

            if (columns.Count == 0)
                return $"Tabella '{tableName}' non trovata o priva di colonne.";

            var sb = new StringBuilder();
            sb.AppendLine($"Schema tabella: {tableName}\n");
            sb.AppendLine($"{"Colonna",-30} {"Tipo",-20} {"Nullable",-10} {"MaxLen",-10} PK");
            sb.AppendLine(new string('-', 80));

            foreach (var col in columns)
            {
                sb.AppendLine(
                    $"{col.ColumnName,-30} " +
                    $"{col.DataType,-20} " +
                    $"{(col.IsNullable ? "YES" : "NO"),-10} " +
                    $"{col.MaxLength?.ToString() ?? "-",-10} " +
                    $"{(col.IsPrimaryKey ? "✓" : "")}");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"ERRORE: {ex.Message}";
        }
    }
}
```

---

### `Program.cs`

```csharp
using MetronomoMCP.Configuration;
using MetronomoMCP.Data;
using MetronomoMCP.Data.Abstractions;
using MetronomoMCP.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

// Bootstrap Serilog prima dell'host per catturare errori di avvio
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Avvio MetronomoMCP...");

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

    // Repository
    builder.Services.AddSingleton<IQueryRepository, SqlQueryRepository>();

    // MCP Server — trasporto stdio (compatibile Claude Desktop e Ollama)
    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithToolsFromAssembly();

    var app = builder.Build();
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
```

---

## MCP Tools Esposti

| Tool            | Nome MCP         | Descrizione                                              |
|-----------------|------------------|----------------------------------------------------------|
| `QueryTool`     | `execute_query`  | Esegue query SQL SELECT con validazione in sola lettura  |
| `TableListTool` | `list_tables`    | Elenca tutte le tabelle/viste del database               |
| `SchemaTool`    | `describe_table` | Descrive schema di una tabella (colonne, tipi, PK)       |

---

## Sicurezza — Approccio Read-Only

1. **`SqlReadOnlyValidator`**: blocca a livello applicativo ogni keyword DDL/DML pericolosa (`INSERT`, `UPDATE`, `DELETE`, `DROP`, `TRUNCATE`, `ALTER`, `CREATE`, `EXEC`, `MERGE`, `GRANT`, `REVOKE`, `DENY`, `BULK`).
2. **Utente DB con permessi limitati**: configurare l'utente SQL Server con solo `db_datareader` e nessun altro ruolo.
3. **Nessuna stored procedure di scrittura**: l'`EXEC` è bloccato nel validator.
4. **Sanificazione nomi tabella**: il metodo `GetTableSchemaAsync` valida il nome con regex prima di usarlo in query dinamiche.

---

## Configurazione Claude Desktop

Aggiungere in `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "MetronomoMCP": {
      "command": "dotnet",
      "args": ["run", "--project", "/percorso/assoluto/src/MetronomoMCP"],
      "env": {}
    }
  }
}
```

Oppure, dopo il publish:

```json
{
  "mcpServers": {
    "MetronomoMCP": {
      "command": "/percorso/assoluto/MetronomoMCP.exe"
    }
  }
}
```

---

## Configurazione Futura Ollama (SSE Transport)

Per rendere il server accessibile via HTTP/SSE (richiesto da Ollama e client non stdio):

1. In `appsettings.json` impostare `"TransportMode": "sse"`
2. In `Program.cs` sostituire `.WithStdioServerTransport()` con `.WithHttpServerTransport(port: 5100)`
3. Ollama punterà a `http://localhost:5100/sse`

---

## Testing

### `MetronomoMCP.Tests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\MetronomoMCP\MetronomoMCP.csproj" />
    <PackageReference Include="xunit" Version="*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="*" />
    <PackageReference Include="Moq" Version="*" />
    <PackageReference Include="FluentAssertions" Version="*" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="*" />
  </ItemGroup>
</Project>
```

### Esempio Test Validatore

```csharp
using FluentAssertions;
using MetronomoMCP.Validation;
using Xunit;

namespace MetronomoMCP.Tests.Validation;

public class SqlReadOnlyValidatorTests
{
    [Theory]
    [InlineData("SELECT * FROM Customers")]
    [InlineData("SELECT Id, Name FROM Orders WHERE Status = 'Active'")]
    [InlineData("SELECT COUNT(*) FROM Products")]
    public void ValidQuery_ShouldPass(string sql)
    {
        var result = SqlReadOnlyValidator.Validate(sql);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("INSERT INTO Customers VALUES ('x')")]
    [InlineData("DELETE FROM Orders")]
    [InlineData("DROP TABLE Products")]
    [InlineData("UPDATE Users SET Name = 'hack'")]
    [InlineData("EXEC sp_executesql N'SELECT 1'")]
    [InlineData("TRUNCATE TABLE Logs")]
    public void ForbiddenQuery_ShouldFail(string sql)
    {
        var result = SqlReadOnlyValidator.Validate(sql);
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }
}
```

---

## Comandi Utili

```bash
# Ripristino dipendenze
dotnet restore

# Build
dotnet build --configuration Release

# Test
dotnet test

# Esecuzione locale
dotnet run --project src/MetronomoMCP

# Publish (self-contained, Windows x64)
dotnet publish src/MetronomoMCP \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -o ./publish
```

---

## Roadmap Futura

- [ ] Aggiungere supporto **SSE transport** per Ollama e client HTTP
- [ ] Implementare **secret management** (Azure Key Vault / User Secrets)
- [ ] Aggiungere **query caching** (IMemoryCache) per query frequenti
- [ ] Supporto **parametri SQL** per prevenire SQL injection nelle query dinamiche
- [ ] Aggiungere **rate limiting** per protezione da abusi
- [ ] Esporre **MCP Resources** per schema database (oltre ai Tool)
- [ ] Dashboard metriche con OpenTelemetry

---

*Generato per il progetto MetronomoMCP — .NET 10 / C# 13 / SQL Server / Model Context Protocol*
