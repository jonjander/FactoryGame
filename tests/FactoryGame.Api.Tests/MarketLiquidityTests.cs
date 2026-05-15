using System.Net;
using System.Net.Http.Json;
using FactoryGame.Contracts.Auth;
using FactoryGame.Contracts.Market;
using FactoryGame.Domain.Content;
using FactoryGame.Domain.Market;
using FactoryGame.Infrastructure.Data;
using FactoryGame.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryGame.Api.Tests;

public sealed class MarketLiquidityTests : IClassFixture<ApiWebApplicationFixture>
{
    private readonly ApiWebApplicationFixture _fixture;

    public MarketLiquidityTests(ApiWebApplicationFixture fixture) => _fixture = fixture;

    [Fact]
    public void ElementReferencePrice_is_stable_for_same_dna()
    {
        var dna = ElementCatalog.All[0].Dna;
        var a = ElementReferencePrice.Compute(dna);
        var b = ElementReferencePrice.Compute(dna);
        Assert.Equal(a, b);
    }

    [Fact]
    public void ElementReferencePrice_differs_for_different_dna()
    {
        var a = ElementReferencePrice.Compute(ElementCatalog.All[0].Dna);
        var b = ElementReferencePrice.Compute(ElementCatalog.All[1].Dna);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public async Task Synthetic_sells_are_worse_than_player_ask_when_both_exist()
    {
        var seller = await CreateAuthenticatedClientAsync("synth-seller-" + Guid.NewGuid().ToString("N"));
        var other = await CreateAuthenticatedClientAsync("synth-other-" + Guid.NewGuid().ToString("N"));

        const int elementId = 1;
        var playerAsk = 12.50m;
        var sell = await seller.PostAsJsonAsync("/v1/market/orders",
            new PlaceOrderRequest(elementId, "sell", playerAsk, 40, "player-sell-cap"));
        sell.EnsureSuccessStatusCode();

        await other.GetAsync("/v1/market/summary");

        var depth = await other.GetFromJsonAsync<MarketDepthDto>($"/v1/market/elements/{elementId}/depth");
        Assert.NotNull(depth);

        var bestAsk = depth.BestAsk;
        Assert.NotNull(bestAsk);
        Assert.Equal(playerAsk, bestAsk.Value);

        var syntheticAsks = depth.Levels
            .Where(l => l.AskQuantity > 0 && l.Price > playerAsk)
            .ToList();
        Assert.NotEmpty(syntheticAsks);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string deviceKey)
    {
        var client = _fixture.Factory.CreateClient();
        var auth = await client.PostAsJsonAsync("/v1/auth/guest", new GuestAuthRequest(deviceKey));
        auth.EnsureSuccessStatusCode();
        var body = await auth.Content.ReadFromJsonAsync<GuestAuthResponse>();
        Assert.NotNull(body);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {body.SessionToken}");
        return client;
    }
}
