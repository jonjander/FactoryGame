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

        if (_opts.CommonnessDriftEnabled)
        {
            await ApplyPeriodicAliveMarketDriftAsync(ct);
            return;
        }

        var elementIds = await GetElementsNeedingRefreshAsync(
            await GetPooledElementIdsAsync(ct),
            Math.Max(1, _opts.MaxElementsPerBackgroundRefresh),
            ct);
        if (elementIds.Count == 0)
            return;

        await EnsureSystemPlayerAsync(ct);
        foreach (var elementId in elementIds)
            await EnsureLiquidityForElementAsync(elementId, ct);
    }

    public async Task EnsureLiquidityForPlayerPoolAsync(Guid playerId, CancellationToken ct = default)
    {
        if (!_opts.Enabled)
            return;

        var stacks = await db.PoolStacks.AsNoTracking()
            .Where(s => s.PlayerId == playerId && s.Quantity > 0)
            .Select(s => new { s.ElementId, s.Dna })
            .Distinct()
            .ToListAsync(ct);

        if (stacks.Count == 0)
            return;

        await EnsureSystemPlayerAsync(ct);
        foreach (var stack in stacks)
            await EnsureLiquidityForElementAsync(stack.ElementId, ct, dna: stack.Dna);
    }

    public async Task ApplyPeriodicAliveMarketDriftAsync(CancellationToken ct = default)
    {
        if (!_opts.Enabled || !_opts.CommonnessDriftEnabled)
            return;

        await EnsureSystemPlayerAsync(ct);

        var quantities = await LoadGlobalPoolQuantitiesAsync(ct);
        var scores = MarketCommonnessPriceNudge.ComputeCommonnessScores(
            ElementCatalog.All.Select(e => e.Id),
            quantities);
        var driftBucket = GetDriftBucket();

        foreach (var element in ElementCatalog.All)
        {
            var jitter = ComputeAliveJitter(element.Id, driftBucket);
            var multiplier = MarketCommonnessPriceNudge.ComputeMultiplier(
                scores[element.Id],
                _opts.CommonnessDriftMaxFraction,
                jitter);

            await EnsureLiquidityForElementAsync(element.Id, ct, force: false, dna: element.Dna);
            await ApplyCandleDriftAsync(element.Id, multiplier, ct);
            await EnsureLiquidityForElementAsync(
                element.Id,
                ct,
                force: true,
                dna: element.Dna,
                priceNudgeMultiplier: multiplier);
        }
    }

    public async Task EnsureLiquidityForElementAsync(
        int elementId,
        CancellationToken ct = default,
        bool force = false,
        long? dna = null,
        decimal? priceNudgeMultiplier = null)
    {
        if (!_opts.Enabled)
            return;

        if (!ElementCatalog.All.Any(e => e.Id == elementId))
            return;

        var element = ElementCatalog.All.First(e => e.Id == elementId);
        var tradeDna = dna is > 0 ? dna.Value : element.Dna;

        if (!force && !await NeedsLiquidityRefreshAsync(elementId, tradeDna, ct))
            return;
        await EnsureSystemPlayerAsync(ct);
        await EnsureSystemPoolStackAsync(elementId, ct);
        await SeedHistoryIfNeededAsync(elementId, tradeDna, ct);

        var referenceMid = ElementReferencePrice.Compute(tradeDna);
        if (priceNudgeMultiplier is > 0)
            referenceMid = MarketCommonnessPriceNudge.ApplyToPrice(referenceMid, priceNudgeMultiplier.Value);
        var playerOrders = await db.MarketOrders
            .Where(o => o.ElementId == elementId && o.Dna == tradeDna && o.Status == OrderStatus.Open && !o.IsSynthetic && o.QuantityRemaining > 0)
            .ToListAsync(ct);

        await db.MarketOrders
            .Where(o => o.ElementId == elementId && o.Dna == tradeDna && o.IsSynthetic && o.Status == OrderStatus.Open)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, OrderStatus.Cancelled), ct);

        var bestBid = BestBid(playerOrders);
        var bestAsk = BestAsk(playerOrders);
        var playerBidQty = playerOrders.Where(o => o.Side == OrderSide.Buy).Sum(o => o.QuantityRemaining);
        var playerAskQty = playerOrders.Where(o => o.Side == OrderSide.Sell).Sum(o => o.QuantityRemaining);

        var mid = ComputeMid(referenceMid, bestBid, bestAsk);
        var capQty = ComputeSyntheticCap(playerBidQty, playerAskQty);
        var rng = CreateRng(elementId, $"ladder-{tradeDna}");

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
                db.MarketOrders.Add(CreateSyntheticOrder(elementId, tradeDna, OrderSide.Buy, buyPrice, lot, now));
            }

            if (!bestAsk.HasValue || sellPrice > bestAsk.Value)
            {
                db.MarketOrders.Add(CreateSyntheticOrder(elementId, tradeDna, OrderSide.Sell, sellPrice, lot, now));
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private MarketOrderEntity CreateSyntheticOrder(int elementId, long dna, OrderSide side, decimal price, long qty, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            PlayerId = _opts.SystemPlayerId,
            ElementId = elementId,
            Dna = dna,
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

    private static decimal? BestBid(IReadOnlyList<MarketOrderEntity> playerOrders)
    {
        decimal? best = null;
        foreach (var o in playerOrders)
        {
            if (o.Side != OrderSide.Buy || o.LimitPrice is not { } price)
                continue;
            best = best is null || price > best ? price : best;
        }

        return best;
    }

    private static decimal? BestAsk(IReadOnlyList<MarketOrderEntity> playerOrders)
    {
        decimal? best = null;
        foreach (var o in playerOrders)
        {
            if (o.Side != OrderSide.Sell || o.LimitPrice is not { } price)
                continue;
            best = best is null || price < best ? price : best;
        }

        return best;
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
            s => s.PlayerId == _opts.SystemPlayerId && s.ElementId == elementId
                 && s.Dna == ElementCatalogLookup.CatalogDnaFor(elementId), ct);
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
                Dna = ElementCatalogLookup.CatalogDnaFor(elementId),
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

    private async Task<List<int>> GetElementsNeedingRefreshAsync(
        IReadOnlyList<int> candidateElementIds,
        int maxCount,
        CancellationToken ct)
    {
        if (candidateElementIds.Count == 0 || maxCount <= 0)
            return [];

        var result = new List<int>(Math.Min(maxCount, candidateElementIds.Count));
        foreach (var elementId in candidateElementIds)
        {
            if (await NeedsLiquidityRefreshAsync(elementId, ElementCatalogLookup.CatalogDnaFor(elementId), ct))
                result.Add(elementId);
            if (result.Count >= maxCount)
                break;
        }

        return result;
    }

    private async Task<bool> NeedsLiquidityRefreshAsync(int elementId, long dna, CancellationToken ct)
    {
        var hasCandles = await db.MarketPriceCandles.AsNoTracking()
            .AnyAsync(c => c.ElementId == elementId, ct);
        if (!hasCandles)
            return true;

        var openSyntheticCount = await db.MarketOrders.AsNoTracking()
            .CountAsync(
                o => o.ElementId == elementId && o.Dna == dna && o.IsSynthetic && o.Status == OrderStatus.Open,
                ct);
        if (openSyntheticCount < _opts.LevelsPerSide)
            return true;

        var cooldownCutoff = DateTimeOffset.UtcNow.AddMinutes(-Math.Max(1, _opts.ElementRefreshCooldownMinutes));
        var syntheticCreatedAt = await db.MarketOrders.AsNoTracking()
            .Where(o => o.ElementId == elementId && o.Dna == dna && o.IsSynthetic && o.Status == OrderStatus.Open)
            .Select(o => o.CreatedAt)
            .ToListAsync(ct);

        if (syntheticCreatedAt.Count == 0)
            return true;

        var newestSynthetic = syntheticCreatedAt.Max();
        return newestSynthetic < cooldownCutoff;
    }

    private async Task<Dictionary<int, long>> LoadGlobalPoolQuantitiesAsync(CancellationToken ct)
    {
        var rows = await db.PoolStacks.AsNoTracking()
            .Where(s => s.PlayerId != _opts.SystemPlayerId && s.Quantity > 0)
            .GroupBy(s => s.ElementId)
            .Select(g => new { ElementId = g.Key, Total = g.Sum(s => s.Quantity) })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.ElementId, r => r.Total);
    }

    private async Task ApplyCandleDriftAsync(int elementId, decimal multiplier, CancellationToken ct)
    {
        var candles = await db.MarketPriceCandles
            .Where(c => c.ElementId == elementId)
            .ToListAsync(ct);
        var latest = candles.OrderByDescending(c => c.BucketStart).FirstOrDefault();
        if (latest == null)
            return;

        var newClose = MarketCommonnessPriceNudge.ApplyToPrice(latest.Close, multiplier);
        if (newClose == latest.Close)
            return;

        latest.Close = newClose;
        latest.High = Math.Max(latest.High, newClose);
        latest.Low = Math.Min(latest.Low, newClose);
        latest.Volume += Math.Max(1, _opts.MinLotSize / 4);
        await db.SaveChangesAsync(ct);
    }

    private long GetDriftBucket()
    {
        var intervalMinutes = Math.Max(1, _opts.RefreshIntervalMinutes);
        var bucketTicks = TimeSpan.FromMinutes(intervalMinutes).Ticks;
        return DateTimeOffset.UtcNow.UtcTicks / bucketTicks;
    }

    private decimal ComputeAliveJitter(int elementId, long driftBucket)
    {
        if (_opts.AliveDriftMaxFraction <= 0)
            return 0m;

        var hash = HashCode.Combine(elementId, driftBucket, _opts.SeedVersion, "alive-drift");
        var rng = new Random(hash);
        var unit = (decimal)rng.NextDouble() * 2m - 1m;
        return unit * _opts.AliveDriftMaxFraction;
    }

    private Random CreateRng(int elementId, string salt)
    {
        var hash = HashCode.Combine(elementId, _opts.SeedVersion, salt);
        return new Random(hash);
    }
}
