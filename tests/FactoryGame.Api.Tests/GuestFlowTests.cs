using System.Net;
using System.Net.Http.Json;
using FactoryGame.Contracts.Auth;
using FactoryGame.Contracts.Market;
using FactoryGame.Domain.Content;

namespace FactoryGame.Api.Tests;

public sealed class GuestFlowTests : IClassFixture<ApiWebApplicationFixture>
{
    private readonly ApiWebApplicationFixture _fixture;

    public GuestFlowTests(ApiWebApplicationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Two_guests_can_match_on_order_book()
    {
        var seller = _fixture.Factory.CreateClient();
        var buyer = _fixture.Factory.CreateClient();

        var sellerAuth = await seller.PostAsJsonAsync("/v1/auth/guest", new GuestAuthRequest("integration-device-seller"));
        sellerAuth.EnsureSuccessStatusCode();
        var sellerBody = await sellerAuth.Content.ReadFromJsonAsync<GuestAuthResponse>();
        Assert.NotNull(sellerBody);
        seller.DefaultRequestHeaders.Add("Authorization", $"Bearer {sellerBody.SessionToken}");

        var buyerAuth = await buyer.PostAsJsonAsync("/v1/auth/guest", new GuestAuthRequest("integration-device-buyer"));
        buyerAuth.EnsureSuccessStatusCode();
        var buyerBody = await buyerAuth.Content.ReadFromJsonAsync<GuestAuthResponse>();
        Assert.NotNull(buyerBody);
        buyer.DefaultRequestHeaders.Add("Authorization", $"Bearer {buyerBody.SessionToken}");

        (await seller.GetAsync("/v1/me/wallet")).EnsureSuccessStatusCode();
        (await buyer.GetAsync("/v1/me/wallet")).EnsureSuccessStatusCode();

        var sell = await seller.PostAsJsonAsync("/v1/market/orders",
            new PlaceOrderRequest(1, ElementCatalogLookup.CatalogDnaFor(1), "sell", 10m, 5, "int-sell-1"));
        sell.EnsureSuccessStatusCode();

        var buy = await buyer.PostAsJsonAsync("/v1/market/orders",
            new PlaceOrderRequest(1, ElementCatalogLookup.CatalogDnaFor(1), "buy", 10m, 5, "int-buy-1"));
        buy.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Guest_session_can_load_market_insights()
    {
        var client = _fixture.Factory.CreateClient();
        var auth = await client.PostAsJsonAsync("/v1/auth/guest",
            new GuestAuthRequest("insights-" + Guid.NewGuid().ToString("N")));
        auth.EnsureSuccessStatusCode();
        var body = await auth.Content.ReadFromJsonAsync<GuestAuthResponse>();
        Assert.NotNull(body);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {body.SessionToken}");

        var insights = await client.GetAsync("/v1/market/insights");
        insights.EnsureSuccessStatusCode();
    }
}
