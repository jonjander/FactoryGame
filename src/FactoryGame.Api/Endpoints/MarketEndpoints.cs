using FactoryGame.Contracts.Market;
using FactoryGame.Domain.Content;
using FactoryGame.Domain.Market;
using FactoryGame.Infrastructure.Data;
using FactoryGame.Infrastructure.Options;
using FactoryGame.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FactoryGame.Api.Endpoints;

public static class MarketEndpoints
{
    public static void MapMarketEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/v1/market").WithTags("Market");

        group.MapGet("/summary", async Task<IResult> (
                HttpContext http,
                PlayerPoolBootstrapService poolBootstrap,
                IServiceScopeFactory scopeFactory,
                IOptions<MarketLiquidityOptions> liquidityOptions,
                MarketQueryService query,
                CancellationToken ct) =>
            {
                if (http.Items["PlayerId"] is not Guid playerId)
                    return Results.Unauthorized();

                await poolBootstrap.EnsureStarterPoolAsync(playerId, ct);

                if (liquidityOptions.Value.RefreshOnSummaryRequest)
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var liquidity = scope.ServiceProvider.GetRequiredService<MarketLiquidityService>();
                    await liquidity.EnsureLiquidityForPlayerPoolAsync(playerId, ct);
                }

                var locale = http.Request.Headers.AcceptLanguage.ToString().Split(',')[0].Trim();
                if (string.IsNullOrEmpty(locale))
                    locale = "sv";
                var rows = await query.GetSummaryForPlayerAsync(playerId, locale, ct);
                var dtos = rows.Select(r => new MarketElementSummaryDto(
                    r.ElementId,
                    r.DisplayName,
                    r.PoolQuantity,
                    r.LastPrice,
                    r.ChangePercent24h,
                    r.BestBid,
                    r.BestAsk)).ToList();
                return Results.Ok(dtos);
            })
            .WithName("MarketSummary")
            .WithOpenApi();

        group.MapGet("/elements/{elementId:int}/depth", async Task<IResult> (
                int elementId,
                AppDbContext db,
                IServiceScopeFactory scopeFactory,
                MarketQueryService query,
                CancellationToken ct) =>
            {
                if (!await IsTradeableElementAsync(elementId, db, ct))
                    return Results.NotFound();

                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var liquidity = scope.ServiceProvider.GetRequiredService<MarketLiquidityService>();
                    await liquidity.EnsureLiquidityForElementAsync(elementId, ct);
                }
                catch
                {
                    // Returnera befintligt djup även om syntetisk likviditet inte kunde uppdateras.
                }

                var depth = await query.GetDepthAsync(elementId, ct);
                var dto = new MarketDepthDto(
                    depth.ElementId,
                    depth.BestBid,
                    depth.BestAsk,
                    depth.Levels.Select(l => new MarketDepthLevelDto(l.Price, l.BidQuantity, l.AskQuantity)).ToList());
                return Results.Ok(dto);
            })
            .WithName("MarketDepth")
            .WithOpenApi();

        group.MapGet("/elements/{elementId:int}/history", async Task<IResult> (
                int elementId,
                int? points,
                AppDbContext db,
                MarketQueryService query,
                CancellationToken ct) =>
            {
                if (!await IsTradeableElementAsync(elementId, db, ct))
                    return Results.NotFound();

                var history = await query.GetHistoryAsync(elementId, points ?? 48, ct);
                var dtos = history.Select(c => new MarketCandleDto(
                    c.BucketStart, c.Open, c.High, c.Low, c.Close, c.Volume)).ToList();
                return Results.Ok(dtos);
            })
            .WithName("MarketHistory")
            .WithOpenApi();

        group.MapPost("/orders", async Task<IResult> (
                HttpContext http,
                PlaceOrderRequest request,
                ExchangeService exchange,
                CancellationToken ct) =>
            {
                if (http.Items["PlayerId"] is not Guid playerId)
                    return Results.Unauthorized();
                try
                {
                    var result = await exchange.PlaceOrderAsync(playerId, request, ct);
                    return Results.Ok(result);
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.Conflict(new { error = ex.Message });
                }
            })
            .WithName("PlaceOrder")
            .WithOpenApi();

        group.MapGet("/orders/mine", async Task<IResult> (
                HttpContext http,
                int? elementId,
                AppDbContext db,
                CancellationToken ct) =>
            {
                if (http.Items["PlayerId"] is not Guid playerId)
                    return Results.Unauthorized();

                var q = db.MarketOrders.AsNoTracking()
                    .Where(o => o.PlayerId == playerId && !o.IsSynthetic && o.Status == OrderStatus.Open && o.QuantityRemaining > 0);
                if (elementId is { } e)
                    q = q.Where(o => o.ElementId == e);

                var list = await q
                    .OrderByDescending(o => o.CreatedAt)
                    .Select(o => new MyOpenOrderDto(
                        o.Id,
                        o.ElementId,
                        o.Side.ToString(),
                        o.LimitPrice ?? 0m,
                        o.QuantityRemaining,
                        o.OriginalQuantity,
                        o.CreatedAt))
                    .ToListAsync(ct);
                return Results.Ok(list);
            })
            .WithName("MyOpenOrders")
            .WithOpenApi();

        group.MapGet("/orders/open", async Task<IResult> (int? elementId, AppDbContext db, CancellationToken ct) =>
            {
                var q = db.MarketOrders.AsNoTracking().Where(o => o.Status == OrderStatus.Open);
                if (elementId is { } e)
                    q = q.Where(o => o.ElementId == e);
                var list = await q
                    .OrderBy(o => o.ElementId)
                    .ThenBy(o => o.Side)
                    .ThenBy(o => o.LimitPrice)
                    .Select(o => new
                    {
                        o.Id,
                        o.PlayerId,
                        o.ElementId,
                        side = o.Side.ToString(),
                        o.LimitPrice,
                        o.QuantityRemaining,
                        o.CreatedAt,
                        o.IsSynthetic
                    })
                    .ToListAsync(ct);
                return Results.Ok(list);
            })
            .WithName("ListOpenOrders")
            .WithOpenApi();

        group.MapGet("/trades", async Task<IResult> (
                int? elementId,
                int? limit,
                bool? includeSynthetic,
                MarketQueryService query,
                CancellationToken ct) =>
            {
                var rows = await query.GetRecentTradesAsync(
                    elementId,
                    limit ?? 50,
                    includeSynthetic ?? false,
                    highlightPlayerId: null,
                    ct);
                var dtos = rows.Select(t => new MarketTradeDto(t.Id, t.ElementId, t.Price, t.Quantity, t.CreatedAt)).ToList();
                return Results.Ok(dtos);
            })
            .WithName("RecentTrades")
            .WithOpenApi();
    }

    private static async Task<bool> IsTradeableElementAsync(int elementId, AppDbContext db, CancellationToken ct)
    {
        if (!ElementCatalog.All.Any(e => e.Id == elementId))
            return false;

        return await db.PoolStacks.AnyAsync(s => s.ElementId == elementId && s.Quantity > 0, ct)
            || await db.MarketPriceCandles.AnyAsync(c => c.ElementId == elementId, ct)
            || await db.MarketOrders.AnyAsync(
                o => o.ElementId == elementId && o.Status == OrderStatus.Open, ct);
    }
}
