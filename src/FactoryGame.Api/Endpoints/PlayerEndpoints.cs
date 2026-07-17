using FactoryGame.Contracts.Machines;
using FactoryGame.Contracts.Player;
using FactoryGame.Infrastructure.Data;
using FactoryGame.Infrastructure.Options;
using FactoryGame.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FactoryGame.Api.Endpoints;

public static class PlayerEndpoints
{
    public static void MapPlayerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/v1").WithTags("Player");

        group.MapGet("/me/machine-inventory", async Task<IResult> (HttpContext http, MachineInventoryService inv, CancellationToken ct) =>
            {
                if (http.Items["PlayerId"] is not Guid playerId)
                    return Results.Unauthorized();
                var rows = await inv.ListStockAsync(playerId, ct);
                return Results.Ok(rows);
            })
            .WithName("GetMyMachineInventory")
            .WithOpenApi();

        group.MapPost("/me/machine-inventory/purchase", async Task<IResult> (
                HttpContext http,
                [FromBody] PurchaseMachineRequest? body,
                MachineInventoryService inv,
                CancellationToken ct) =>
            {
                if (http.Items["PlayerId"] is not Guid playerId)
                    return Results.Unauthorized();
                if (body == null || string.IsNullOrWhiteSpace(body.MachineType))
                    return Results.BadRequest(new { error = "MachineType is required." });
                try
                {
                    await inv.PurchaseAsync(playerId, body.MachineType.Trim(), ct);
                    return Results.NoContent();
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            })
            .WithName("PurchaseMachine")
            .WithOpenApi();

        group.MapGet("/me/wallet", async Task<IResult> (
                HttpContext http,
                AppDbContext db,
                PlayerPoolBootstrapService poolBootstrap,
                IOptions<GameEconomyOptions> economyOptions,
                CancellationToken ct) =>
            {
                if (http.Items["PlayerId"] is not Guid playerId)
                    return Results.Unauthorized();

                await poolBootstrap.EnsureStarterPoolAsync(playerId, ct);

                var balance = await db.PlayerBalances.AsNoTracking()
                    .FirstOrDefaultAsync(b => b.PlayerId == playerId, ct);
                if (balance == null)
                    return Results.NotFound();

                var pool = await db.InventoryPools.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.PlayerId == playerId, ct);

                var economy = economyOptions.Value;
                var baseIncomeDates = await db.EconomyTransactions.AsNoTracking()
                    .Where(t => t.PlayerId == playerId && t.Type == "BaseIncome")
                    .Select(t => t.CreatedAt)
                    .ToListAsync(ct);
                DateTimeOffset? lastBaseIncome = baseIncomeDates.Count == 0
                    ? null
                    : baseIncomeDates.Max();

                return Results.Ok(new
                {
                    playerId,
                    cash = balance.Cash,
                    pool = pool == null
                        ? null
                        : new { pool.MaxVolume, pool.UsedVolume },
                    baseIncomeAmount = economy.BaseIncomeAmount,
                    baseIncomeIntervalMinutes = economy.BaseIncomeIntervalMinutes,
                    lastBaseIncomeAt = lastBaseIncome
                });
            })
            .WithName("GetMyWallet")
            .WithOpenApi();

        group.MapGet("/me/pool", async Task<IResult> (
                HttpContext http,
                AppDbContext db,
                PlayerPoolBootstrapService poolBootstrap,
                CancellationToken ct) =>
            {
                if (http.Items["PlayerId"] is not Guid playerId)
                    return Results.Unauthorized();

                await poolBootstrap.EnsureStarterPoolAsync(playerId, ct);

                var pool = await db.InventoryPools.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.PlayerId == playerId, ct);
                if (pool == null)
                    return Results.NotFound();

                var stacks = await db.PoolStacks.AsNoTracking()
                    .Where(s => s.PlayerId == playerId && s.Quantity > 0)
                    .OrderBy(s => s.ElementId)
                    .Select(s => new { s.ElementId, s.Quantity, s.VolumePerUnit })
                    .ToListAsync(ct);

                return Results.Ok(new
                {
                    pool.MaxVolume,
                    pool.UsedVolume,
                    stacks
                });
            })
            .WithName("GetMyPool")
            .WithOpenApi();

        group.MapGet("/me/pool/view", async Task<IResult> (
                HttpContext http,
                PlayerPoolBootstrapService poolBootstrap,
                MarketQueryService query,
                CancellationToken ct) =>
            {
                if (http.Items["PlayerId"] is not Guid playerId)
                    return Results.Unauthorized();

                await poolBootstrap.EnsureStarterPoolAsync(playerId, ct);

                var locale = http.Request.Headers.AcceptLanguage.ToString().Split(',')[0].Trim();
                if (string.IsNullOrEmpty(locale))
                    locale = "en";

                var overview = await query.GetPoolOverviewAsync(playerId, locale, ct);
                if (overview == null)
                    return Results.NotFound();

                return Results.Ok(overview);
            })
            .WithName("GetMyPoolView")
            .WithOpenApi();

        group.MapGet("/me/transactions", async Task<IResult> (
                HttpContext http,
                AppDbContext db,
                int? page,
                int? pageSize,
                DateTimeOffset? from,
                DateTimeOffset? to,
                CancellationToken ct) =>
            {
                if (http.Items["PlayerId"] is not Guid playerId)
                    return Results.Unauthorized();

                var p = Math.Max(1, page ?? 1);
                var size = Math.Clamp(pageSize ?? 25, 1, 100);

                var query = db.EconomyTransactions.AsNoTracking()
                    .Where(t => t.PlayerId == playerId);

                if (from.HasValue)
                    query = query.Where(t => t.CreatedAt >= from.Value);
                if (to.HasValue)
                    query = query.Where(t => t.CreatedAt <= to.Value);

                var total = await query.CountAsync(ct);
                var rows = await query.ToListAsync(ct);
                var items = rows
                    .OrderByDescending(t => t.CreatedAt)
                    .Skip((p - 1) * size)
                    .Take(size)
                    .Select(t => new PlayerTransactionDto(
                        t.Id,
                        t.Type,
                        t.CashDelta,
                        t.CreatedAt,
                        t.Metadata))
                    .ToList();

                return Results.Ok(new PlayerTransactionsPageDto(items, total, p, size));
            })
            .WithName("GetMyTransactions")
            .WithOpenApi();
    }
}
