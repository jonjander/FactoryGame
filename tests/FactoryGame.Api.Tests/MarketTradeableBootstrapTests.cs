using System.Net.Http.Json;
using FactoryGame.Contracts.Auth;
using FactoryGame.Contracts.Market;

namespace FactoryGame.Api.Tests;

/// <summary>
/// Catalog elements must be tradeable before any player holds them (synthetic liquidity bootstrap).
/// </summary>
public sealed class MarketTradeableBootstrapTests : IClassFixture<ApiWebApplicationFixture>
{
    private const int NonStarterLiquidElementId = 7;

    private readonly ApiWebApplicationFixture _fixture;

    public MarketTradeableBootstrapTests(ApiWebApplicationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Depth_for_catalog_element_without_pool_still_returns_ask()
    {
        var client = _fixture.Factory.CreateClient();
        var auth = await client.PostAsJsonAsync("/v1/auth/guest",
            new GuestAuthRequest("tradeable-bootstrap-" + Guid.NewGuid().ToString("N")));
        auth.EnsureSuccessStatusCode();
        var body = await auth.Content.ReadFromJsonAsync<GuestAuthResponse>();
        Assert.NotNull(body);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {body.SessionToken}");

        var depth = await client.GetFromJsonAsync<MarketDepthDto>(
            $"/v1/market/elements/{NonStarterLiquidElementId}/depth");
        Assert.NotNull(depth);
        Assert.NotNull(depth.BestAsk);
        Assert.True(depth.BestAsk > 0);
    }
}
