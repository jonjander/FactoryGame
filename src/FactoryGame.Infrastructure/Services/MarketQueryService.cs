using FactoryGame.Contracts.Market;
using FactoryGame.Contracts.Pool;
using FactoryGame.Domain.Content;
using FactoryGame.Domain.Dna;
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
            .OrderBy(s => s.ElementId)
            .ThenBy(s => s.Dna)
            .ToListAsync(ct);

        if (stacks.Count == 0)
            return Array.Empty<MarketElementSummary>();

        var summaries = new List<MarketElementSummary>();
        foreach (var stack in stacks)
        {
            var depth = await GetDepthAsync(stack.ElementId, stack.Dna, ct);
            var (lastPrice, changePct) = await GetLastPriceAndChangeAsync(stack.ElementId, stack.Dna, ct);
            var phase = MaterialPhaseLabels.DecodePhase(stack.Dna);
            var label = MaterialLabelFormatter.Format(stack.ElementId, stack.Dna, locale);
            summaries.Add(new MarketElementSummary(
                stack.ElementId,
                stack.Dna,
                MaterialLabelFormatter.VariantCode(stack.ElementId, stack.Dna),
                MaterialPhaseLabels.PhaseKey(phase),
                MaterialPhaseLabels.PhaseLabel(phase),
                label,
                stack.Quantity,
                lastPrice,
                changePct,
                depth.BestBid,
                depth.BestAsk));
        }

        return summaries;
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
            .Where(s => s.PlayerId == playerId)
            .OrderBy(s => s.ElementId)
            .ThenBy(s => s.Dna)
            .ToListAsync(ct);

        var ranks = await GetGlobalPriceRanksAsync(ct);
        var catalogSize = ElementCatalog.All.Count;
        var stackViews = new List<PoolStackViewDto>();
        var groups = new List<PoolElementGroupDto>();
        decimal totalValue = 0;

        foreach (var group in stacks.GroupBy(s => s.ElementId).OrderBy(g => g.Key))
        {
            var element = ElementCatalog.All.First(e => e.Id == group.Key);
            var variants = new List<PoolVariantStackDto>();
            long groupQty = 0;

            foreach (var stack in group.OrderBy(s => MaterialPhaseLabels.PhaseSortOrder(MaterialPhaseLabels.DecodePhase(s.Dna)))
                .ThenBy(s => s.Dna))
            {
                var phase = MaterialPhaseLabels.DecodePhase(stack.Dna);
                var (lastPrice, changePct) = await GetLastPriceAndChangeAsync(stack.ElementId, stack.Dna, ct);
                var lineValue = Math.Round(lastPrice * stack.Quantity, 2);
                totalValue += lineValue;
                groupQty += stack.Quantity;
                ranks.TryGetValue(stack.ElementId, out var priceRank);

                var variant = new PoolVariantStackDto(
                    stack.ElementId,
                    MaterialLabelFormatter.Format(stack.ElementId, stack.Dna, locale),
                    stack.Dna,
                    MaterialPhaseLabels.PhaseKey(phase),
                    MaterialPhaseLabels.PhaseLabel(phase),
                    stack.Quantity,
                    stack.VolumePerUnit,
                    lastPrice,
                    lineValue,
                    priceRank > 0 ? priceRank : catalogSize,
                    catalogSize,
                    changePct);
                variants.Add(variant);
                stackViews.Add(new PoolStackViewDto(
                    stack.ElementId,
                    MaterialLabelFormatter.Format(stack.ElementId, stack.Dna, locale),
                    stack.Dna,
                    MaterialPhaseLabels.PhaseKey(phase),
                    MaterialPhaseLabels.PhaseLabel(phase),
                    MaterialLabelFormatter.Format(stack.ElementId, stack.Dna, locale),
                    stack.Quantity,
                    stack.VolumePerUnit,
                    lastPrice,
                    lineValue,
                    priceRank > 0 ? priceRank : catalogSize,
                    catalogSize,
                    changePct));
            }

            groups.Add(new PoolElementGroupDto(
                element.Id,
                element.Symbol,
                ElementNameGenerator.Generate(element.Dna, locale),
                groupQty,
                variants));
        }

        return new PoolOverviewDto(
            pool.MaxVolume,
            pool.UsedVolume,
            totalValue,
            stackViews,
            groups);
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
        var depth = await GetDepthAsync(elementId, dna, ct);
        var candles = (await db.MarketPriceCandles.AsNoTracking()
            .Where(c => c.ElementId == elementId && c.Dna == dna)
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

    public async Task<MarketDepthSnapshot> GetDepthAsync(int elementId, long dna, CancellationToken ct = default)
    {
        if (dna == 0)
            dna = ElementCatalogLookup.CatalogDnaFor(elementId);

        var orders = await db.MarketOrders.AsNoTracking()
            .Where(o => o.ElementId == elementId && o.Dna == dna && o.Status == OrderStatus.Open && o.QuantityRemaining > 0)
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

        return new MarketDepthSnapshot(elementId, dna, bid, ask, levels);
    }

    public async Task<IReadOnlyList<MarketCandlePoint>> GetHistoryAsync(
        int elementId,
        long dna,
        int points,
        CancellationToken ct = default)
    {
        if (dna == 0)
            dna = ElementCatalogLookup.CatalogDnaFor(elementId);

        var take = Math.Clamp(points, 1, 500);
        return (await db.MarketPriceCandles.AsNoTracking()
            .Where(c => c.ElementId == elementId && c.Dna == dna)
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
                return await MapTradesAsync(mine, playerId, ct);

            var rest = ordered
                .Where(t => t.BuyerPlayerId != playerId && t.SellerPlayerId != playerId)
                .Take(take - mine.Count);
            return await MapTradesAsync(mine.Concat(rest), playerId, ct);
        }

        return await MapTradesAsync(ordered.Take(take), highlightPlayerId, ct);
    }

    public async Task<MarketInsightsResponse> GetInsightsForPlayerAsync(
        Guid playerId,
        string locale,
        CancellationToken ct = default)
    {
        var holdings = await db.PoolStacks.AsNoTracking()
            .Where(s => s.PlayerId == playerId && s.Quantity > 0)
            .Select(s => new { s.ElementId, s.Dna })
            .ToListAsync(ct);

        var openSponsorOrders = await (
            from o in db.MarketOrders.AsNoTracking()
            join c in db.SponsorCompanies.AsNoTracking() on o.SponsorCompanyId equals c.Id
            where o.Status == OrderStatus.Open && o.QuantityRemaining > 0 && o.SponsorCompanyId != null && c.IsActive
            select new { Order = o, Company = c }).ToListAsync(ct);

        var sellOps = new List<MarketInsightDto>();
        foreach (var row in openSponsorOrders.Where(x => x.Order.Side == OrderSide.Buy))
        {
            if (!holdings.Any(h => h.ElementId == row.Order.ElementId && h.Dna == row.Order.Dna))
                continue;
            sellOps.Add(await MapInsightAsync(row.Company, row.Order, locale, ct));
        }

        sellOps = sellOps
            .OrderByDescending(i => i.AttractivenessScore)
            .ThenByDescending(i => i.LimitPrice)
            .Take(20)
            .ToList();

        var buyOpsList = new List<MarketInsightDto>();
        foreach (var x in openSponsorOrders.Where(x => x.Order.Side == OrderSide.Sell))
            buyOpsList.Add(await MapInsightAsync(x.Company, x.Order, locale, ct));

        var buyOps = buyOpsList
            .OrderByDescending(i => i.AttractivenessScore)
            .Take(20)
            .ToList();

        return new MarketInsightsResponse(sellOps, buyOps);
    }

    public async Task<MarketLeaderboardsDto> GetLeaderboardsAsync(CancellationToken ct = default)
    {
        var since = DateTimeOffset.UtcNow.AddDays(-7);
        var trades = await db.TradeExecutions.AsNoTracking()
            .Where(t => !t.IsSynthetic)
            .ToListAsync(ct);

        var sponsors = await db.SponsorCompanies.AsNoTracking().ToDictionaryAsync(c => c.Id, ct);

        var playerSpend = BuildPlayerMetrics(trades, t => t.BuyerPlayerId, since, spend: true);
        var playerVolume = BuildPlayerMetrics(trades, t => t.BuyerPlayerId, since, spend: false, volumeBothSides: true);
        var playerActive = BuildPlayerMetrics(trades, t => t.BuyerPlayerId, since, spend: false, countOnly: true, volumeBothSides: true);

        var sponsorSpend = BuildSponsorMetrics(trades, sponsors, since, spend: true);
        var sponsorVolume = BuildSponsorMetrics(trades, sponsors, since, spend: false);
        var sponsorActive = BuildSponsorMetrics(trades, sponsors, since, spend: false, countOnly: true);

        return new MarketLeaderboardsDto(
            RankPlayers(playerSpend, spend: true),
            RankSponsors(sponsorSpend, sponsors, spend: true),
            RankPlayers(playerVolume, spend: false),
            RankSponsors(sponsorVolume, sponsors, spend: false),
            RankPlayers(playerActive, spend: false, countFocus: true),
            RankSponsors(sponsorActive, sponsors, spend: false, countFocus: true));
    }

    public async Task<SponsorProfileDto?> GetSponsorProfileAsync(Guid sponsorCompanyId, CancellationToken ct = default)
    {
        var company = await db.SponsorCompanies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == sponsorCompanyId, ct);
        if (company == null)
            return null;

        var trades = await db.TradeExecutions.AsNoTracking()
            .Where(t => !t.IsSynthetic && (t.BuyerSponsorCompanyId == company.Id || t.SellerSponsorCompanyId == company.Id))
            .ToListAsync(ct);

        var spend = company.FundingMode == SponsorFundingMode.Utopia
            ? company.VirtualSpend
            : trades.Where(t => t.BuyerSponsorCompanyId == company.Id).Sum(t => t.Price * t.Quantity);

        var leaderboards = await GetLeaderboardsAsync(ct);
        var spendRank = IndexOf(leaderboards.BigSpendersSponsors, company.Id);
        var volumeRank = IndexOf(leaderboards.TopVolumeSponsors, company.Id);

        return new SponsorProfileDto(
            company.Id,
            company.Name,
            company.Description,
            company.LogoUrl,
            company.ExposureTier,
            new SponsorCompanyPublicStatsDto(
                spend,
                trades.Sum(t => t.Quantity),
                trades.Count,
                spendRank >= 0 ? spendRank + 1 : null,
                volumeRank >= 0 ? volumeRank + 1 : null));
    }

    private static int IndexOf(IReadOnlyList<LeaderboardEntryDto> entries, Guid id)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].Id == id)
                return i;
        }
        return -1;
    }

    private async Task<MarketInsightDto> MapInsightAsync(
        SponsorCompanyEntity company,
        MarketOrderEntity order,
        string locale,
        CancellationToken ct)
    {
        var element = ElementCatalog.All.First(e => e.Id == order.ElementId);
        var phase = MaterialPhaseLabels.DecodePhase(order.Dna);
        var price = order.LimitPrice ?? 0m;
        var score = price * order.QuantityRemaining * company.ExposureTier;
        await Task.CompletedTask;
        return new MarketInsightDto(
            company.Id,
            company.Name,
            company.LogoUrl,
            order.ElementId,
            order.Dna,
            MaterialLabelFormatter.VariantCode(order.ElementId, order.Dna),
            MaterialPhaseLabels.PhaseLabel(phase),
            MaterialLabelFormatter.Format(order.ElementId, order.Dna, locale),
            price,
            order.QuantityRemaining,
            company.ExposureTier,
            score);
    }

    private static Dictionary<Guid, (decimal Spend, long Volume, int Count)> BuildPlayerMetrics(
        IReadOnlyList<TradeExecutionEntity> trades,
        Func<TradeExecutionEntity, Guid> pickPlayer,
        DateTimeOffset since,
        bool spend,
        bool countOnly = false,
        bool volumeBothSides = false)
    {
        var result = new Dictionary<Guid, (decimal Spend, long Volume, int Count)>();
        foreach (var t in trades)
        {
            if (t.BuyerSponsorCompanyId != null || t.SellerSponsorCompanyId != null)
                continue;

            void Add(Guid playerId, bool asBuyer)
            {
                if (!result.TryGetValue(playerId, out var m))
                    m = (0, 0, 0);
                if (asBuyer && spend)
                    m.Spend += t.Price * t.Quantity;
                if (!countOnly)
                    m.Volume += t.Quantity;
                m.Count += 1;
                result[playerId] = m;
            }

            if (volumeBothSides)
            {
                Add(t.BuyerPlayerId, asBuyer: true);
                if (t.SellerPlayerId != t.BuyerPlayerId)
                    Add(t.SellerPlayerId, asBuyer: false);
            }
            else
            {
                Add(pickPlayer(t), asBuyer: true);
            }
        }

        _ = since;
        return result;
    }

    private static Dictionary<Guid, (decimal Spend, long Volume, int Count)> BuildSponsorMetrics(
        IReadOnlyList<TradeExecutionEntity> trades,
        IReadOnlyDictionary<Guid, SponsorCompanyEntity> sponsors,
        DateTimeOffset since,
        bool spend,
        bool countOnly = false)
    {
        var result = new Dictionary<Guid, (decimal Spend, long Volume, int Count)>();
        foreach (var t in trades)
        {
            Guid? sponsorId = t.BuyerSponsorCompanyId ?? t.SellerSponsorCompanyId;
            if (sponsorId is not { } id || !sponsors.ContainsKey(id))
                continue;

            if (!result.TryGetValue(id, out var m))
                m = (0, 0, 0);

            if (spend && t.BuyerSponsorCompanyId == id)
                m.Spend += t.Price * t.Quantity;

            if (!countOnly)
                m.Volume += t.Quantity;
            m.Count += 1;
            result[id] = m;
        }

        _ = since;
        return result;
    }

    private static IReadOnlyList<LeaderboardEntryDto> RankPlayers(
        Dictionary<Guid, (decimal Spend, long Volume, int Count)> metrics,
        bool spend,
        bool countFocus = false)
    {
        var ordered = metrics
            .OrderByDescending(kv => countFocus ? kv.Value.Count : spend ? kv.Value.Spend : kv.Value.Volume)
            .ThenBy(kv => kv.Key)
            .Take(25)
            .ToList();

        var list = new List<LeaderboardEntryDto>();
        for (var i = 0; i < ordered.Count; i++)
        {
            var kv = ordered[i];
            list.Add(new LeaderboardEntryDto(
                kv.Key,
                AnonymizePlayer(kv.Key),
                null,
                null,
                kv.Value.Spend,
                kv.Value.Volume,
                kv.Value.Count,
                i + 1));
        }
        return list;
    }

    private static IReadOnlyList<LeaderboardEntryDto> RankSponsors(
        Dictionary<Guid, (decimal Spend, long Volume, int Count)> metrics,
        IReadOnlyDictionary<Guid, SponsorCompanyEntity> sponsors,
        bool spend,
        bool countFocus = false)
    {
        var ordered = metrics
            .OrderByDescending(kv => countFocus ? kv.Value.Count : spend ? kv.Value.Spend : kv.Value.Volume)
            .ThenBy(kv => kv.Key)
            .Take(25)
            .ToList();

        var list = new List<LeaderboardEntryDto>();
        for (var i = 0; i < ordered.Count; i++)
        {
            var kv = ordered[i];
            sponsors.TryGetValue(kv.Key, out var company);
            list.Add(new LeaderboardEntryDto(
                kv.Key,
                company?.Name ?? "Company",
                company?.LogoUrl,
                company?.Description,
                kv.Value.Spend,
                kv.Value.Volume,
                kv.Value.Count,
                i + 1));
        }
        return list;
    }

    private async Task<IReadOnlyList<MarketTradeRow>> MapTradesAsync(
        IEnumerable<TradeExecutionEntity> rows,
        Guid? viewerPlayerId,
        CancellationToken ct)
    {
        var list = rows.ToList();
        if (list.Count == 0)
            return Array.Empty<MarketTradeRow>();

        var sponsorIds = list
            .SelectMany(t => new[] { t.BuyerSponsorCompanyId, t.SellerSponsorCompanyId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var sponsors = sponsorIds.Count == 0
            ? new Dictionary<Guid, SponsorCompanyEntity>()
            : await db.SponsorCompanies.AsNoTracking()
                .Where(c => sponsorIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, ct);

        return list.Select(t =>
        {
            var buyerLabel = LabelFor(t.BuyerPlayerId, t.BuyerSponsorCompanyId, sponsors, viewerPlayerId);
            var sellerLabel = LabelFor(t.SellerPlayerId, t.SellerSponsorCompanyId, sponsors, viewerPlayerId);
            return new MarketTradeRow(
                t.Id,
                t.ElementId,
                t.Dna,
                t.Price,
                t.Quantity,
                t.CreatedAt,
                buyerLabel,
                sellerLabel,
                t.BuyerSponsorCompanyId.HasValue,
                t.SellerSponsorCompanyId.HasValue,
                t.BuyerSponsorCompanyId,
                t.SellerSponsorCompanyId);
        }).ToList();
    }

    private static string LabelFor(
        Guid playerId,
        Guid? sponsorCompanyId,
        IReadOnlyDictionary<Guid, SponsorCompanyEntity> sponsors,
        Guid? viewerPlayerId)
    {
        if (viewerPlayerId == playerId)
            return "Du";
        if (sponsorCompanyId is { } sid && sponsors.TryGetValue(sid, out var company))
            return company.Name;
        return AnonymizePlayer(playerId);
    }

    private static string AnonymizePlayer(Guid playerId)
    {
        var tail = playerId.ToString("N")[^4..];
        return $"Spelare •••{tail}";
    }
}

public sealed record MarketElementSummary(
    int ElementId,
    long Dna,
    string Symbol,
    string Phase,
    string PhaseLabel,
    string DisplayName,
    long PoolQuantity,
    decimal LastPrice,
    decimal? ChangePercent24h,
    decimal? BestBid,
    decimal? BestAsk);

public sealed record MarketDepthLevel(decimal Price, long BidQuantity, long AskQuantity);

public sealed record MarketDepthSnapshot(int ElementId, long Dna, decimal? BestBid, decimal? BestAsk, IReadOnlyList<MarketDepthLevel> Levels);

public sealed record MarketCandlePoint(
    DateTimeOffset BucketStart,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume);

public sealed record MarketTradeRow(
    Guid Id,
    int ElementId,
    long Dna,
    decimal Price,
    long Quantity,
    DateTimeOffset CreatedAt,
    string? BuyerLabel,
    string? SellerLabel,
    bool BuyerIsSponsor,
    bool SellerIsSponsor,
    Guid? BuyerSponsorCompanyId,
    Guid? SellerSponsorCompanyId);
