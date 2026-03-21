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
