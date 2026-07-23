using FactoryGame.Contracts.Player;
using FactoryGame.Domain.Content;
using FactoryGame.Domain.Market;
using FactoryGame.Infrastructure.Data;
using FactoryGame.Infrastructure.Data.Entities;
using FactoryGame.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FactoryGame.Infrastructure.Services;

public sealed class PlayerEconomyService(
    AppDbContext db,
    MarketQueryService marketQuery,
    IOptions<GameEconomyOptions> economyOptions)
{
    private const int MaxHistoryPoints = 240;

    private readonly GameEconomyOptions _economy = economyOptions.Value;

    public async Task<PlayerEconomyOverviewDto?> GetOverviewAsync(
        Guid playerId,
        string locale,
        CancellationToken ct = default)
    {
        var balance = await db.PlayerBalances.AsNoTracking()
            .FirstOrDefaultAsync(b => b.PlayerId == playerId, ct);
        if (balance == null)
            return null;

        var poolOverview = await marketQuery.GetPoolOverviewAsync(playerId, locale, ct);
        var poolValue = poolOverview?.TotalEstimatedValue ?? 0m;

        var machineTypes = await db.PlayerMachineStocks.AsNoTracking()
            .Where(m => m.PlayerId == playerId)
            .Select(m => m.MachineType)
            .ToListAsync(ct);
        var machineValue = machineTypes.Sum(t => MachineStoreCatalog.TryGetEntry(t)?.Price ?? 0m);

        var cash = balance.Cash;
        var totalNow = cash + poolValue + machineValue;

        var history = await BuildHistoryAsync(playerId, cash, poolValue, machineValue, ct);
        var current = history.Count > 0 ? history[^1].TotalValue : totalNow;
        var now = DateTimeOffset.UtcNow;

        var changes = new PlayerEconomyPeriodChangesDto(
            PercentChange(current, ValueAtOrBefore(history, now - TimeSpan.FromDays(1))),
            PercentChange(current, ValueAtOrBefore(history, now - TimeSpan.FromDays(7))),
            PercentChange(current, ValueAtOrBefore(history, now - TimeSpan.FromDays(30))),
            PercentChange(current, ValueAtOrBefore(history, now - TimeSpan.FromDays(365))),
            PercentChange(current, history.Count > 0 ? history[0].TotalValue : null));

        return new PlayerEconomyOverviewDto(
            cash,
            poolValue,
            machineValue,
            totalNow,
            history,
            changes);
    }

    private async Task<IReadOnlyList<PlayerEconomyHistoryPointDto>> BuildHistoryAsync(
        Guid playerId,
        decimal cashNow,
        decimal poolValueNow,
        decimal machineValueNow,
        CancellationToken ct)
    {
        var transactions = (await db.EconomyTransactions.AsNoTracking()
                .Where(t => t.PlayerId == playerId)
                .ToListAsync(ct))
            .OrderBy(t => t.CreatedAt)
            .ThenBy(t => t.Id)
            .ToList();

        if (transactions.Count == 0)
        {
            var nowOnly = DateTimeOffset.UtcNow;
            return
            [
                new PlayerEconomyHistoryPointDto(nowOnly, cashNow + poolValueNow + machineValueNow, cashNow,
                    poolValueNow + machineValueNow)
            ];
        }

        var tradeIds = new List<Guid>();
        foreach (var tx in transactions.Where(t => t.Type is "MarketBuy" or "MarketSell" or "MarketTrade"))
        {
            if (Guid.TryParse(tx.Metadata, out var tradeId))
                tradeIds.Add(tradeId);
        }

        var trades = tradeIds.Count == 0
            ? new Dictionary<Guid, TradeExecutionEntity>()
            : await db.TradeExecutions.AsNoTracking()
                .Where(t => tradeIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, ct);

        var candleKeys = CollectCandleKeys(transactions, trades);
        var candlePrices = await LoadCandlePricesAsync(candleKeys, ct);

        var holdings = new Dictionary<(int ElementId, long Dna), long>();
        var cash = 0m;
        var machineAssets = 0m;
        var points = new List<PlayerEconomyHistoryPointDto>();

        foreach (var tx in transactions)
        {
            ApplyTransaction(tx, playerId, trades, holdings, ref cash, ref machineAssets);

            var poolAssets = ValueHoldings(holdings, candlePrices, tx.CreatedAt);
            var total = cash + poolAssets + machineAssets;
            points.Add(new PlayerEconomyHistoryPointDto(
                tx.CreatedAt,
                Math.Round(total, 2),
                Math.Round(cash, 2),
                Math.Round(poolAssets + machineAssets, 2)));
        }

        var now = DateTimeOffset.UtcNow;
        var last = points[^1];
        var assetsNow = poolValueNow + machineValueNow;
        if (last.At < now.AddMinutes(-1)
            || Math.Abs(last.TotalValue - (cashNow + assetsNow)) > 0.01m
            || Math.Abs(last.Cash - cashNow) > 0.01m)
        {
            points.Add(new PlayerEconomyHistoryPointDto(
                now,
                Math.Round(cashNow + assetsNow, 2),
                Math.Round(cashNow, 2),
                Math.Round(assetsNow, 2)));
        }

        return Downsample(points);
    }

    private HashSet<(int ElementId, long Dna)> CollectCandleKeys(
        IReadOnlyList<EconomyTransactionEntity> transactions,
        IReadOnlyDictionary<Guid, TradeExecutionEntity> trades)
    {
        var keys = new HashSet<(int, long)>();
        foreach (var elementId in _economy.GetStartingElementIds())
            keys.Add((elementId, ElementCatalogLookup.CatalogDnaFor(elementId)));

        foreach (var tx in transactions)
        {
            if (Guid.TryParse(tx.Metadata, out var tradeId) && trades.TryGetValue(tradeId, out var trade))
                keys.Add((trade.ElementId, trade.Dna));
        }

        return keys;
    }

    private async Task<Dictionary<(int ElementId, long Dna), List<(DateTimeOffset BucketStart, decimal Close)>>> LoadCandlePricesAsync(
        IEnumerable<(int ElementId, long Dna)> keys,
        CancellationToken ct)
    {
        var keyList = keys.Distinct().ToList();
        if (keyList.Count == 0)
            return new Dictionary<(int, long), List<(DateTimeOffset, decimal)>>();

        var elementIds = keyList.Select(k => k.ElementId).Distinct().ToList();
        var rows = (await db.MarketPriceCandles.AsNoTracking()
                .Where(c => elementIds.Contains(c.ElementId))
                .Select(c => new { c.ElementId, c.Dna, c.BucketStart, c.Close })
                .ToListAsync(ct))
            .OrderBy(c => c.BucketStart)
            .ToList();

        var allowed = keyList.ToHashSet();
        var map = new Dictionary<(int, long), List<(DateTimeOffset, decimal)>>();
        foreach (var row in rows)
        {
            var key = (row.ElementId, row.Dna);
            if (!allowed.Contains(key))
                continue;

            if (!map.TryGetValue(key, out var list))
            {
                list = [];
                map[key] = list;
            }

            list.Add((row.BucketStart, row.Close));
        }

        return map;
    }

    private void ApplyTransaction(
        EconomyTransactionEntity tx,
        Guid playerId,
        IReadOnlyDictionary<Guid, TradeExecutionEntity> trades,
        Dictionary<(int ElementId, long Dna), long> holdings,
        ref decimal cash,
        ref decimal machineAssets)
    {
        cash += tx.CashDelta;

        switch (tx.Type)
        {
            case "StarterPool":
                foreach (var part in (tx.Metadata ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!int.TryParse(part, out var elementId))
                        continue;
                    if (!ElementCatalog.All.Any(e => e.Id == elementId))
                        continue;

                    var dna = ElementCatalogLookup.CatalogDnaFor(elementId);
                    AddHoldings(holdings, elementId, dna, _economy.StartingElementQuantityPerStack);
                }

                break;

            case "MarketBuy":
            case "MarketSell":
            case "MarketTrade":
                if (!Guid.TryParse(tx.Metadata, out var tradeId) || !trades.TryGetValue(tradeId, out var trade))
                    break;

                if (tx.Type is "MarketBuy" or "MarketTrade" && trade.BuyerPlayerId == playerId)
                    AddHoldings(holdings, trade.ElementId, trade.Dna, trade.Quantity);
                else if (tx.Type == "MarketSell" && trade.SellerPlayerId == playerId)
                    RemoveHoldings(holdings, trade.ElementId, trade.Dna, trade.Quantity);

                break;

            case "MachinePurchase":
                if (!string.IsNullOrWhiteSpace(tx.Metadata))
                {
                    var entry = MachineStoreCatalog.TryGetEntry(tx.Metadata.Trim());
                    if (entry != null)
                        machineAssets += entry.Price;
                }

                break;
        }
    }

    private static void AddHoldings(
        Dictionary<(int ElementId, long Dna), long> holdings,
        int elementId,
        long dna,
        long qty)
    {
        var key = (elementId, dna);
        holdings[key] = holdings.GetValueOrDefault(key) + qty;
    }

    private static void RemoveHoldings(
        Dictionary<(int ElementId, long Dna), long> holdings,
        int elementId,
        long dna,
        long qty)
    {
        var key = (elementId, dna);
        if (!holdings.TryGetValue(key, out var current))
            return;

        var next = current - qty;
        if (next <= 0)
            holdings.Remove(key);
        else
            holdings[key] = next;
    }

    private static decimal ValueHoldings(
        IReadOnlyDictionary<(int ElementId, long Dna), long> holdings,
        IReadOnlyDictionary<(int ElementId, long Dna), List<(DateTimeOffset BucketStart, decimal Close)>> candlePrices,
        DateTimeOffset at)
    {
        decimal total = 0;
        foreach (var ((elementId, dna), qty) in holdings)
        {
            if (qty <= 0)
                continue;

            var price = PriceAt(candlePrices, elementId, dna, at);
            total += price * qty;
        }

        return total;
    }

    private static decimal PriceAt(
        IReadOnlyDictionary<(int ElementId, long Dna), List<(DateTimeOffset BucketStart, decimal Close)>> candlePrices,
        int elementId,
        long dna,
        DateTimeOffset at)
    {
        if (!candlePrices.TryGetValue((elementId, dna), out var candles) || candles.Count == 0)
            return ElementReferencePrice.Compute(dna);

        var idx = candles.BinarySearch(
            (at, 0m),
            Comparer<(DateTimeOffset BucketStart, decimal Close)>.Create((a, b) =>
                a.BucketStart.CompareTo(b.BucketStart)));

        if (idx >= 0)
            return candles[idx].Close;

        var insert = ~idx;
        if (insert == 0)
            return candles[0].Close;

        return candles[insert - 1].Close;
    }

    private static decimal? ValueAtOrBefore(
        IReadOnlyList<PlayerEconomyHistoryPointDto> history,
        DateTimeOffset at)
    {
        for (var i = history.Count - 1; i >= 0; i--)
        {
            if (history[i].At <= at)
                return history[i].TotalValue;
        }

        return history.Count > 0 ? history[0].TotalValue : null;
    }

    private static decimal? PercentChange(decimal current, decimal? baseline)
    {
        if (baseline is not > 0)
            return null;

        return Math.Round((current - baseline.Value) / baseline.Value * 100m, 2);
    }

    private static IReadOnlyList<PlayerEconomyHistoryPointDto> Downsample(
        IReadOnlyList<PlayerEconomyHistoryPointDto> points)
    {
        if (points.Count <= MaxHistoryPoints)
            return points;

        var result = new List<PlayerEconomyHistoryPointDto>(MaxHistoryPoints);
        var step = (points.Count - 1) / (double)(MaxHistoryPoints - 1);
        for (var i = 0; i < MaxHistoryPoints; i++)
        {
            var index = (int)Math.Round(i * step);
            index = Math.Clamp(index, 0, points.Count - 1);
            result.Add(points[index]);
        }

        if (result[^1] != points[^1])
            result[^1] = points[^1];

        return result;
    }
}
