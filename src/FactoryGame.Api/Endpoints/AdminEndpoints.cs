using FactoryGame.Contracts.Admin;
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

        group.MapGet("/players", async Task<IResult> (
                [FromHeader(Name = "X-Admin-Token")] string? token,
                AdminService admin,
                AppDbContext db,
                CancellationToken ct) =>
            {
                var auth = ValidateAdmin(token, admin);
                if (auth != null)
                    return auth;

                var rows = await db.Players.AsNoTracking().ToListAsync(ct);
                var players = rows
                    .OrderByDescending(p => p.CreatedAt.UtcDateTime.Ticks)
                    .Take(200)
                    .Select(p => new { p.Id, p.CreatedAt, p.IsSponsorAccount })
                    .ToList();
                return Results.Ok(players);
            })
            .WithName("AdminListPlayers")
            .WithOpenApi();

        group.MapPost("/api-keys", async Task<IResult> (
                [FromHeader(Name = "X-Admin-Token")] string? token,
                [FromBody] CreateApiKeyBody body,
                AdminService admin,
                CancellationToken ct) =>
            {
                var auth = ValidateAdmin(token, admin);
                if (auth != null)
                    return auth;

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

        group.MapGet("/companies", async Task<IResult> (
                [FromHeader(Name = "X-Admin-Token")] string? token,
                AdminService admin,
                SponsorCompanyService sponsors,
                CancellationToken ct) =>
            {
                var auth = ValidateAdmin(token, admin);
                if (auth != null)
                    return auth;
                return Results.Ok(await sponsors.ListAsync(ct));
            })
            .WithName("AdminListCompanies")
            .WithOpenApi();

        group.MapGet("/companies/{id:guid}", async Task<IResult> (
                Guid id,
                [FromHeader(Name = "X-Admin-Token")] string? token,
                AdminService admin,
                SponsorCompanyService sponsors,
                CancellationToken ct) =>
            {
                var auth = ValidateAdmin(token, admin);
                if (auth != null)
                    return auth;
                var company = await sponsors.GetAsync(id, ct);
                return company == null ? Results.NotFound() : Results.Ok(company);
            })
            .WithName("AdminGetCompany")
            .WithOpenApi();

        group.MapPost("/companies", async Task<IResult> (
                [FromHeader(Name = "X-Admin-Token")] string? token,
                [FromBody] CreateSponsorCompanyRequest body,
                AdminService admin,
                SponsorCompanyService sponsors,
                CancellationToken ct) =>
            {
                var auth = ValidateAdmin(token, admin);
                if (auth != null)
                    return auth;
                try
                {
                    var created = await sponsors.CreateAsync(body, ct);
                    return Results.Ok(created);
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
            .WithName("AdminCreateCompany")
            .WithOpenApi();

        group.MapPatch("/companies/{id:guid}", async Task<IResult> (
                Guid id,
                [FromHeader(Name = "X-Admin-Token")] string? token,
                [FromBody] UpdateSponsorCompanyRequest body,
                AdminService admin,
                SponsorCompanyService sponsors,
                CancellationToken ct) =>
            {
                var auth = ValidateAdmin(token, admin);
                if (auth != null)
                    return auth;
                try
                {
                    var updated = await sponsors.UpdateAsync(id, body, ct);
                    return Results.Ok(updated);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.NotFound(new { error = ex.Message });
                }
            })
            .WithName("AdminUpdateCompany")
            .WithOpenApi();

        group.MapGet("/companies/{companyId:guid}/orders", async Task<IResult> (
                Guid companyId,
                [FromHeader(Name = "X-Admin-Token")] string? token,
                AdminService admin,
                SponsorCompanyService sponsors,
                CancellationToken ct) =>
            {
                var auth = ValidateAdmin(token, admin);
                if (auth != null)
                    return auth;
                try
                {
                    return Results.Ok(await sponsors.ListOrdersAsync(companyId, ct));
                }
                catch (InvalidOperationException ex)
                {
                    return Results.NotFound(new { error = ex.Message });
                }
            })
            .WithName("AdminListCompanyOrders")
            .WithOpenApi();

        group.MapPost("/companies/{companyId:guid}/orders", async Task<IResult> (
                Guid companyId,
                [FromHeader(Name = "X-Admin-Token")] string? token,
                [FromBody] CreateSponsorCompanyOrderRequest body,
                AdminService admin,
                SponsorCompanyService sponsors,
                CancellationToken ct) =>
            {
                var auth = ValidateAdmin(token, admin);
                if (auth != null)
                    return auth;
                try
                {
                    var created = await sponsors.CreateOrderAsync(companyId, body, ct);
                    return Results.Ok(created);
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
            .WithName("AdminCreateCompanyOrder")
            .WithOpenApi();

        group.MapPatch("/companies/{companyId:guid}/orders/{orderId:guid}", async Task<IResult> (
                Guid companyId,
                Guid orderId,
                [FromHeader(Name = "X-Admin-Token")] string? token,
                [FromBody] UpdateSponsorCompanyOrderRequest body,
                AdminService admin,
                SponsorCompanyService sponsors,
                CancellationToken ct) =>
            {
                var auth = ValidateAdmin(token, admin);
                if (auth != null)
                    return auth;
                try
                {
                    var updated = await sponsors.UpdateOrderAsync(companyId, orderId, body, ct);
                    return Results.Ok(updated);
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.NotFound(new { error = ex.Message });
                }
            })
            .WithName("AdminUpdateCompanyOrder")
            .WithOpenApi();

        group.MapDelete("/companies/{companyId:guid}/orders/{orderId:guid}", async Task<IResult> (
                Guid companyId,
                Guid orderId,
                [FromHeader(Name = "X-Admin-Token")] string? token,
                AdminService admin,
                SponsorCompanyService sponsors,
                CancellationToken ct) =>
            {
                var auth = ValidateAdmin(token, admin);
                if (auth != null)
                    return auth;
                try
                {
                    await sponsors.DeleteOrderAsync(companyId, orderId, ct);
                    return Results.NoContent();
                }
                catch (InvalidOperationException ex)
                {
                    return Results.NotFound(new { error = ex.Message });
                }
            })
            .WithName("AdminDeleteCompanyOrder")
            .WithOpenApi();
    }

    private static IResult? ValidateAdmin(string? token, AdminService admin)
    {
        try
        {
            admin.ValidateBootstrapToken(token);
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    public sealed record CreateApiKeyBody(Guid PlayerId, string Name, string Scopes);
}
