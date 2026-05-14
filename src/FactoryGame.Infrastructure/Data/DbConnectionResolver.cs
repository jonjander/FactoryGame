namespace FactoryGame.Infrastructure.Data;

/// <summary>
/// Resolved SQLite connection string for EF Core. PostgreSQL/Npgsql is not used in this project.
/// </summary>
/// <param name="PinSharedMemoryDatabase">When true, register <see cref="SqliteSharedMemoryDatabasePin"/> so the DB is not dropped when all short-lived EF connections close.</param>
public sealed record DbConnectionResolution(string ConnectionString, bool PinSharedMemoryDatabase);

public static class DbConnectionResolver
{
    /// <summary>
    /// Named shared in-memory database (all connections with this exact string share one DB; keep one open via <see cref="SqliteSharedMemoryDatabasePin"/>).
    /// </summary>
    public const string SqliteInMemoryShared = "Data Source=FactoryGameShared;Mode=Memory;Cache=Shared";

    public static DbConnectionResolution Resolve(string? configuredConnectionString)
    {
        if (string.IsNullOrWhiteSpace(configuredConnectionString))
            return new DbConnectionResolution(SqliteInMemoryShared, PinSharedMemoryDatabase: true);

        var trimmed = configuredConnectionString.Trim();
        if (IsSqliteConnection(trimmed))
            return new DbConnectionResolution(trimmed, PinSharedMemoryDatabase: IsSharedInMemorySqlite(trimmed));

        throw new InvalidOperationException(
            "ConnectionStrings:DefaultConnection must be empty (SQLite in-memory) or a SQLite connection string " +
            "starting with \"Data Source=\" or \"Filename=\". PostgreSQL is not supported in this codebase.");
    }

    private static bool IsSqliteConnection(string s)
    {
        var t = s.TrimStart();
        return t.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)
               || t.StartsWith("Filename=", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Shared-cache in-memory DB is removed when no connection references it; pin with a long-lived open connection.</summary>
    private static bool IsSharedInMemorySqlite(string connectionString)
    {
        if (!connectionString.Contains("Cache=Shared", StringComparison.OrdinalIgnoreCase))
            return false;
        return connectionString.Contains(":memory:", StringComparison.OrdinalIgnoreCase)
               || connectionString.Contains("Mode=Memory", StringComparison.OrdinalIgnoreCase);
    }
}
