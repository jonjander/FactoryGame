using FactoryGame.Contracts.Pool;
using FactoryGame.Domain.Content;
using FactoryGame.Domain.Market;
using FactoryGame.Domain.Names;
using FactoryGame.Infrastructure.Data;
using FactoryGame.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FactoryGame.Infrastructure.Services;

public sealed class MarketQueryService(AppDbContext db)
{
    public async Task<IReadOnlyList<MarketElementSummary>> GetSummaryForPlayerAsync(
        Guid playerId,
        string locale,
        CancellationToken ct = default)
    {
        var stacks = await db.PoolStacks.AsNoTracking()
            .Where(s => s.PlayerId == playerId && s.Quantity > 0)
            .ToListAsync(ct);

        if (stacks.Count == 0)
            return Array.Empty<MarketElementSummary>();

        var elementIds = stacks.Select(s => s.ElementId).ToList();
        var summaries = new List<MarketElementSummary>();

        foreach (var elementId in elementIds)
        {
            var element = ElementCatalog.All.First(e => e.Id == elementId);
            var depth = await GetDepthAsync(elementId, ct);
            var (lastPrice, changePct) = await GetLastPriceAndChangeAsync(elementId, element.Dna, ct);

            var stack = stacks.First(s => s.ElementId == elementId);
            summaries.Add(new MarketElementSummary(
                elementId,
                ElementNameGenerator.Generate(element.Dna, locale),
                stack.Quantity,
                lastPrice,
                changePct,
                depth.BestBid,
                depth.BestAsk));
        }

        return summaries.OrderBy(s => s.ElementId).ToList();
    }

    public async Task<PoolOverviewDto?> GetPoolOverviewAsync(
        Guid playerId,
        string locale,
        CancellationToken ct = default)
    {
        var pool = await db.InventoryPools.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PlayerId == playerId, ct);
        if (pool == null)
            return null;

        var stacks = await db.PoolStacks.AsNoTracking()
            .Where(s => s.PlayerId == playerId && s.Quantity > 0)
            .OrderBy(s => s.ElementId)
            .ToListAsync(ct);

        var ranks = await GetGlobalPriceRanksAsync(ct);
        var catalogSize = ElementCatalog.All.Count;
        var stackViews = new List<PoolStackViewDto>();
        decimal totalValue = 0;

        foreach (var stack in stacks)
        {
            var element = ElementCatalog.All.First(e => e.Id == stack.ElementId);
            var (lastPrice, changePct) = await GetLastPriceAndChangeAsync(stack.ElementId, element.Dna, ct);
            var lineValue = Math.Round(lastPrice * stack.Quantity, 2);
            totalValue += lineValue;
            ranks.TryGetValue(stack.ElementId, out var priceRank);

            stackViews.Add(new PoolStackViewDto(
                stack.ElementId,
                element.Symbol,
                ElementNameGenerator.Generate(element.Dna, locale),
                stack.Quantity,
                stack.VolumePerUnit,
                lastPrice,
                lineValue,
                priceRank > 0 ? priceRank : catalogSize,
                catalogSize,
                changePct));
        }

