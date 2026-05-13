using FactoryGame.Contracts.Auth;
using FactoryGame.Infrastructure.Services;

namespace FactoryGame.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/v1").WithTags("Auth");

        group.MapPost("/auth/guest", async Task<IResult> (
                GuestAuthRequest request,
                GuestAuthService auth,
                CancellationToken ct) =>
            {
                try
                {
                    var result = await auth.SignInGuestAsync(request.DeviceKey, ct);
                    return Results.Ok(new GuestAuthResponse(result.PlayerId, result.SessionToken));
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            })
            .WithName("GuestAuth")
            .WithOpenApi();
    }
}
