using FactoryGame.Contracts.Machines;
using FactoryGame.Infrastructure.Data;
using FactoryGame.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        group.MapGet("/me/wallet", async Task<IResult> (HttpContext http, AppDbContext db, CancellationToken ct) =>
            {
                if (http.Items["PlayerId"] is not Guid playerId)
                    return Results.Unauthorized();

                var balance = await db.PlayerBalances.AsNoTracking()
                    .FirstOrDefaultAsync(b => b.PlayerId == playerId, ct);
                if (balance == null)
                    return Results.NotFound();

                var pool = await db.InventoryPools.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.PlayerId == playerId, ct);

                return Results.Ok(new
                {
                    playerId,
                    cash = balance.Cash,
                    pool = pool == null
                        ? null
                        : new { pool.MaxVolume, pool.UsedVolume }
                });
            })
            .WithName("GetMyWallet")
            .WithOpenApi();

        group.MapGet("/me/pool", async Task<IResult> (HttpContext http, AppDbContext db, CancellationToken ct) =>
            {
                if (http.Items["PlayerId"] is not Guid playerId)
                    return Results.Unauthorized();

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

        group.MapGet("/me/transactions", async Task<IResult> (HttpContext http, AppDbContext db, CancellationToken ct) =>
            {
                if (http.Items["PlayerId"] is not Guid playerId)
                    return Results.Unauthorized();

                var rows = await db.EconomyTransactions.AsNoTracking()
                    .Where(t => t.PlayerId == playerId)
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(100)
                    .Select(t => new
                    {
                        t.Id,
                        t.Type,
                        t.CashDelta,
                        t.CreatedAt,
                        t.Metadata
                    })
                    .ToListAsync(ct);

                return Results.Ok(rows);
            })
            .WithName("GetMyTransactions")
            .WithOpenApi();
    }
}
