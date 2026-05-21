using System.Net.Http.Json;
using FactoryGame.Contracts.Auth;
using FactoryGame.Contracts.Market;

namespace FactoryGame.Api.Tests;

public sealed class MarketMyOrdersTests : IClassFixture<ApiWebApplicationFixture>
{
    private readonly ApiWebApplicationFixture _fixture;

    public MarketMyOrdersTests(ApiWebApplicationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task My_open_orders_returns_ok_after_place_order()
    {
        var client = _fixture.Factory.CreateClient();
        var auth = await client.PostAsJsonAsync("/v1/auth/guest",
            new GuestAuthRequest("my-orders-" + Guid.NewGuid().ToString("N")));
        auth.EnsureSuccessStatusCode();
        var body = await auth.Content.ReadFromJsonAsync<GuestAuthResponse>();
        Assert.NotNull(body);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {body.SessionToken}");

        await client.GetAsync("/v1/market/summary");

        var depth = await client.GetFromJsonAsync<MarketDepthDto>("/v1/market/elements/1/depth");
        Assert.NotNull(depth);
        Assert.NotNull(depth.BestAsk);

        var sellPrice = depth.BestAsk!.Value + 5m;
        var place = await client.PostAsJsonAsync("/v1/market/orders",
            new PlaceOrderRequest(1, "sell", sellPrice, 1, "my-orders-sell-1"));
        place.EnsureSuccessStatusCode();

        var mine = await client.GetAsync("/v1/market/orders/mine");
        mine.EnsureSuccessStatusCode();
        var orders = await mine.Content.ReadFromJsonAsync<List<MyOpenOrderDto>>();
        Assert.NotNull(orders);
        Assert.Contains(orders, o => o.ElementId == 1 && o.Side == "Sell" && o.QuantityRemaining > 0);
    }
}
