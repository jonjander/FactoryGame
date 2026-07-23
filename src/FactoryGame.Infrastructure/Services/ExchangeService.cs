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

    private Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var result = await action(ct);
                await tx.CommitAsync(ct);
                return result;
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        });

    public async Task<PlaceOrderResponse> PlaceOrderAsync(Guid playerId, PlaceOrderRequest request, CancellationToken ct = default)

    {

        if (request.Quantity <= 0)

            throw new ArgumentException("Quantity must be positive.", nameof(request));



        if (!ElementCatalog.All.Any(e => e.Id == request.ElementId))

            throw new InvalidOperationException("Unknown element.");



        var dna = ResolveOrderDna(request.ElementId, request.Dna);



        var side = request.Side.Equals("buy", StringComparison.OrdinalIgnoreCase)

            ? OrderSide.Buy

            : request.Side.Equals("sell", StringComparison.OrdinalIgnoreCase)

                ? OrderSide.Sell

                : throw new ArgumentException("Side must be buy or sell.");



        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))

        {

            var existing = await db.MarketOrders.FirstOrDefaultAsync(

                o => o.PlayerId == playerId && o.IdempotencyKey == request.IdempotencyKey, ct);

            if (existing != null)

            {

                var filled = existing.OriginalQuantity - existing.QuantityRemaining;

                return new PlaceOrderResponse(

                    existing.Id,

                    existing.QuantityRemaining,

                    existing.Status.ToString(),

                    filled,

                    await GetAverageFillPriceAsync(existing.Id, ct),

                    existing.OriginalQuantity);

            }

        }



        return await ExecuteInTransactionAsync(async ctInner =>
        {
            if (side == OrderSide.Sell)
            {
                await RemoveFromPoolAsync(playerId, request.ElementId, dna, request.Quantity, ctInner);
            }
            else
            {
                var cost = request.LimitPrice * request.Quantity;
                var balance = await db.PlayerBalances.FirstAsync(b => b.PlayerId == playerId, ctInner);
                if (balance.Cash < cost)
                    throw new InvalidOperationException("Insufficient cash for buy.");
                var pool = await db.InventoryPools.FirstAsync(p => p.PlayerId == playerId, ctInner);
                if (pool.UsedVolume + request.Quantity * VolumePerUnit > pool.MaxVolume)
                    throw new InvalidOperationException("Pool volume would exceed max; buy blocked.");
            }

            var order = new MarketOrderEntity
            {
                Id = Guid.NewGuid(),
                PlayerId = playerId,
                ElementId = request.ElementId,
                Dna = dna,
                Side = side,
                LimitPrice = request.LimitPrice,
                QuantityRemaining = request.Quantity,
                OriginalQuantity = request.Quantity,
                Status = OrderStatus.Open,
                CreatedAt = DateTimeOffset.UtcNow,
                IdempotencyKey = request.IdempotencyKey
            };
            db.MarketOrders.Add(order);
            await db.SaveChangesAsync(ctInner);

            await MatchOrdersForVariantAsync(request.ElementId, dna, ctInner);

            await db.SaveChangesAsync(ctInner);

            var updated = await db.MarketOrders.AsNoTracking()
                .FirstAsync(o => o.Id == order.Id, ctInner);
            var quantityFilled = updated.OriginalQuantity - updated.QuantityRemaining;
            return new PlaceOrderResponse(
                updated.Id,
                updated.QuantityRemaining,
                updated.Status.ToString(),
                quantityFilled,
                quantityFilled > 0
                    ? await GetAverageFillPriceAsync(updated.Id, ctInner)
                    : null,
                updated.OriginalQuantity);
        }, ct);
    }



    private static long ResolveOrderDna(int elementId, long dna) =>

        dna != 0 ? dna : ElementCatalogLookup.CatalogDnaFor(elementId);



    private async Task MatchOrdersForVariantAsync(int elementId, long dna, CancellationToken ct)

    {

        var progressed = true;

        var guard = 0;

        while (progressed && guard++ < 500)

        {

            progressed = false;

            var buys = (await db.MarketOrders

                .Where(o => o.ElementId == elementId && o.Dna == dna && o.Side == OrderSide.Buy

                            && o.Status == OrderStatus.Open && o.QuantityRemaining > 0)

                .ToListAsync(ct))

                .OrderByDescending(o => o.LimitPrice)

                .ThenBy(o => o.CreatedAt)

                .ToList();



            foreach (var buy in buys)

            {

                var skipSellIds = new HashSet<Guid>();

                var innerGuard = 0;

                while (buy.QuantityRemaining > 0 && buy.Status == OrderStatus.Open && innerGuard++ < 1000)

                {

                    var sellCandidates = await db.MarketOrders

                        .Where(o => o.ElementId == elementId && o.Dna == dna && o.Side == OrderSide.Sell

                                    && o.Status == OrderStatus.Open && o.QuantityRemaining > 0

                                    && o.LimitPrice <= buy.LimitPrice)

                        .ToListAsync(ct);

                    var sell = sellCandidates

                        .Where(o => !skipSellIds.Contains(o.Id))

                        .OrderBy(o => o.LimitPrice)

                        .ThenBy(o => SellCandidatePriority(o))

                        .ThenBy(o => o.CreatedAt)

                        .FirstOrDefault();



                    if (sell == null)

                        break;



                    if (buy.IsSynthetic && sell.IsSynthetic)

                        break;



                    if (buy.SponsorCompanyId.HasValue && sell.SponsorCompanyId.HasValue)

                    {

                        skipSellIds.Add(sell.Id);

                        continue;

                    }



                    var qty = Math.Min(buy.QuantityRemaining, sell.QuantityRemaining);

                    var price = sell.LimitPrice!.Value;



                    var ok = await TryExecuteTradeAsync(buy, sell, price, qty, ct);

                    if (!ok)

                    {

                        skipSellIds.Add(sell.Id);

                        continue;

                    }



                    progressed = true;



                    if (buy.QuantityRemaining == 0)

                        buy.Status = OrderStatus.Filled;

                    if (sell.QuantityRemaining == 0)

                        sell.Status = OrderStatus.Filled;



                    // Re-query sees stale rows until pending changes are flushed (EF + SQLite in one tx).

                    await db.SaveChangesAsync(ct);

                }

            }

        }

    }



    private async Task<bool> TryExecuteTradeAsync(MarketOrderEntity buy, MarketOrderEntity sell, decimal price, long qty, CancellationToken ct)

    {

        var buyerBalance = await db.PlayerBalances.FirstAsync(b => b.PlayerId == buy.PlayerId, ct);

        var sellerBalance = await db.PlayerBalances.FirstAsync(b => b.PlayerId == sell.PlayerId, ct);

        var total = price * qty;



        SponsorCompanyEntity? buyerSponsor = null;

        SponsorCompanyEntity? sellerSponsor = null;

        if (buy.SponsorCompanyId is { } buyerSponsorId)

            buyerSponsor = await db.SponsorCompanies.FirstOrDefaultAsync(c => c.Id == buyerSponsorId, ct);

        if (sell.SponsorCompanyId is { } sellerSponsorId)

            sellerSponsor = await db.SponsorCompanies.FirstOrDefaultAsync(c => c.Id == sellerSponsorId, ct);



        if (buyerSponsor?.FundingMode == SponsorFundingMode.Budget

            && (buyerSponsor.BudgetRemaining is not { } budgetRemaining || budgetRemaining < total))

            return false;



        if (buyerSponsor?.FundingMode != SponsorFundingMode.Utopia && buyerBalance.Cash < total)

            return false;



        if (buyerSponsor?.FundingMode == SponsorFundingMode.Utopia && buyerBalance.Cash < total)

            buyerBalance.Cash = total;



        var buyerPool = await db.InventoryPools.FirstAsync(p => p.PlayerId == buy.PlayerId, ct);

        var addVol = qty * VolumePerUnit;

        if (buyerPool.UsedVolume + addVol > buyerPool.MaxVolume)

            return false;



        buyerBalance.Cash -= total;

        sellerBalance.Cash += total;



        if (buyerSponsor?.FundingMode == SponsorFundingMode.Budget && buyerSponsor.BudgetRemaining is { } br)

            buyerSponsor.BudgetRemaining = br - total;

        if (buyerSponsor?.FundingMode == SponsorFundingMode.Utopia)

            buyerSponsor.VirtualSpend += total;



        await AddToBuyerPoolAsync(buy.PlayerId, buy.ElementId, sell.Dna, qty, ct);



        buy.QuantityRemaining -= qty;

        sell.QuantityRemaining -= qty;



        var tradeId = Guid.NewGuid();

        db.TradeExecutions.Add(new TradeExecutionEntity

        {

            Id = tradeId,

            ElementId = buy.ElementId,

            Dna = sell.Dna,

            Price = price,

            Quantity = qty,

            BuyerPlayerId = buy.PlayerId,

            SellerPlayerId = sell.PlayerId,

            BuyOrderId = buy.Id,

            SellOrderId = sell.Id,

            CreatedAt = DateTimeOffset.UtcNow,

            IsSynthetic = buy.IsSynthetic && sell.IsSynthetic,

            BuyerSponsorCompanyId = buy.SponsorCompanyId,

            SellerSponsorCompanyId = sell.SponsorCompanyId

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



    private async Task RemoveFromPoolAsync(Guid playerId, int elementId, long dna, long qty, CancellationToken ct)

    {

        var stack = await db.PoolStacks.FirstOrDefaultAsync(

            s => s.PlayerId == playerId && s.ElementId == elementId && s.Dna == dna, ct);

        if (stack == null || stack.Quantity < qty)

            throw new InvalidOperationException("Insufficient quantity in pool for sell.");



        stack.Quantity -= qty;

        var pool = await db.InventoryPools.FirstAsync(p => p.PlayerId == playerId, ct);

        pool.UsedVolume -= qty * VolumePerUnit;

        if (stack.Quantity < 0)
            stack.Quantity = 0;

    }



    private async Task AddToBuyerPoolAsync(Guid buyerId, int elementId, long dna, long qty, CancellationToken ct)

    {

        var toStack = await db.PoolStacks.FirstOrDefaultAsync(

            s => s.PlayerId == buyerId && s.ElementId == elementId && s.Dna == dna, ct);

        var toPool = await db.InventoryPools.FirstAsync(p => p.PlayerId == buyerId, ct);

        if (toStack == null)

        {

            db.PoolStacks.Add(new PoolStackEntity

            {

                Id = Guid.NewGuid(),

                PlayerId = buyerId,

                ElementId = elementId,

                Dna = dna,

                Quantity = qty,

                VolumePerUnit = VolumePerUnit

            });

        }

        else

            toStack.Quantity += qty;



        toPool.UsedVolume += qty * VolumePerUnit;

    }



    private async Task<decimal?> GetAverageFillPriceAsync(Guid orderId, CancellationToken ct)

    {

        var trades = await db.TradeExecutions.AsNoTracking()

            .Where(t => t.BuyOrderId == orderId || t.SellOrderId == orderId)

            .Select(t => new { t.Price, t.Quantity })

            .ToListAsync(ct);

        if (trades.Count == 0)

            return null;



        var totalQty = trades.Sum(t => t.Quantity);

        if (totalQty == 0)

            return null;



        return trades.Sum(t => t.Price * t.Quantity) / totalQty;

    }



    public async Task<long> GetPoolQuantityAsync(Guid playerId, int elementId, long dna, CancellationToken ct) =>

        await db.PoolStacks.AsNoTracking()

            .Where(s => s.PlayerId == playerId && s.ElementId == elementId && s.Dna == dna)

            .Select(s => s.Quantity)

            .FirstOrDefaultAsync(ct);



    public async Task<OrderActionResponse> CancelOrderAsync(Guid playerId, Guid orderId, CancellationToken ct = default)
    {
        return await ExecuteInTransactionAsync(async ctInner =>
        {
            var order = await db.MarketOrders.FirstOrDefaultAsync(
                o => o.Id == orderId && o.PlayerId == playerId, ctInner);
            if (order == null)
                throw new InvalidOperationException("Order not found.");
            if (order.IsSynthetic)
                throw new InvalidOperationException("Cannot cancel synthetic order.");
            if (order.Status != OrderStatus.Open || order.QuantityRemaining <= 0)
                throw new InvalidOperationException("Order is not open.");

            var remaining = order.QuantityRemaining;
            if (order.Side == OrderSide.Sell && remaining > 0)
                await AddToBuyerPoolAsync(order.PlayerId, order.ElementId, order.Dna, remaining, ctInner);

            order.Status = OrderStatus.Cancelled;
            order.QuantityRemaining = 0;
            await db.SaveChangesAsync(ctInner);

            return new OrderActionResponse(order.Id, order.Status.ToString(), 0, order.OriginalQuantity - remaining, null, order.LimitPrice, order.OriginalQuantity);
        }, ct);
    }



    public async Task<OrderActionResponse> AmendOrderPriceAsync(Guid playerId, Guid orderId, decimal newLimitPrice, CancellationToken ct = default)
    {
        if (newLimitPrice <= 0)
            throw new ArgumentException("Limit price must be positive.", nameof(newLimitPrice));

        return await ExecuteInTransactionAsync(async ctInner =>
        {
            var order = await db.MarketOrders.FirstOrDefaultAsync(
                o => o.Id == orderId && o.PlayerId == playerId, ctInner);
            if (order == null)
                throw new InvalidOperationException("Order not found.");
            if (order.IsSynthetic)
                throw new InvalidOperationException("Cannot amend synthetic order.");
            if (order.Status != OrderStatus.Open || order.QuantityRemaining <= 0)
                throw new InvalidOperationException("Order is not open.");

            var oldLimit = order.LimitPrice ?? 0m;
            if (order.Side == OrderSide.Buy && newLimitPrice > oldLimit)
            {
                var extraCost = (newLimitPrice - oldLimit) * order.QuantityRemaining;
                var balance = await db.PlayerBalances.FirstAsync(b => b.PlayerId == playerId, ctInner);
                if (balance.Cash < extraCost)
                    throw new InvalidOperationException("Insufficient cash for higher buy limit.");
            }

            order.LimitPrice = newLimitPrice;
            await db.SaveChangesAsync(ctInner);

            await MatchOrdersForVariantAsync(order.ElementId, order.Dna, ctInner);
            await db.SaveChangesAsync(ctInner);

            var updated = await db.MarketOrders.AsNoTracking().FirstAsync(o => o.Id == orderId, ctInner);
            var filled = updated.OriginalQuantity - updated.QuantityRemaining;
            return new OrderActionResponse(
                updated.Id,
                updated.Status.ToString(),
                updated.QuantityRemaining,
                filled,
                filled > 0 ? await GetAverageFillPriceAsync(updated.Id, ctInner) : null,
                updated.LimitPrice,
                updated.OriginalQuantity);
        }, ct);
    }



    public async Task<PlaceOrderResponse> PlaceSponsorOrderAsync(

        Guid sponsorCompanyId,

        PlaceOrderRequest request,

        CancellationToken ct = default)

    {

        var company = await db.SponsorCompanies.FirstOrDefaultAsync(c => c.Id == sponsorCompanyId, ct)

            ?? throw new InvalidOperationException("Sponsor company not found.");

        if (!company.IsActive)

            throw new InvalidOperationException("Sponsor company is inactive.");



        if (request.Quantity <= 0)

            throw new ArgumentException("Quantity must be positive.", nameof(request));



        if (!ElementCatalog.All.Any(e => e.Id == request.ElementId))

            throw new InvalidOperationException("Unknown element.");



        var dna = ResolveOrderDna(request.ElementId, request.Dna);

        var side = request.Side.Equals("buy", StringComparison.OrdinalIgnoreCase)

            ? OrderSide.Buy

            : request.Side.Equals("sell", StringComparison.OrdinalIgnoreCase)

                ? OrderSide.Sell

                : throw new ArgumentException("Side must be buy or sell.");



        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))

        {

            var existing = await db.MarketOrders.FirstOrDefaultAsync(

                o => o.PlayerId == company.PlayerId && o.IdempotencyKey == request.IdempotencyKey, ct);

            if (existing != null)

            {

                var filledExisting = existing.OriginalQuantity - existing.QuantityRemaining;

                return new PlaceOrderResponse(

                    existing.Id,

                    existing.QuantityRemaining,

                    existing.Status.ToString(),

                    filledExisting,

                    await GetAverageFillPriceAsync(existing.Id, ct),

                    existing.OriginalQuantity);

            }

        }



        return await ExecuteInTransactionAsync(async ctInner =>
        {
            if (side == OrderSide.Sell)
            {
                await RemoveFromPoolAsync(company.PlayerId, request.ElementId, dna, request.Quantity, ctInner);
            }
            else
            {
                var cost = request.LimitPrice * request.Quantity;
                if (company.FundingMode == SponsorFundingMode.Budget
                    && (company.BudgetRemaining is not { } budget || budget < cost))
                    throw new InvalidOperationException("Insufficient sponsor budget for buy.");

                var balance = await db.PlayerBalances.FirstAsync(b => b.PlayerId == company.PlayerId, ctInner);
                if (company.FundingMode != SponsorFundingMode.Utopia && balance.Cash < cost)
                    throw new InvalidOperationException("Insufficient cash for buy.");

                if (company.FundingMode == SponsorFundingMode.Utopia && balance.Cash < cost)
                    balance.Cash = cost;

                var pool = await db.InventoryPools.FirstAsync(p => p.PlayerId == company.PlayerId, ctInner);
                if (pool.UsedVolume + request.Quantity * VolumePerUnit > pool.MaxVolume)
                    throw new InvalidOperationException("Pool volume would exceed max; buy blocked.");
            }

            var order = new MarketOrderEntity
            {
                Id = Guid.NewGuid(),
                PlayerId = company.PlayerId,
                SponsorCompanyId = sponsorCompanyId,
                ElementId = request.ElementId,
                Dna = dna,
                Side = side,
                LimitPrice = request.LimitPrice,
                QuantityRemaining = request.Quantity,
                OriginalQuantity = request.Quantity,
                Status = OrderStatus.Open,
                CreatedAt = DateTimeOffset.UtcNow,
                IdempotencyKey = request.IdempotencyKey
            };
            db.MarketOrders.Add(order);
            await db.SaveChangesAsync(ctInner);

            await MatchOrdersForVariantAsync(request.ElementId, dna, ctInner);
            await db.SaveChangesAsync(ctInner);

            var updated = await db.MarketOrders.AsNoTracking().FirstAsync(o => o.Id == order.Id, ctInner);
            var quantityFilled = updated.OriginalQuantity - updated.QuantityRemaining;
            return new PlaceOrderResponse(
                updated.Id,
                updated.QuantityRemaining,
                updated.Status.ToString(),
                quantityFilled,
                quantityFilled > 0 ? await GetAverageFillPriceAsync(updated.Id, ctInner) : null,
                updated.OriginalQuantity);
        }, ct);
    }



    private static int SellCandidatePriority(MarketOrderEntity order)

    {

        if (order.IsSynthetic)

            return 2;

        if (order.SponsorCompanyId.HasValue)

            return 1;

        return 0;

    }

}