        return new PoolOverviewDto(
            pool.MaxVolume,
            pool.UsedVolume,
            totalValue,
            stackViews);
    }

    public async Task<IReadOnlyDictionary<int, int>> GetGlobalPriceRanksAsync(CancellationToken ct = default)
    {
        var prices = new List<(int ElementId, decimal Price)>();
        foreach (var element in ElementCatalog.All)
        {
            var (price, _) = await GetLastPriceAndChangeAsync(element.Id, element.Dna, ct);
            prices.Add((element.Id, price));
        }

        var ranked = prices
            .OrderByDescending(p => p.Price)
            .ThenBy(p => p.ElementId)
            .Select((p, index) => (p.ElementId, Rank: index + 1))
            .ToList();

        return ranked.ToDictionary(x => x.ElementId, x => x.Rank);
    }

    public async Task<(decimal LastPrice, decimal? ChangePercent24h)> GetLastPriceAndChangeAsync(
        int elementId,
        long dna,
        CancellationToken ct = default)
    {
        var depth = await GetDepthAsync(elementId, ct);
        var candles = (await db.MarketPriceCandles.AsNoTracking()
            .Where(c => c.ElementId == elementId)
            .ToListAsync(ct))
            .OrderByDescending(c => c.BucketStart)
            .Take(2)
            .ToList();

        var lastPrice = candles.FirstOrDefault()?.Close
            ?? depth.BestAsk
            ?? depth.BestBid
            ?? ElementReferencePrice.Compute(dna);

        decimal? changePct = null;
        if (candles.Count >= 2 && candles[1].Close > 0)
        {
            changePct = Math.Round((lastPrice - candles[1].Close) / candles[1].Close * 100m, 2);
        }

        return (lastPrice, changePct);
    }

    public async Task<MarketDepthSnapshot> GetDepthAsync(int elementId, CancellationToken ct = default)
    {
        var orders = await db.MarketOrders.AsNoTracking()
            .Where(o => o.ElementId == elementId && o.Status == OrderStatus.Open && o.QuantityRemaining > 0)
            .ToListAsync(ct);

        var levels = orders
            .Where(o => o.LimitPrice.HasValue)
            .GroupBy(o => o.LimitPrice!.Value)
            .Select(g => new MarketDepthLevel(
                g.Key,
                g.Where(o => o.Side == OrderSide.Buy).Sum(o => o.QuantityRemaining),
                g.Where(o => o.Side == OrderSide.Sell).Sum(o => o.QuantityRemaining)))
            .OrderByDescending(l => l.Price)
            .ToList();

        var bidPrices = levels.Where(l => l.BidQuantity > 0).Select(l => l.Price).ToList();
        var askPrices = levels.Where(l => l.AskQuantity > 0).Select(l => l.Price).ToList();
        decimal? bid = bidPrices.Count > 0 ? bidPrices.Max() : null;
        decimal? ask = askPrices.Count > 0 ? askPrices.Min() : null;

        return new MarketDepthSnapshot(elementId, bid, ask, levels);
    }

    public async Task<IReadOnlyList<MarketCandlePoint>> GetHistoryAsync(int elementId, int points, CancellationToken ct = default)
    {
        var take = Math.Clamp(points, 1, 500);
        return (await db.MarketPriceCandles.AsNoTracking()
            .Where(c => c.ElementId == elementId)
            .ToListAsync(ct))
            .OrderByDescending(c => c.BucketStart)
            .Take(take)
            .OrderBy(c => c.BucketStart)
            .Select(c => new MarketCandlePoint(c.BucketStart, c.Open, c.High, c.Low, c.Close, c.Volume))
            .ToList();
    }

    public async Task<IReadOnlyList<MarketTradeRow>> GetRecentTradesAsync(
        int? elementId,
        int limit,
        bool includeSynthetic = false,
        Guid? highlightPlayerId = null,
        CancellationToken ct = default)
    {
        var take = Math.Clamp(limit, 1, 200);
        var q = db.TradeExecutions.AsNoTracking();
        if (elementId is { } e)
            q = q.Where(t => t.ElementId == e);
        if (!includeSynthetic)
            q = q.Where(t => !t.IsSynthetic);

        var rows = await q.ToListAsync(ct);
        var ordered = rows
            .OrderByDescending(t => t.CreatedAt.UtcDateTime.Ticks)
            .ThenByDescending(t => t.Id)
            .ToList();

        if (highlightPlayerId is { } playerId)
        {
            var mine = ordered
                .Where(t => t.BuyerPlayerId == playerId || t.SellerPlayerId == playerId)
                .Take(take)
                .ToList();
            if (mine.Count >= take)
                return MapTrades(mine);

            var rest = ordered
                .Where(t => t.BuyerPlayerId != playerId && t.SellerPlayerId != playerId)
                .Take(take - mine.Count);
            return MapTrades(mine.Concat(rest));
        }

        return MapTrades(ordered.Take(take));
    }

    private static IReadOnlyList<MarketTradeRow> MapTrades(IEnumerable<TradeExecutionEntity> rows) =>
        rows.Select(t => new MarketTradeRow(t.Id, t.ElementId, t.Price, t.Quantity, t.CreatedAt)).ToList();
}

public sealed record MarketElementSummary(
    int ElementId,
    string DisplayName,
    long PoolQuantity,
    decimal LastPrice,
    decimal? ChangePercent24h,
    decimal? BestBid,
    decimal? BestAsk);

public sealed record MarketDepthLevel(decimal Price, long BidQuantity, long AskQuantity);

public sealed record MarketDepthSnapshot(int ElementId, decimal? BestBid, decimal? BestAsk, IReadOnlyList<MarketDepthLevel> Levels);

public sealed record MarketCandlePoint(
    DateTimeOffset BucketStart,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume);

public sealed record MarketTradeRow(Guid Id, int ElementId, decimal Price, long Quantity, DateTimeOffset CreatedAt);
