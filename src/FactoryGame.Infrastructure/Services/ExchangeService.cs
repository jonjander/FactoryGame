using FactoryGame.Contracts.Market;
using FactoryGame.Domain.Content;
using FactoryGame.Domain.Market;
using FactoryGame.Infrastructure.Data;
using FactoryGame.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FactoryGame.Infrastructure.Services;

public sealed class ExchangeService(AppDbContext db)
{
    private const long VolumePerUnit = 1;

    public async Task<PlaceOrderResponse> PlaceOrderAsync(Guid playerId, PlaceOrderRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Quantity <= 0)
            throw new ArgumentException("Quantity must be positive.", nameof(request));

        if (!ElementCatalog.All.Any(e => e.Id == request.ElementId))
            throw new InvalidOperationException("Unknown element.");

        var side = request.Side.Equals("buy", StringComparison.OrdinalIgnoreCase)
            ? OrderSide.Buy
            : request.Side.Equals("sell", StringComparison.OrdinalIgnoreCase)
                ? OrderSide.Sell
                : throw new ArgumentException("Side must be buy or sell.");

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existing = await db.MarketOrders.FirstOrDefaultAsync(
                o => o.PlayerId == playerId && o.IdempotencyKey == request.IdempotencyKey,
                cancellationToken);
            if (existing != null)
                return new PlaceOrderResponse(existing.Id, existing.QuantityRemaining, existing.Status.ToString());
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (side == OrderSide.Sell)
            {
                await RemoveFromPoolAsync(playerId, request.ElementId, request.Quantity, cancellationToken);
            }
            else
            {
                var cost = request.LimitPrice * request.Quantity;
                var balance = await db.PlayerBalances.FirstAsync(b => b.PlayerId == playerId, cancellationToken);
                if (balance.Cash < cost)
                    throw new InvalidOperationException("Insufficient cash for buy.");
                var pool = await db.InventoryPools.FirstAsync(p => p.PlayerId == playerId, cancellationToken);
                if (pool.UsedVolume + request.Quantity * VolumePerUnit > pool.MaxVolume)
                    throw new InvalidOperationException("Pool volume would exceed max; buy blocked.");
            }

            var order = new MarketOrderEntity
            {
                Id = Guid.NewGuid(),
                PlayerId = playerId,
                ElementId = request.ElementId,
                Side = side,
                LimitPrice = request.LimitPrice,
                QuantityRemaining = request.Quantity,
                OriginalQuantity = request.Quantity,
                Status = OrderStatus.Open,
                CreatedAt = DateTimeOffset.UtcNow,
                IdempotencyKey = request.IdempotencyKey
            };
            db.MarketOrders.Add(order);
            await db.SaveChangesAsync(cancellationToken);

            await MatchOrdersForElementAsync(request.ElementId, cancellationToken);

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            var updated = await db.MarketOrders.AsNoTracking()
                .FirstAsync(o => o.Id == order.Id, cancellationToken);
            return new PlaceOrderResponse(updated.Id, updated.QuantityRemaining, updated.Status.ToString());
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task MatchOrdersForElementAsync(int elementId, CancellationToken ct)
    {
        var progressed = true;
        while (progressed)
        {
            progressed = false;
            var buys = await db.MarketOrders
                .Where(o => o.ElementId == elementId && o.Side == OrderSide.Buy && o.Status == OrderStatus.Open && o.QuantityRemaining > 0)
                .OrderByDescending(o => o.LimitPrice)
                .ThenBy(o => o.CreatedAt)
                .ToListAsync(ct);

            foreach (var buy in buys)
            {
                while (buy.QuantityRemaining > 0 && buy.Status == OrderStatus.Open)
                {
                    var sell = await db.MarketOrders
                        .Where(o => o.ElementId == elementId && o.Side == OrderSide.Sell && o.Status == OrderStatus.Open && o.QuantityRemaining > 0 && o.LimitPrice <= buy.LimitPrice)
                        .OrderBy(o => o.LimitPrice)
                        .ThenBy(o => o.CreatedAt)
                        .FirstOrDefaultAsync(ct);

                    if (sell == null)
                        break;

                    var qty = Math.Min(buy.QuantityRemaining, sell.QuantityRemaining);
                    var price = sell.LimitPrice!.Value;

                    var ok = await TryExecuteTradeAsync(buy, sell, price, qty, ct);
                    if (!ok)
                        break;

                    progressed = true;

                    if (buy.QuantityRemaining == 0)
                        buy.Status = OrderStatus.Filled;
                    if (sell.QuantityRemaining == 0)
                        sell.Status = OrderStatus.Filled;
                }
            }
        }
    }

