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
                    if (string.IsNullOrWhiteSpace(request.DeviceKey))
                        return Results.BadRequest(new { error = "DeviceKey is required." });

                    var result = await auth.SignInGuestAsync(request.DeviceKey, ct);
                    return Results.Ok(new GuestAuthResponse(result.PlayerId, result.SessionToken));
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
                catch (Exception ex)
                {
                    return Results.Problem(
                        detail: ex.Message,
                        statusCode: StatusCodes.Status500InternalServerError,
                        title: "Guest sign-in failed");
                }
            })
            .WithName("GuestAuth")
            .WithOpenApi();
    }
}
