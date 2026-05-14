using FactoryGame.Contracts.Machines;
using FactoryGame.Domain.Content;
using FactoryGame.Infrastructure.Data;
using FactoryGame.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FactoryGame.Infrastructure.Services;

public sealed class MachineInventoryService(AppDbContext db)
{
    public async Task<IReadOnlyList<PlayerMachineStockDto>> ListStockAsync(Guid playerId, CancellationToken ct)
    {
        var rows = await db.PlayerMachineStocks.AsNoTracking()
            .Where(s => s.PlayerId == playerId)
            .ToListAsync(ct);
        return rows.OrderBy(s => s.CreatedAt)
            .Select(s => new PlayerMachineStockDto(s.Id, s.MachineType, s.CreatedAt))
            .ToList();
    }

    public async Task PurchaseAsync(Guid playerId, string machineType, CancellationToken ct)
    {
        if (!MachineStoreCatalog.TryGetCanonicalType(machineType, out var canonical))
            throw new InvalidOperationException("Unknown machine type for store purchase.");

        var entry = MachineStoreCatalog.TryGetEntry(canonical)
            ?? throw new InvalidOperationException("Machine is not offered in the store.");

        if (!MachinePortCatalog.IsKnownMachineType(canonical))
            throw new InvalidOperationException("Machine type has no port schema.");

        var balance = await db.PlayerBalances.FirstAsync(b => b.PlayerId == playerId, ct);
        if (balance.Cash < entry.Price)
            throw new InvalidOperationException("Insufficient cash.");

        balance.Cash -= entry.Price;
        db.EconomyTransactions.Add(new EconomyTransactionEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Type = "MachinePurchase",
            CashDelta = -entry.Price,
            CreatedAt = DateTimeOffset.UtcNow,
            Metadata = canonical
        });
        db.PlayerMachineStocks.Add(new PlayerMachineStockEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            MachineType = canonical,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }
}