    private async Task<bool> TryExecuteTradeAsync(MarketOrderEntity buy, MarketOrderEntity sell, decimal price, long qty, CancellationToken ct)
    {
        var buyerBalance = await db.PlayerBalances.FirstAsync(b => b.PlayerId == buy.PlayerId, ct);
        var sellerBalance = await db.PlayerBalances.FirstAsync(b => b.PlayerId == sell.PlayerId, ct);
        var total = price * qty;

        if (buyerBalance.Cash < total)
            return false;

        var buyerPool = await db.InventoryPools.FirstAsync(p => p.PlayerId == buy.PlayerId, ct);
        var addVol = qty * VolumePerUnit;
        if (buyerPool.UsedVolume + addVol > buyerPool.MaxVolume)
            return false;

        buyerBalance.Cash -= total;
        sellerBalance.Cash += total;

        await AddToBuyerPoolAsync(buy.PlayerId, buy.ElementId, qty, ct);

        buy.QuantityRemaining -= qty;
        sell.QuantityRemaining -= qty;

        var tradeId = Guid.NewGuid();
        db.TradeExecutions.Add(new TradeExecutionEntity
        {
            Id = tradeId,
            ElementId = buy.ElementId,
            Price = price,
            Quantity = qty,
            BuyerPlayerId = buy.PlayerId,
            SellerPlayerId = sell.PlayerId,
            BuyOrderId = buy.Id,
            SellOrderId = sell.Id,
            CreatedAt = DateTimeOffset.UtcNow
        });

        db.EconomyTransactions.Add(new EconomyTransactionEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = buy.PlayerId,
            Type = "MarketBuy",
            CashDelta = -total,
            CreatedAt = DateTimeOffset.UtcNow,
            Metadata = tradeId.ToString()
        });
        db.EconomyTransactions.Add(new EconomyTransactionEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = sell.PlayerId,
            Type = "MarketSell",
            CashDelta = total,
            CreatedAt = DateTimeOffset.UtcNow,
            Metadata = tradeId.ToString()
        });

        return true;
    }

    private async Task RemoveFromPoolAsync(Guid playerId, int elementId, long qty, CancellationToken ct)
    {
        var stack = await db.PoolStacks.FirstOrDefaultAsync(
            s => s.PlayerId == playerId && s.ElementId == elementId, ct);
        if (stack == null || stack.Quantity < qty)
            throw new InvalidOperationException("Insufficient quantity in pool for sell.");

        stack.Quantity -= qty;
        var pool = await db.InventoryPools.FirstAsync(p => p.PlayerId == playerId, ct);
        pool.UsedVolume -= qty * VolumePerUnit;
        if (stack.Quantity == 0)
            db.PoolStacks.Remove(stack);
    }

    private async Task AddToBuyerPoolAsync(Guid buyerId, int elementId, long qty, CancellationToken ct)
    {
        var toStack = await db.PoolStacks.FirstOrDefaultAsync(
            s => s.PlayerId == buyerId && s.ElementId == elementId, ct);
        var toPool = await db.InventoryPools.FirstAsync(p => p.PlayerId == buyerId, ct);
        if (toStack == null)
        {
            db.PoolStacks.Add(new PoolStackEntity
            {
                Id = Guid.NewGuid(),
                PlayerId = buyerId,
                ElementId = elementId,
                Quantity = qty,
                VolumePerUnit = VolumePerUnit
            });
        }
        else
            toStack.Quantity += qty;

        toPool.UsedVolume += qty * VolumePerUnit;
    }

    public async Task<long> GetPoolQuantityAsync(Guid playerId, int elementId, CancellationToken ct) =>
        await db.PoolStacks.AsNoTracking()
            .Where(s => s.PlayerId == playerId && s.ElementId == elementId)
            .Select(s => s.Quantity)
            .FirstOrDefaultAsync(ct);
}
