using System.Net.Http.Json;
using FactoryGame.Contracts.Auth;
using FactoryGame.Contracts.Market;
using FactoryGame.Domain.Content;

namespace FactoryGame.Api.Tests;

public sealed class MarketOrderActionsTests : IClassFixture<ApiWebApplicationFixture>
{
    private readonly ApiWebApplicationFixture _fixture;

    public MarketOrderActionsTests(ApiWebApplicationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Cancel_open_sell_order_returns_quantity_to_pool()
    {
        var client = await CreateAuthedClientAsync();
        await client.GetAsync("/v1/market/summary");

        var depth = await client.GetFromJsonAsync<MarketDepthDto>("/v1/market/elements/1/depth");
        Assert.NotNull(depth);
        Assert.NotNull(depth.BestAsk);

        var sellPrice = depth.BestAsk!.Value + 10m;
        var poolBefore = await GetPoolQuantityAsync(client, 1);

        var place = await client.PostAsJsonAsync("/v1/market/orders",
            new PlaceOrderRequest(1, ElementCatalogLookup.CatalogDnaFor(1), "sell", sellPrice, 2, "cancel-sell-1"));
        place.EnsureSuccessStatusCode();
        var placed = await place.Content.ReadFromJsonAsync<PlaceOrderResponse>();
        Assert.NotNull(placed);

        var poolAfterSell = await GetPoolQuantityAsync(client, 1);
        Assert.Equal(poolBefore - 2, poolAfterSell);

        var cancel = await client.PostAsync($"/v1/market/orders/{placed.OrderId}/cancel", null);
        cancel.EnsureSuccessStatusCode();
        var cancelled = await cancel.Content.ReadFromJsonAsync<OrderActionResponse>();
        Assert.NotNull(cancelled);
        Assert.Equal("Cancelled", cancelled.Status);

        var mine = await client.GetFromJsonAsync<List<MyOpenOrderDto>>("/v1/market/orders/mine?elementId=1");
        Assert.NotNull(mine);
        Assert.DoesNotContain(mine, o => o.Id == placed.OrderId);

        var poolAfterCancel = await GetPoolQuantityAsync(client, 1);
        Assert.Equal(poolBefore, poolAfterCancel);
    }

    [Fact]
    public async Task Amend_buy_order_can_raise_limit_and_fill()
    {
        var client = await CreateAuthedClientAsync();
        await client.GetAsync("/v1/market/summary");

        var depth = await client.GetFromJsonAsync<MarketDepthDto>("/v1/market/elements/1/depth");
        Assert.NotNull(depth);
        Assert.NotNull(depth.BestAsk);

        var ask = depth.BestAsk!.Value;
        var lowBuy = ask - 5m;
        if (lowBuy < 0.01m) lowBuy = 0.01m;

        var place = await client.PostAsJsonAsync("/v1/market/orders",
            new PlaceOrderRequest(1, ElementCatalogLookup.CatalogDnaFor(1), "buy", lowBuy, 1, "amend-buy-1"));
        place.EnsureSuccessStatusCode();
        var placed = await place.Content.ReadFromJsonAsync<PlaceOrderResponse>();
        Assert.NotNull(placed);
        Assert.Equal("Open", placed.Status);

        var amend = await client.PostAsJsonAsync(
            $"/v1/market/orders/{placed.OrderId}/amend",
            new AmendOrderRequest(ask));
        amend.EnsureSuccessStatusCode();
        var amended = await amend.Content.ReadFromJsonAsync<OrderActionResponse>();
        Assert.NotNull(amended);
        Assert.True(amended.QuantityFilled > 0 || amended.QuantityRemaining == 0);
    }

    private async Task<HttpClient> CreateAuthedClientAsync()
    {
        var client = _fixture.Factory.CreateClient();
        var auth = await client.PostAsJsonAsync("/v1/auth/guest",
            new GuestAuthRequest("order-actions-" + Guid.NewGuid().ToString("N")));
        auth.EnsureSuccessStatusCode();
        var body = await auth.Content.ReadFromJsonAsync<GuestAuthResponse>();
        Assert.NotNull(body);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {body.SessionToken}");
        return client;
    }

    private static async Task<long> GetPoolQuantityAsync(HttpClient client, int elementId)
    {
        var pool = await client.GetFromJsonAsync<SimplePoolDto>("/v1/me/pool");
        Assert.NotNull(pool);
        return pool.Stacks.Where(s => s.ElementId == elementId).Sum(s => s.Quantity);
    }

    private sealed record SimplePoolDto(long MaxVolume, long UsedVolume, List<SimplePoolStackDto> Stacks);

    private sealed record SimplePoolStackDto(int ElementId, long Quantity, long VolumePerUnit);
}
