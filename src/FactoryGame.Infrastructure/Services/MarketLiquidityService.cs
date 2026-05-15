using FactoryGame.Domain.Content;
using FactoryGame.Domain.Market;
using FactoryGame.Infrastructure.Data;
using FactoryGame.Infrastructure.Data.Entities;
using FactoryGame.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FactoryGame.Infrastructure.Services;

public sealed class MarketLiquidityService(AppDbContext db, IOptions<MarketLiquidityOptions> options)
{
    private readonly MarketLiquidityOptions _opts = options.Value;

    public async Task EnsureLiquidityForAllPooledElementsAsync(CancellationToken ct = default)
    {
        if (!_opts.Enabled)
            return;

        var elementIds = await GetPooledElementIdsAsync(ct);
        await EnsureSystemPlayerAsync(ct);
        foreach (var elementId in elementIds)
            await EnsureLiquidityForElementAsync(elementId, ct);
    }

    public async Task EnsureLiquidityForPlayerPoolAsync(Guid playerId, CancellationToken ct = default)
    {
        if (!_opts.Enabled)
            return;

        var elementIds = await db.PoolStacks.AsNoTracking()
            .Where(s => s.PlayerId == playerId && s.Quantity > 0)
            .Select(s => s.ElementId)
            .Distinct()
            .ToListAsync(ct);

        if (elementIds.Count == 0)
            return;

        await EnsureSystemPlayerAsync(ct);
        foreach (var elementId in elementIds)
            await EnsureLiquidityForElementAsync(elementId, ct);
    }

