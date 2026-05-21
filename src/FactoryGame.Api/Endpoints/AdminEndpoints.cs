using FactoryGame.Infrastructure.Data;
using FactoryGame.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FactoryGame.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/v1/admin").WithTags("Admin");

        group.MapGet("/players", async Task<IResult> (HttpContext http, [FromHeader(Name = "X-Admin-Token")] string? token, AdminService admin, AppDbContext db, CancellationToken ct) =>
            {
                try
                {
                    admin.ValidateBootstrapToken(token);
                }
                catch (UnauthorizedAccessException)
                {
                    return Results.Unauthorized();
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }

                var rows = await db.Players.AsNoTracking().ToListAsync(ct);
                var players = rows
                    .OrderByDescending(p => p.CreatedAt.UtcDateTime.Ticks)
                    .Take(200)
                    .Select(p => new { p.Id, p.CreatedAt })
                    .ToList();
                return Results.Ok(players);
            })
            .WithName("AdminListPlayers")
            .WithOpenApi();

        group.MapPost("/api-keys", async Task<IResult> (
                HttpContext http,
                [FromHeader(Name = "X-Admin-Token")] string? token,
                [FromBody] CreateApiKeyBody body,
                AdminService admin,
                CancellationToken ct) =>
            {
                try
                {
                    admin.ValidateBootstrapToken(token);
                }
                catch (UnauthorizedAccessException)
                {
                    return Results.Unauthorized();
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }

                try
                {
                    var (id, plain) = await admin.CreateApiKeyAsync(body.PlayerId, body.Name, body.Scopes, ct);
                    return Results.Ok(new { id, key = plain, body.PlayerId, body.Name, body.Scopes });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            })
            .WithName("AdminCreateApiKey")
            .WithOpenApi();
    }

    public sealed record CreateApiKeyBody(Guid PlayerId, string Name, string Scopes);
}
