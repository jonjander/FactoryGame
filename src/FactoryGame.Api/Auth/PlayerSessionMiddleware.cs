using System.Net;
using FactoryGame.Infrastructure.Data;
using FactoryGame.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace FactoryGame.Api.Auth;

public sealed class PlayerSessionMiddleware(RequestDelegate next)
{
    private static readonly PathString GuestAuthPath = "/v1/auth/guest";

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        // API lives under /v1 only; static UI and infrastructure routes skip DB session.
        if (!context.Request.Path.StartsWithSegments("/v1"))
        {
            await next(context);
            return;
        }

        if (IsPublic(context))
        {
            await next(context);
            return;
        }

        if (TryGetApiKey(context, out var apiKeyHeader))
        {
            var hash = ApiKeyHash.Sha256Hex(apiKeyHeader);
            var apiKey = await db.ApiKeys.AsNoTracking()
                .FirstOrDefaultAsync(k => k.KeyHash == hash, context.RequestAborted);
            if (apiKey == null)
            {
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                return;
            }

            context.Items["PlayerId"] = apiKey.PlayerId;
            context.Items["AuthScopes"] = apiKey.Scopes;
            await next(context);
            return;
        }

        if (!context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            return;
        }

        var token = context.Request.Headers.Authorization.ToString()["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(token))
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            return;
        }

        var session = await db.PlayerSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Token == token, context.RequestAborted);
        if (session == null)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            return;
        }

        if (session.ExpiresAt is { } exp && exp < DateTimeOffset.UtcNow)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            return;
        }

        context.Items["PlayerId"] = session.PlayerId;
        context.Items["AuthScopes"] = "market,boards,content,player";
        await next(context);
    }

    private static bool TryGetApiKey(HttpContext context, out string key)
    {
        key = "";
        if (context.Request.Headers.TryGetValue("X-Api-Key", out var values))
        {
            var v = values.ToString().Trim();
            if (!string.IsNullOrEmpty(v))
            {
                key = v;
                return true;
            }
        }

        return false;
    }

    private static bool IsPublic(HttpContext context)
    {
        var path = context.Request.Path;
        if (context.Request.Method == HttpMethods.Get && path.StartsWithSegments("/v1/content"))
            return true;
        if (context.Request.Method == HttpMethods.Get && path.StartsWithSegments("/v1/market"))
        {
            if (path.StartsWithSegments("/v1/market/summary"))
                return false;
            return true;
        }
        if (path == GuestAuthPath && context.Request.Method == HttpMethods.Post)
            return true;
        return false;
    }
}
