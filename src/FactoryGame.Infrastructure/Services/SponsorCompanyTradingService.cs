using FactoryGame.Contracts.Market;
using FactoryGame.Domain.Content;
using FactoryGame.Domain.Market;
using FactoryGame.Infrastructure.Data;
using FactoryGame.Infrastructure.Data.Entities;
using FactoryGame.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FactoryGame.Infrastructure.Services;

public sealed class SponsorCompanyTradingService(
    AppDbContext db,
    ExchangeService exchange,
    IOptions<SponsorCompanyOptions> options)
{
    private readonly SponsorCompanyOptions _opts = options.Value;

    public async Task RefreshAllActiveCompaniesAsync(CancellationToken ct = default)
    {
        if (!_opts.Enabled)
            return;

        var companies = await db.SponsorCompanies
            .Include(c => c.StandingOrders)
            .Where(c => c.IsActive)
            .ToListAsync(ct);

        foreach (var company in companies)
        {
            await EnsureBackingResourcesAsync(company, ct);
            foreach (var standing in company.StandingOrders.Where(o => o.IsActive))
                await MaintainStandingOrderAsync(company, standing, ct);
        }
    }

    private async Task EnsureBackingResourcesAsync(SponsorCompanyEntity company, CancellationToken ct)
    {
        var balance = await db.PlayerBalances.FirstAsync(b => b.PlayerId == company.PlayerId, ct);
        if (company.FundingMode == SponsorFundingMode.Utopia && balance.Cash < _opts.DefaultStartingCash)
            balance.Cash = _opts.DefaultStartingCash * 100;

        if (company.FundingMode == SponsorFundingMode.Budget && company.BudgetRemaining is <= 0)
            return;

        var sellOrders = company.StandingOrders.Where(o => o.IsActive && o.Side == OrderSide.Sell).ToList();
        foreach (var sell in sellOrders)
            await EnsureSellInventoryAsync(company, sell, ct);
    }

    private async Task EnsureSellInventoryAsync(
        SponsorCompanyEntity company,
        SponsorCompanyOrderEntity standing,
        CancellationToken ct)
    {
        var dna = standing.Dna != 0 ? standing.Dna : ElementCatalogLookup.CatalogDnaFor(standing.ElementId);
        var stack = await db.PoolStacks.FirstOrDefaultAsync(
            s => s.PlayerId == company.PlayerId && s.ElementId == standing.ElementId && s.Dna == dna, ct);

        var needed = standing.TargetQuantity + standing.TargetQuantity / 2;
        var have = stack?.Quantity ?? 0;
        if (have >= needed)
            return;

        var add = needed - have;
        var pool = await db.InventoryPools.FirstAsync(p => p.PlayerId == company.PlayerId, ct);
        if (pool.UsedVolume + add > pool.MaxVolume)
            add = Math.Max(0, pool.MaxVolume - pool.UsedVolume);
        if (add <= 0)
            return;

        if (stack == null)
        {
            db.PoolStacks.Add(new PoolStackEntity
            {
                Id = Guid.NewGuid(),
                PlayerId = company.PlayerId,
                ElementId = standing.ElementId,
                Dna = dna,
                Quantity = add,
                VolumePerUnit = 1
            });
        }
        else
            stack.Quantity += add;

        pool.UsedVolume += add;
        await db.SaveChangesAsync(ct);
    }

    private async Task MaintainStandingOrderAsync(
        SponsorCompanyEntity company,
        SponsorCompanyOrderEntity standing,
        CancellationToken ct)
    {
        if (standing.Side == OrderSide.Buy && company.FundingMode == SponsorFundingMode.Budget
            && company.BudgetRemaining is <= 0)
        {
            await CancelLinkedMarketOrderAsync(standing, ct);
            return;
        }

        if (await IsRateLimitedAsync(company, ct))
            return;

        var dna = standing.Dna != 0 ? standing.Dna : ElementCatalogLookup.CatalogDnaFor(standing.ElementId);
        var lot = EffectiveLotSize(company, standing.TargetQuantity);

        if (standing.LinkedMarketOrderId is { } linkedId)
        {
            var linked = await db.MarketOrders.FirstOrDefaultAsync(o => o.Id == linkedId, ct);
            if (linked != null && linked.Status == OrderStatus.Open && linked.QuantityRemaining > 0)
            {
                if (linked.LimitPrice == standing.LimitPrice && linked.QuantityRemaining >= lot / 2)
                    return;
                await CancelLinkedMarketOrderAsync(standing, linked, ct);
            }
            else
            {
                standing.LinkedMarketOrderId = null;
                standing.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }

        if (standing.Side == OrderSide.Buy && company.FundingMode == SponsorFundingMode.Budget)
        {
            var maxCost = standing.LimitPrice * lot;
            if (company.BudgetRemaining is not { } remaining || remaining < maxCost)
            {
                lot = company.BudgetRemaining is { } b && b > 0 && standing.LimitPrice > 0
                    ? Math.Min(lot, (long)Math.Floor(b / standing.LimitPrice))
                    : 0;
            }
            if (lot <= 0)
                return;
        }

        var idempotencyKey = $"sponsor-{standing.Id:N}";
        var request = new PlaceOrderRequest(
            standing.ElementId,
            dna,
            standing.Side == OrderSide.Buy ? "buy" : "sell",
            standing.LimitPrice,
            lot,
            idempotencyKey);

        var result = await exchange.PlaceSponsorOrderAsync(company.Id, request, ct);
        standing.LinkedMarketOrderId = result.OrderId;
        standing.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private long EffectiveLotSize(SponsorCompanyEntity company, long target)
    {
        var tierIndex = Math.Clamp(company.ExposureTier, 1, 5) - 1;
        var maxLot = _opts.MaxLotSizeByTier.Length > tierIndex
            ? _opts.MaxLotSizeByTier[tierIndex]
            : _opts.MaxLotSizeByTier[^1];
        return Math.Min(target, maxLot);
    }

    private async Task<bool> IsRateLimitedAsync(SponsorCompanyEntity company, CancellationToken ct)
    {
        var tierIndex = Math.Clamp(company.ExposureTier, 1, 5) - 1;
        var maxPerHour = _opts.MaxTradesPerHourByTier.Length > tierIndex
            ? _opts.MaxTradesPerHourByTier[tierIndex]
            : _opts.MaxTradesPerHourByTier[^1];
        var since = DateTimeOffset.UtcNow.AddHours(-1);
        var companyId = company.Id;
        var recent = await db.TradeExecutions.AsNoTracking()
            .Where(t => !t.IsSynthetic)
            .ToListAsync(ct);
        var count = recent.Count(t =>
            t.CreatedAt >= since
            && (t.BuyerSponsorCompanyId == companyId || t.SellerSponsorCompanyId == companyId));
        return count >= maxPerHour;
    }

    private async Task CancelLinkedMarketOrderAsync(SponsorCompanyOrderEntity standing, CancellationToken ct)
    {
        if (standing.LinkedMarketOrderId is not { } linkedId)
            return;
        var linked = await db.MarketOrders.FirstOrDefaultAsync(o => o.Id == linkedId, ct);
        if (linked != null)
            await CancelLinkedMarketOrderAsync(standing, linked, ct);
        else
            standing.LinkedMarketOrderId = null;
    }

    private async Task CancelLinkedMarketOrderAsync(
        SponsorCompanyOrderEntity standing,
        MarketOrderEntity linked,
        CancellationToken ct)
    {
        if (linked.Status == OrderStatus.Open && linked.QuantityRemaining > 0 && !linked.IsSynthetic)
        {
            var company = await db.SponsorCompanies.AsNoTracking()
                .FirstAsync(c => c.Id == standing.SponsorCompanyId, ct);
            await exchange.CancelOrderAsync(company.PlayerId, linked.Id, ct);
        }
        standing.LinkedMarketOrderId = null;
        standing.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
