using FactoryGame.Contracts.Market;
using FactoryGame.Domain.Market;
using FactoryGame.Infrastructure.Data;
using FactoryGame.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace FactoryGame.Api.Endpoints;

public static class MarketEndpoints
{
    public static void MapMarketEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/v1/market").WithTags("Market");

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
                        o.CreatedAt
                    })
                    .ToListAsync(ct);
                return Results.Ok(list);
            })
            .WithName("ListOpenOrders")
            .WithOpenApi();

        group.MapGet("/trades", async Task<IResult> (int? elementId, AppDbContext db, CancellationToken ct) =>
            {
                var q = db.TradeExecutions.AsNoTracking();
                if (elementId is { } e)
                    q = q.Where(t => t.ElementId == e);
                var list = await q.OrderByDescending(t => t.CreatedAt).Take(100).ToListAsync(ct);
                return Results.Ok(list);
            })
            .WithName("RecentTrades")
            .WithOpenApi();
    }
}
