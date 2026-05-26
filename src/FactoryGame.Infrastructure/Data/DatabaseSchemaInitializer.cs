using Microsoft.EntityFrameworkCore;

namespace FactoryGame.Infrastructure.Data;

public interface IDatabaseSchemaInitializer
{
    Task EnsureSchemaAsync(AppDbContext db, CancellationToken cancellationToken = default);
}

internal sealed class SqliteDatabaseSchemaInitializer : IDatabaseSchemaInitializer
{
    public async Task EnsureSchemaAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);
        await ApplyMarketLiquidityPatchesAsync(db, cancellationToken);
        await ApplyPoolDnaPatchesAsync(db, cancellationToken);
    }

    private static async Task ApplyMarketLiquidityPatchesAsync(AppDbContext db, CancellationToken ct)
    {
        await TryAddColumnAsync(db, "MarketOrders", "IsSynthetic", "INTEGER NOT NULL DEFAULT 0", ct);
        await TryAddColumnAsync(db, "TradeExecutions", "IsSynthetic", "INTEGER NOT NULL DEFAULT 0", ct);
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS MarketPriceCandles (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ElementId INTEGER NOT NULL,
                BucketStart TEXT NOT NULL,
                Open REAL NOT NULL,
                High REAL NOT NULL,
                Low REAL NOT NULL,
                Close REAL NOT NULL,
                Volume INTEGER NOT NULL
            );
            """, ct);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_MarketPriceCandles_ElementId_BucketStart ON MarketPriceCandles (ElementId, BucketStart);", ct);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_MarketOrders_ElementId_IsSynthetic_Status ON MarketOrders (ElementId, IsSynthetic, Status);", ct);
    }

    private static async Task ApplyPoolDnaPatchesAsync(AppDbContext db, CancellationToken ct)
    {
        await TryAddColumnAsync(db, "PoolStacks", "Dna", "INTEGER NOT NULL DEFAULT 0", ct);
        await TryAddColumnAsync(db, "MarketOrders", "Dna", "INTEGER NOT NULL DEFAULT 0", ct);
        await TryAddColumnAsync(db, "TradeExecutions", "Dna", "INTEGER NOT NULL DEFAULT 0", ct);

        foreach (var element in FactoryGame.Domain.Content.ElementCatalog.All)
        {
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE PoolStacks SET Dna = {0} WHERE ElementId = {1} AND Dna = 0;",
                element.Dna, element.Id);
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE MarketOrders SET Dna = {0} WHERE ElementId = {1} AND Dna = 0;",
                element.Dna, element.Id);
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE TradeExecutions SET Dna = {0} WHERE ElementId = {1} AND Dna = 0;",
                element.Dna, element.Id);
        }

        await db.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS IX_PoolStacks_PlayerId_ElementId;", ct);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_PoolStacks_PlayerId_ElementId_Dna ON PoolStacks (PlayerId, ElementId, Dna);", ct);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_MarketOrders_ElementId_Dna_Status_Side ON MarketOrders (ElementId, Dna, Status, Side);", ct);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_TradeExecutions_ElementId_Dna ON TradeExecutions (ElementId, Dna);", ct);
    }

    private static async Task TryAddColumnAsync(AppDbContext db, string table, string column, string definition, CancellationToken ct)
    {
        var exists = await db.Database.SqlQueryRaw<int>(
                $"SELECT COUNT(*) AS Value FROM pragma_table_info('{table}') WHERE name = '{column}'")
            .FirstOrDefaultAsync(ct);
        if (exists == 0)
            await db.Database.ExecuteSqlRawAsync($"ALTER TABLE {table} ADD COLUMN {column} {definition};", ct);
    }
}
