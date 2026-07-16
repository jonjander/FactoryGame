using FactoryGame.Domain.Content;
using FactoryGame.Infrastructure.Data;
using FactoryGame.Infrastructure.Data.Entities;
using FactoryGame.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FactoryGame.Infrastructure.Services;

/// <summary>Grants MVP starter elements to new or legacy players (once per player).</summary>
public sealed class PlayerPoolBootstrapService(AppDbContext db, IOptions<GameEconomyOptions> economyOptions)
{
    private const string StarterPoolTransactionType = "StarterPool";

    private readonly GameEconomyOptions _economy = economyOptions.Value;

    public async Task EnsureStarterPoolAsync(Guid playerId, CancellationToken ct = default)
    {
        if (await db.EconomyTransactions.AnyAsync(
                t => t.PlayerId == playerId && t.Type == StarterPoolTransactionType, ct))
            return;

        var elementIds = _economy.GetStartingElementIds();
        if (elementIds.Count == 0 || _economy.StartingElementQuantityPerStack <= 0)
            return;

        var pool = await db.InventoryPools.FirstOrDefaultAsync(p => p.PlayerId == playerId, ct);
        if (pool == null)
            return;

        var qty = _economy.StartingElementQuantityPerStack;
        var added = 0;

        var pendingIds = new HashSet<int>();
        foreach (var elementId in elementIds.Distinct())
        {
            if (!ElementCatalog.All.Any(e => e.Id == elementId))
                continue;

            if (!pendingIds.Add(elementId))
                continue;

            var exists = await db.PoolStacks.AsNoTracking().AnyAsync(
                s => s.PlayerId == playerId && s.ElementId == elementId
                     && s.Dna == ElementCatalogLookup.CatalogDnaFor(elementId), ct);
            if (exists)
                continue;

            if (pool.UsedVolume + qty > pool.MaxVolume)
                break;

            db.PoolStacks.Add(new PoolStackEntity
            {
                Id = Guid.NewGuid(),
                PlayerId = playerId,
                ElementId = elementId,
                Dna = ElementCatalogLookup.CatalogDnaFor(elementId),
                Quantity = qty,
                VolumePerUnit = 1
            });
            pool.UsedVolume += qty;
            added++;
        }

        if (added == 0)
            return;

        db.EconomyTransactions.Add(new EconomyTransactionEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Type = StarterPoolTransactionType,
            CashDelta = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            Metadata = string.Join(',', elementIds.Distinct())
        });

        await db.SaveChangesAsync(ct);
    }
}
