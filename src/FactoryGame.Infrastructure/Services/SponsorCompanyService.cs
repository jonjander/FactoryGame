using FactoryGame.Contracts.Admin;
using FactoryGame.Domain.Content;
using FactoryGame.Domain.Market;
using FactoryGame.Infrastructure.Data;
using FactoryGame.Infrastructure.Data.Entities;
using FactoryGame.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FactoryGame.Infrastructure.Services;

public sealed class SponsorCompanyService(AppDbContext db, IOptions<SponsorCompanyOptions> options)
{
    private readonly SponsorCompanyOptions _opts = options.Value;

    public async Task<IReadOnlyList<SponsorCompanyDto>> ListAsync(CancellationToken ct = default)
    {
        var companies = await db.SponsorCompanies.AsNoTracking()
            .OrderByDescending(c => c.CreatedAt.UtcDateTime.Ticks)
            .ToListAsync(ct);
        var result = new List<SponsorCompanyDto>();
        foreach (var c in companies)
            result.Add(await MapCompanyAsync(c, ct));
        return result;
    }

    public async Task<SponsorCompanyDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var company = await db.SponsorCompanies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        return company == null ? null : await MapCompanyAsync(company, ct);
    }

    public async Task<SponsorCompanyDto> CreateAsync(CreateSponsorCompanyRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Name is required.");

        var fundingMode = ParseFundingMode(request.FundingMode);
        var tier = Math.Clamp(request.ExposureTier, 1, 5);
        var now = DateTimeOffset.UtcNow;
        var playerId = Guid.NewGuid();
        var budget = fundingMode == SponsorFundingMode.Budget
            ? request.TotalBudget ?? 100_000m
            : (decimal?)null;

        var player = new PlayerEntity
        {
            Id = playerId,
            GuestDeviceKeyHash = null,
            CreatedAt = now,
            IsSponsorAccount = true,
            Balance = new PlayerBalanceEntity
            {
                PlayerId = playerId,
                Cash = fundingMode == SponsorFundingMode.Utopia
                    ? _opts.DefaultStartingCash * 100
                    : Math.Max(budget ?? 0, _opts.DefaultStartingCash)
            },
            Pool = new InventoryPoolEntity
            {
                PlayerId = playerId,
                MaxVolume = _opts.DefaultPoolMaxVolume,
                UsedVolume = 0
            }
        };
        db.Players.Add(player);

        var company = new SponsorCompanyEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim() ?? "",
            LogoUrl = request.LogoUrl?.Trim() ?? "",
            PlayerId = playerId,
            IsActive = request.IsActive,
            FundingMode = fundingMode,
            BudgetRemaining = budget,
            TotalBudget = budget,
            VirtualSpend = 0,
            ExposureTier = tier,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.SponsorCompanies.Add(company);
        await db.SaveChangesAsync(ct);
        return await MapCompanyAsync(company, ct);
    }

    public async Task<SponsorCompanyDto> UpdateAsync(Guid id, UpdateSponsorCompanyRequest request, CancellationToken ct = default)
    {
        var company = await db.SponsorCompanies.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new InvalidOperationException("Company not found.");

        if (request.Name is { } name && !string.IsNullOrWhiteSpace(name))
            company.Name = name.Trim();
        if (request.Description is not null)
            company.Description = request.Description.Trim();
        if (request.LogoUrl is not null)
            company.LogoUrl = request.LogoUrl.Trim();
        if (request.FundingMode is { } fm)
            company.FundingMode = ParseFundingMode(fm);
        if (request.TotalBudget is { } total)
        {
            company.TotalBudget = total;
            if (company.BudgetRemaining is null || company.BudgetRemaining > total)
                company.BudgetRemaining = total;
        }
        if (request.BudgetRemaining is { } remaining)
            company.BudgetRemaining = remaining;
        if (request.ExposureTier is { } tier)
            company.ExposureTier = Math.Clamp(tier, 1, 5);
        if (request.IsActive is { } active)
            company.IsActive = active;

        company.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return await MapCompanyAsync(company, ct);
    }

    public async Task<IReadOnlyList<SponsorCompanyOrderDto>> ListOrdersAsync(Guid companyId, CancellationToken ct = default)
    {
        _ = await db.SponsorCompanies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == companyId, ct)
            ?? throw new InvalidOperationException("Company not found.");

        return await db.SponsorCompanyOrders.AsNoTracking()
            .Where(o => o.SponsorCompanyId == companyId)
            .OrderByDescending(o => o.CreatedAt.UtcDateTime.Ticks)
            .Select(o => MapOrder(o))
            .ToListAsync(ct);
    }

    public async Task<SponsorCompanyOrderDto> CreateOrderAsync(
        Guid companyId,
        CreateSponsorCompanyOrderRequest request,
        CancellationToken ct = default)
    {
        var company = await db.SponsorCompanies.FirstOrDefaultAsync(c => c.Id == companyId, ct)
            ?? throw new InvalidOperationException("Company not found.");

        ValidateOrderRequest(request.ElementId, request.Dna, request.Side, request.LimitPrice, request.TargetQuantity);

        var now = DateTimeOffset.UtcNow;
        var order = new SponsorCompanyOrderEntity
        {
            Id = Guid.NewGuid(),
            SponsorCompanyId = company.Id,
            ElementId = request.ElementId,
            Dna = ResolveDna(request.ElementId, request.Dna),
            Side = ParseSide(request.Side),
            LimitPrice = request.LimitPrice,
            TargetQuantity = request.TargetQuantity,
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.SponsorCompanyOrders.Add(order);
        company.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return MapOrder(order);
    }

    public async Task<SponsorCompanyOrderDto> UpdateOrderAsync(
        Guid companyId,
        Guid orderId,
        UpdateSponsorCompanyOrderRequest request,
        CancellationToken ct = default)
    {
        var order = await db.SponsorCompanyOrders.FirstOrDefaultAsync(
            o => o.Id == orderId && o.SponsorCompanyId == companyId, ct)
            ?? throw new InvalidOperationException("Standing order not found.");

        if (request.ElementId is { } elementId)
            order.ElementId = elementId;
        if (request.Dna is { } dna)
            order.Dna = ResolveDna(order.ElementId, dna);
        if (request.Side is { } side)
            order.Side = ParseSide(side);
        if (request.LimitPrice is { } price)
        {
            if (price <= 0)
                throw new ArgumentException("Limit price must be positive.");
            order.LimitPrice = price;
        }
        if (request.TargetQuantity is { } qty)
        {
            if (qty <= 0)
                throw new ArgumentException("Target quantity must be positive.");
            order.TargetQuantity = qty;
        }
        if (request.IsActive is { } active)
            order.IsActive = active;

        order.UpdatedAt = DateTimeOffset.UtcNow;
        order.LinkedMarketOrderId = null;
        await db.SaveChangesAsync(ct);
        return MapOrder(order);
    }

    public async Task DeleteOrderAsync(Guid companyId, Guid orderId, CancellationToken ct = default)
    {
        var order = await db.SponsorCompanyOrders.FirstOrDefaultAsync(
            o => o.Id == orderId && o.SponsorCompanyId == companyId, ct)
            ?? throw new InvalidOperationException("Standing order not found.");

        db.SponsorCompanyOrders.Remove(order);
        await db.SaveChangesAsync(ct);
    }

    public async Task<SponsorCompanyStatsDto> GetStatsAsync(Guid companyId, CancellationToken ct = default)
    {
        var company = await db.SponsorCompanies.AsNoTracking().FirstAsync(c => c.Id == companyId, ct);
        return await BuildStatsAsync(company, ct);
    }

    private async Task<SponsorCompanyDto> MapCompanyAsync(SponsorCompanyEntity company, CancellationToken ct)
    {
        var stats = await BuildStatsAsync(company, ct);
        return new SponsorCompanyDto(
            company.Id,
            company.Name,
            company.Description,
            company.LogoUrl,
            company.PlayerId,
            company.IsActive,
            company.FundingMode.ToString(),
            company.BudgetRemaining,
            company.TotalBudget,
            company.VirtualSpend,
            company.ExposureTier,
            company.CreatedAt,
            company.UpdatedAt,
            stats);
    }

    private async Task<SponsorCompanyStatsDto> BuildStatsAsync(SponsorCompanyEntity company, CancellationToken ct)
    {
        var trades = await db.TradeExecutions.AsNoTracking()
            .Where(t => !t.IsSynthetic && (t.BuyerSponsorCompanyId == company.Id || t.SellerSponsorCompanyId == company.Id))
            .ToListAsync(ct);

        var spend = company.FundingMode == SponsorFundingMode.Utopia
            ? company.VirtualSpend
            : trades.Where(t => t.BuyerSponsorCompanyId == company.Id).Sum(t => t.Price * t.Quantity);

        return new SponsorCompanyStatsDto(
            spend,
            trades.Sum(t => t.Quantity),
            trades.Count,
            trades.Count > 0 ? trades.Max(t => t.CreatedAt) : null);
    }

    private static SponsorCompanyOrderDto MapOrder(SponsorCompanyOrderEntity o) =>
        new(
            o.Id,
            o.SponsorCompanyId,
            o.ElementId,
            o.Dna,
            o.Side.ToString(),
            o.LimitPrice,
            o.TargetQuantity,
            o.IsActive,
            o.LinkedMarketOrderId,
            o.CreatedAt,
            o.UpdatedAt);

    private static SponsorFundingMode ParseFundingMode(string mode) =>
        mode.Equals("Utopia", StringComparison.OrdinalIgnoreCase)
            ? SponsorFundingMode.Utopia
            : SponsorFundingMode.Budget;

    private static OrderSide ParseSide(string side) =>
        side.Equals("sell", StringComparison.OrdinalIgnoreCase) ? OrderSide.Sell : OrderSide.Buy;

    private static long ResolveDna(int elementId, long dna) =>
        dna != 0 ? dna : ElementCatalogLookup.CatalogDnaFor(elementId);

    private static void ValidateOrderRequest(int elementId, long dna, string side, decimal limitPrice, long targetQty)
    {
        if (!ElementCatalog.All.Any(e => e.Id == elementId))
            throw new InvalidOperationException("Unknown element.");
        if (limitPrice <= 0)
            throw new ArgumentException("Limit price must be positive.");
        if (targetQty <= 0)
            throw new ArgumentException("Target quantity must be positive.");
        _ = ParseSide(side);
        _ = ResolveDna(elementId, dna);
    }
}
