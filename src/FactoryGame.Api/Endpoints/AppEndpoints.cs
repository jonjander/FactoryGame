using System.Reflection;
using FactoryGame.Contracts.App;

namespace FactoryGame.Api.Endpoints;

public static class AppEndpoints
{
    public static void MapAppEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/v1/app").WithTags("App");

        group.MapGet("/version", (HttpContext ctx) =>
            {
                ctx.Response.Headers.CacheControl = "no-store";
                return Results.Ok(new AppVersionDto(ReleaseVersion.Current));
            })
            .WithName("GetAppVersion")
            .WithOpenApi();
    }

    private static class ReleaseVersion
    {
        public static string Current =>
            typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(Program).Assembly.GetName().Version?.ToString()
            ?? "0.0.0";
    }
}