    public async Task EnsureLiquidityForElementAsync(int elementId, CancellationToken ct = default)
    {
        if (!_opts.Enabled)
            return;

        if (!ElementCatalog.All.Any(e => e.Id == elementId))
            return;

        var element = ElementCatalog.All.First(e => e.Id == elementId);
        await EnsureSystemPlayerAsync(ct);
        await EnsureSystemPoolStackAsync(elementId, ct);
        await SeedHistoryIfNeededAsync(elementId, element.Dna, ct);

        var referenceMid = ElementReferencePrice.Compute(element.Dna);
        var playerOrders = await db.MarketOrders
            .Where(o => o.ElementId == elementId && o.Status == OrderStatus.Open && !o.IsSynthetic && o.QuantityRemaining > 0)
            .ToListAsync(ct);

        var syntheticOpen = await db.MarketOrders
            .Where(o => o.ElementId == elementId && o.IsSynthetic && o.Status == OrderStatus.Open)
            .ToListAsync(ct);
        foreach (var o in syntheticOpen)
            o.Status = OrderStatus.Cancelled;

        var bestBid = playerOrders.Where(o => o.Side == OrderSide.Buy).Select(o => o.LimitPrice).Max();
        var bestAsk = playerOrders.Where(o => o.Side == OrderSide.Sell).Select(o => o.LimitPrice).Min();
        var playerBidQty = playerOrders.Where(o => o.Side == OrderSide.Buy).Sum(o => o.QuantityRemaining);
        var playerAskQty = playerOrders.Where(o => o.Side == OrderSide.Sell).Sum(o => o.QuantityRemaining);

        var mid = ComputeMid(referenceMid, bestBid, bestAsk);
        var capQty = ComputeSyntheticCap(playerBidQty, playerAskQty);
        var rng = CreateRng(elementId, "ladder");

        var now = DateTimeOffset.UtcNow;
        for (var level = 1; level <= _opts.LevelsPerSide; level++)
        {
            var spread = _opts.SpreadStepFraction * level;
            var lot = ComputeLotSize(rng, capQty);

            var buyPrice = RoundPrice(mid * (1m - spread));
            var sellPrice = RoundPrice(mid * (1m + spread));

            if (bestBid.HasValue)
                buyPrice = Math.Min(buyPrice, RoundPrice(bestBid.Value * (1m - spread)));
            if (bestAsk.HasValue)
                sellPrice = Math.Max(sellPrice, RoundPrice(bestAsk.Value * (1m + spread)));

            if (!bestBid.HasValue || buyPrice < bestBid.Value)
            {
                db.MarketOrders.Add(CreateSyntheticOrder(elementId, OrderSide.Buy, buyPrice, lot, now));
            }

            if (!bestAsk.HasValue || sellPrice > bestAsk.Value)
            {
                db.MarketOrders.Add(CreateSyntheticOrder(elementId, OrderSide.Sell, sellPrice, lot, now));
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private MarketOrderEntity CreateSyntheticOrder(int elementId, OrderSide side, decimal price, long qty, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            PlayerId = _opts.SystemPlayerId,
            ElementId = elementId,
            Side = side,
            LimitPrice = price,
            QuantityRemaining = qty,
            OriginalQuantity = qty,
            Status = OrderStatus.Open,
            CreatedAt = now,
            IsSynthetic = true
        };

    private long ComputeSyntheticCap(long playerBidQty, long playerAskQty)
    {
        var playerQty = Math.Max(playerBidQty, playerAskQty);
        if (playerQty <= 0)
            return _opts.MaxLotSize;

        var capped = (long)Math.Ceiling(playerQty * _opts.CapRatio);
        return Math.Clamp(capped, _opts.MinLotSize, _opts.MaxLotSize);
    }

    private long ComputeLotSize(Random rng, long capQty)
    {
        var span = (int)Math.Max(1, capQty - _opts.MinLotSize + 1);
        return _opts.MinLotSize + rng.Next(span);
    }

    private static decimal ComputeMid(decimal referenceMid, decimal? bestBid, decimal? bestAsk)
    {
        if (bestBid.HasValue && bestAsk.HasValue)
            return (bestBid.Value + bestAsk.Value) / 2m;
        if (bestBid.HasValue)
            return (referenceMid + bestBid.Value) / 2m;
        if (bestAsk.HasValue)
            return (referenceMid + bestAsk.Value) / 2m;
        return referenceMid;
    }

    private static decimal RoundPrice(decimal price) =>
        Math.Round(price, 2, MidpointRounding.AwayFromZero);

    private async Task SeedHistoryIfNeededAsync(int elementId, long dna, CancellationToken ct)
    {
        var hasCandles = await db.MarketPriceCandles.AnyAsync(c => c.ElementId == elementId, ct);
        if (hasCandles)
            return;

        var reference = ElementReferencePrice.Compute(dna);
        var rng = CreateRng(elementId, "history");
        var now = DateTimeOffset.UtcNow;
        var price = reference;
        var interval = TimeSpan.FromHours(1);
        var points = _opts.HistoryCandlePoints;

        for (var i = points - 1; i >= 0; i--)
        {
            var bucket = now - interval * i;
            var drift = (decimal)(rng.NextDouble() * 0.04 - 0.02);
            var open = price;
            price = Math.Max(1m, RoundPrice(price * (1m + drift)));
            var high = RoundPrice(Math.Max(open, price) * (1m + (decimal)rng.NextDouble() * 0.01m));
            var low = RoundPrice(Math.Min(open, price) * (1m - (decimal)rng.NextDouble() * 0.01m));
            var close = price;
            var volume = _opts.MinLotSize + rng.Next((int)(_opts.MaxLotSize - _opts.MinLotSize + 1));

            db.MarketPriceCandles.Add(new MarketPriceCandleEntity
            {
                ElementId = elementId,
                BucketStart = bucket,
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = volume
            });
        }

        var tradeRng = CreateRng(elementId, "trades");
        var systemId = _opts.SystemPlayerId;
        for (var t = 0; t < _opts.HistoryTradeSamples; t++)
        {
            var tradeTime = now - TimeSpan.FromMinutes(tradeRng.Next(1, points * 60));
            var tradePrice = RoundPrice(reference * (1m + (decimal)(tradeRng.NextDouble() * 0.1 - 0.05)));
            var tradeQty = _opts.MinLotSize + tradeRng.Next((int)(_opts.MaxLotSize - _opts.MinLotSize + 1));
            var buyOrderId = Guid.NewGuid();
            var sellOrderId = Guid.NewGuid();

            db.TradeExecutions.Add(new TradeExecutionEntity
            {
                Id = Guid.NewGuid(),
                ElementId = elementId,
                Price = tradePrice,
                Quantity = tradeQty,
                BuyerPlayerId = systemId,
                SellerPlayerId = systemId,
                BuyOrderId = buyOrderId,
                SellOrderId = sellOrderId,
                CreatedAt = tradeTime,
                IsSynthetic = true
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureSystemPlayerAsync(CancellationToken ct)
    {
        var exists = await db.Players.AnyAsync(p => p.Id == _opts.SystemPlayerId, ct);
        if (exists)
            return;

        db.Players.Add(new PlayerEntity
        {
            Id = _opts.SystemPlayerId,
            GuestDeviceKeyHash = "__market_maker__",
            CreatedAt = DateTimeOffset.UtcNow,
            Balance = new PlayerBalanceEntity { PlayerId = _opts.SystemPlayerId, Cash = _opts.SystemCash },
            Pool = new InventoryPoolEntity
            {
                PlayerId = _opts.SystemPlayerId,
                MaxVolume = long.MaxValue / 4,
                UsedVolume = 0
            }
        });
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureSystemPoolStackAsync(int elementId, CancellationToken ct)
    {
        var stack = await db.PoolStacks.FirstOrDefaultAsync(
            s => s.PlayerId == _opts.SystemPlayerId && s.ElementId == elementId, ct);
        if (stack != null && stack.Quantity >= _opts.SystemPoolQuantityPerElement / 2)
            return;

        var target = _opts.SystemPoolQuantityPerElement;
        if (stack == null)
        {
            db.PoolStacks.Add(new PoolStackEntity
            {
                Id = Guid.NewGuid(),
                PlayerId = _opts.SystemPlayerId,
                ElementId = elementId,
                Quantity = target,
                VolumePerUnit = 1
            });
            var pool = await db.InventoryPools.FirstAsync(p => p.PlayerId == _opts.SystemPlayerId, ct);
            pool.UsedVolume += target;
        }
        else
        {
            var delta = target - stack.Quantity;
            if (delta > 0)
            {
                stack.Quantity = target;
                var pool = await db.InventoryPools.FirstAsync(p => p.PlayerId == _opts.SystemPlayerId, ct);
                pool.UsedVolume += delta;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task<List<int>> GetPooledElementIdsAsync(CancellationToken ct) =>
        await db.PoolStacks.AsNoTracking()
            .Where(s => s.Quantity > 0)
            .Select(s => s.ElementId)
            .Distinct()
            .ToListAsync(ct);

    private Random CreateRng(int elementId, string salt)
    {
        var hash = HashCode.Combine(elementId, _opts.SeedVersion, salt);
        return new Random(hash);
    }
}
