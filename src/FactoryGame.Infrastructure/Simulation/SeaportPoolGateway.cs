using FactoryGame.Domain.Content;
using FactoryGame.Domain.Simulation;
using FactoryGame.Infrastructure.Data;
using FactoryGame.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FactoryGame.Infrastructure.Simulation;

public sealed class SeaportPoolGateway(AppDbContext db, Guid playerId) : ISeaportPoolSink
{
    private const long VolumePerUnit = 1;

    public bool TryWithdraw(int elementId, decimal quantity)
    {
        if (quantity <= 0)
            return false;
        var qty = (long)Math.Ceiling(quantity);
        var stack = db.PoolStacks.FirstOrDefault(s => s.PlayerId == playerId && s.ElementId == elementId);
        if (stack == null || stack.Quantity < qty)
            return false;

        stack.Quantity -= qty;
        var pool = db.InventoryPools.First(p => p.PlayerId == playerId);
        pool.UsedVolume -= qty * VolumePerUnit;
        if (stack.Quantity <= 0)
            db.PoolStacks.Remove(stack);
        return true;
    }

    public bool TryDeposit(int elementId, long dna, decimal quantity)
    {
        if (quantity <= 0)
            return false;
        if (!ElementCatalog.All.Any(e => e.Id == elementId))
            return false;

        var qty = (long)Math.Ceiling(quantity);
        var pool = db.InventoryPools.First(p => p.PlayerId == playerId);
        if (pool.UsedVolume + qty * VolumePerUnit > pool.MaxVolume)
            return false;

        var stack = db.PoolStacks.FirstOrDefault(s => s.PlayerId == playerId && s.ElementId == elementId);
        if (stack == null)
        {
            db.PoolStacks.Add(new PoolStackEntity
            {
                Id = Guid.NewGuid(),
                PlayerId = playerId,
                ElementId = elementId,
                Quantity = qty,
                VolumePerUnit = VolumePerUnit
            });
        }
        else
        {
            stack.Quantity += qty;
        }

        pool.UsedVolume += qty * VolumePerUnit;
        return true;
    }
}
