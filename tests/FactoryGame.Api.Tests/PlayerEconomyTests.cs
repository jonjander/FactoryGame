using System.Net.Http.Json;
using FactoryGame.Contracts.Auth;
using FactoryGame.Contracts.Player;

namespace FactoryGame.Api.Tests;

public sealed class PlayerEconomyTests : IClassFixture<ApiWebApplicationFixture>
{
    private readonly ApiWebApplicationFixture _fixture;

    public PlayerEconomyTests(ApiWebApplicationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Economy_overview_returns_history_and_period_changes()
    {
        var client = _fixture.Factory.CreateClient();
        var auth = await client.PostAsJsonAsync("/v1/auth/guest",
            new GuestAuthRequest("econ-overview-" + Guid.NewGuid().ToString("N")));
        auth.EnsureSuccessStatusCode();
        var body = await auth.Content.ReadFromJsonAsync<GuestAuthResponse>();
        Assert.NotNull(body);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {body.SessionToken}");

        await client.GetAsync("/v1/me/wallet");

        var overview = await client.GetFromJsonAsync<PlayerEconomyOverviewDto>("/v1/me/economy/overview");
        Assert.NotNull(overview);
        Assert.True(overview.TotalValue > 0);
        Assert.True(overview.Cash > 0);
        Assert.NotEmpty(overview.History);
        Assert.True(overview.PeriodChanges.MaxPercent is null or decimal);
    }
}
