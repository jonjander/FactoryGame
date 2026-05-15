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
            var candles = (await db.MarketPriceCandles.AsNoTracking()
                .Where(c => c.ElementId == elementId)
                .ToListAsync(ct))
                .OrderByDescending(c => c.BucketStart)
                .Take(2)
                .ToList();

            var lastPrice = candles.FirstOrDefault()?.Close
                ?? depth.BestAsk
                ?? depth.BestBid
                ?? ElementReferencePrice.Compute(element.Dna);

            decimal? changePct = null;
            if (candles.Count >= 2 && candles[1].Close > 0)
            {
                changePct = Math.Round((lastPrice - candles[1].Close) / candles[1].Close * 100m, 2);
            }

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

    public async Task<IReadOnlyList<MarketTradeRow>> GetRecentTradesAsync(int? elementId, int limit, CancellationToken ct = default)
    {
        var take = Math.Clamp(limit, 1, 200);
        var q = db.TradeExecutions.AsNoTracking();
        if (elementId is { } e)
            q = q.Where(t => t.ElementId == e);

        var rows = await q.ToListAsync(ct);
        return rows
            .OrderByDescending(t => t.CreatedAt)
            .Take(take)
            .Select(t => new MarketTradeRow(t.Id, t.ElementId, t.Price, t.Quantity, t.CreatedAt))
            .ToList();
    }
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
