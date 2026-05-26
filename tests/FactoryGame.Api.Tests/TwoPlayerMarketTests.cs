using System.Net.Http.Json;
using FactoryGame.Contracts.Auth;
using FactoryGame.Contracts.Market;
using FactoryGame.Domain.Content;

namespace FactoryGame.Api.Tests;

public sealed class TwoPlayerMarketTests : IClassFixture<ApiWebApplicationFixture>
{
    private const int ElementId = 1;
    private static readonly long ElementDna = ElementCatalogLookup.CatalogDnaFor(ElementId);

    private readonly ApiWebApplicationFixture _fixture;

    public TwoPlayerMarketTests(ApiWebApplicationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Seller_and_buyer_can_match_on_open_order()
    {
        var factory = _fixture.Factory;
        var seller = factory.CreateClient();
        var buyer = factory.CreateClient();

        var sellerAuth = await seller.PostAsJsonAsync("/v1/auth/guest",
            new GuestAuthRequest("two-player-sell-" + Guid.NewGuid().ToString("N")));
        sellerAuth.EnsureSuccessStatusCode();
        var sellerBody = await sellerAuth.Content.ReadFromJsonAsync<GuestAuthResponse>();
        Assert.NotNull(sellerBody);
        seller.DefaultRequestHeaders.Add("Authorization", $"Bearer {sellerBody.SessionToken}");

        var buyerAuth = await buyer.PostAsJsonAsync("/v1/auth/guest",
            new GuestAuthRequest("two-player-buy-" + Guid.NewGuid().ToString("N")));
        buyerAuth.EnsureSuccessStatusCode();
        var buyerBody = await buyerAuth.Content.ReadFromJsonAsync<GuestAuthResponse>();
        Assert.NotNull(buyerBody);
        buyer.DefaultRequestHeaders.Add("Authorization", $"Bearer {buyerBody.SessionToken}");

        await seller.GetAsync("/v1/market/summary");
        var depth = await seller.GetFromJsonAsync<MarketDepthDto>($"/v1/market/elements/{ElementId}/depth");
        Assert.NotNull(depth);
        Assert.NotNull(depth.BestAsk);

        // Undercut synthetic asks so this player sell is the best ask.
        var sellPrice = Math.Max(0.01m, depth.BestAsk!.Value - 0.01m);
        var sell = await seller.PostAsJsonAsync("/v1/market/orders",
            new PlaceOrderRequest(ElementId, ElementDna, "sell", sellPrice, 3, "two-player-sell"));
        sell.EnsureSuccessStatusCode();
        var sellResult = await sell.Content.ReadFromJsonAsync<PlaceOrderResponse>();
        Assert.NotNull(sellResult);
        Assert.Equal("Open", sellResult.Status, ignoreCase: true);

        var buy = await buyer.PostAsJsonAsync("/v1/market/orders",
            new PlaceOrderRequest(ElementId, ElementDna, "buy", sellPrice, 2, "two-player-buy"));
        buy.EnsureSuccessStatusCode();
        var buyResult = await buy.Content.ReadFromJsonAsync<PlaceOrderResponse>();
        Assert.NotNull(buyResult);
        Assert.Equal("Filled", buyResult.Status, ignoreCase: true);
        Assert.Equal(2, buyResult.QuantityFilled);

        var sellerOrders = await seller.GetFromJsonAsync<List<MyOpenOrderDto>>("/v1/market/orders/mine");
        Assert.NotNull(sellerOrders);
        var remaining = sellerOrders.FirstOrDefault(o => o.ElementId == ElementId && o.Dna == ElementDna);
        Assert.NotNull(remaining);
        Assert.Equal(1, remaining.QuantityRemaining);
    }
}
