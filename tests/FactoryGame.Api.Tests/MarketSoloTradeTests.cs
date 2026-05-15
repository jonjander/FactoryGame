using System.Net;
using System.Net.Http.Json;
using FactoryGame.Contracts.Auth;
using FactoryGame.Contracts.Market;

namespace FactoryGame.Api.Tests;

public sealed class MarketSoloTradeTests : IClassFixture<ApiWebApplicationFixture>
{
    private readonly ApiWebApplicationFixture _fixture;

    public MarketSoloTradeTests(ApiWebApplicationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Solo_player_can_buy_against_synthetic_liquidity()
    {
        var client = _fixture.Factory.CreateClient();
        var auth = await client.PostAsJsonAsync("/v1/auth/guest", new GuestAuthRequest("solo-buyer-" + Guid.NewGuid().ToString("N")));
        auth.EnsureSuccessStatusCode();
        var body = await auth.Content.ReadFromJsonAsync<GuestAuthResponse>();
        Assert.NotNull(body);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {body.SessionToken}");

        var summary = await client.GetAsync("/v1/market/summary");
        summary.EnsureSuccessStatusCode();
        var items = await summary.Content.ReadFromJsonAsync<List<MarketElementSummaryDto>>();
        Assert.NotNull(items);
        Assert.NotEmpty(items);

        var elementId = items[0].ElementId;
        var depth = await client.GetFromJsonAsync<MarketDepthDto>($"/v1/market/elements/{elementId}/depth");
        Assert.NotNull(depth);
        Assert.NotNull(depth.BestAsk);

        var buy = await client.PostAsJsonAsync("/v1/market/orders",
            new PlaceOrderRequest(elementId, "buy", depth.BestAsk!.Value, 1, "solo-buy-1"));
        Assert.Equal(HttpStatusCode.OK, buy.StatusCode);
        var orderBody = await buy.Content.ReadFromJsonAsync<PlaceOrderResponse>();
        Assert.NotNull(orderBody);
        Assert.Equal("Filled", orderBody.Status, ignoreCase: true);
    }
}
