namespace FactoryGame.Infrastructure.Data;

/// <summary>
/// Resolved database provider and connection string for EF Core.
/// </summary>
public sealed record DbConnectionResolution(string ConnectionString, bool IsSqlite, bool UseEnsureCreatedInsteadOfMigrate);

public static class DbConnectionResolver
{
    /// <summary>
    /// Shared in-memory SQLite so all pooled connections see the same database for the process lifetime.
    /// </summary>
    public const string SqliteInMemoryShared = "Data Source=:memory:;Cache=Shared";

    public static DbConnectionResolution Resolve(string? configuredConnectionString)
    {
        if (string.IsNullOrWhiteSpace(configuredConnectionString))
            return new DbConnectionResolution(SqliteInMemoryShared, IsSqlite: true, UseEnsureCreatedInsteadOfMigrate: true);

        var trimmed = configuredConnectionString.Trim();
        if (IsSqliteConnection(trimmed))
            return new DbConnectionResolution(trimmed, IsSqlite: true, UseEnsureCreatedInsteadOfMigrate: true);

        return new DbConnectionResolution(trimmed, IsSqlite: false, UseEnsureCreatedInsteadOfMigrate: false);
    }

    private static bool IsSqliteConnection(string s)
    {
        var t = s.TrimStart();
        return t.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)
               || t.StartsWith("Filename=", StringComparison.OrdinalIgnoreCase);
    }
}
