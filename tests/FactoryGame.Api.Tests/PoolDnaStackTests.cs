using System.Net.Http.Json;
using FactoryGame.Contracts.Auth;
using FactoryGame.Contracts.Market;
using FactoryGame.Contracts.Pool;
using FactoryGame.Domain.Content;
using FactoryGame.Domain.Dna;
using FactoryGame.Infrastructure.Data;
using FactoryGame.Infrastructure.Simulation;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryGame.Api.Tests;

public sealed class PoolDnaStackTests : IClassFixture<ApiWebApplicationFixture>
{
    private const int ElementId = 7;

    private readonly ApiWebApplicationFixture _fixture;

    public PoolDnaStackTests(ApiWebApplicationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Pool_keeps_separate_stacks_per_dna_variant()
    {
        var client = _fixture.Factory.CreateClient();
        var authBody = await AuthAsync(client, "pool-dna-" + Guid.NewGuid().ToString("N"));

        var catalogDna = ElementCatalogLookup.CatalogDnaFor(ElementId);
        var gasDna = BuildGasDna(boilingPoint: 2500);

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var gateway = new SeaportPoolGateway(db, authBody.PlayerId);
            Assert.True(gateway.TryDeposit(ElementId, gasDna, 23));
            Assert.True(gateway.TryDeposit(ElementId, catalogDna, 3));
            await db.SaveChangesAsync();
        }

        var overview = await client.GetFromJsonAsync<PoolOverviewDto>("/v1/me/pool/view");
        Assert.NotNull(overview);
        var group = Assert.Single(overview.Groups.Where(g => g.ElementId == ElementId));
        Assert.Equal(2, group.Variants.Count);
        Assert.Equal(26, group.TotalQuantity);

        var gasVariant = Assert.Single(group.Variants, v => v.Dna == gasDna);
        var liquidVariant = Assert.Single(group.Variants, v => v.Dna == catalogDna);
        Assert.Equal(23, gasVariant.Quantity);
        Assert.Equal(3, liquidVariant.Quantity);
        Assert.Equal("Gas", gasVariant.PhaseLabel);
        Assert.Equal("Liquid", liquidVariant.PhaseLabel);
    }

    [Fact]
    public async Task Market_orders_only_match_same_dna_variant()
    {
        var seller = _fixture.Factory.CreateClient();
        var buyer = _fixture.Factory.CreateClient();
        var sellerBody = await AuthAsync(seller, "dna-sell-" + Guid.NewGuid().ToString("N"));
        var buyerBody = await AuthAsync(buyer, "dna-buy-" + Guid.NewGuid().ToString("N"));
        _ = buyerBody;

        var catalogDna = ElementCatalogLookup.CatalogDnaFor(ElementId);
        var gasDna = BuildGasDna(boilingPoint: 2500);

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var sellerGateway = new SeaportPoolGateway(db, sellerBody.PlayerId);
            Assert.True(sellerGateway.TryDeposit(ElementId, gasDna, 5));
            await db.SaveChangesAsync();
        }

        await seller.GetAsync("/v1/market/summary");
        await buyer.GetAsync("/v1/market/summary");

        var sell = await seller.PostAsJsonAsync("/v1/market/orders",
            new PlaceOrderRequest(ElementId, gasDna, "sell", 12m, 2, "dna-variant-sell"));
        sell.EnsureSuccessStatusCode();

        var buyWrong = await buyer.PostAsJsonAsync("/v1/market/orders",
            new PlaceOrderRequest(ElementId, catalogDna, "buy", 12m, 2, "dna-variant-buy-wrong"));
        buyWrong.EnsureSuccessStatusCode();
        var wrongResult = await buyWrong.Content.ReadFromJsonAsync<PlaceOrderResponse>();
        Assert.NotNull(wrongResult);
        Assert.Equal(0, wrongResult.QuantityFilled);

        var buyRight = await buyer.PostAsJsonAsync("/v1/market/orders",
            new PlaceOrderRequest(ElementId, gasDna, "buy", 12m, 2, "dna-variant-buy-right"));
        buyRight.EnsureSuccessStatusCode();
        var rightResult = await buyRight.Content.ReadFromJsonAsync<PlaceOrderResponse>();
        Assert.NotNull(rightResult);
        Assert.Equal(2, rightResult.QuantityFilled);
    }

    private static async Task<GuestAuthResponse> AuthAsync(HttpClient client, string deviceKey)
    {
        var auth = await client.PostAsJsonAsync("/v1/auth/guest", new GuestAuthRequest(deviceKey));
        auth.EnsureSuccessStatusCode();
        var body = await auth.Content.ReadFromJsonAsync<GuestAuthResponse>();
        Assert.NotNull(body);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {body.SessionToken}");
        return body;
    }

    private static long BuildGasDna(int boilingPoint)
    {
        const long phaseGas = 2;
        var dna = phaseGas << DnaLayout.PhaseShift;
        dna |= (long)boilingPoint << DnaLayout.BoilingShift;
        return dna;
    }
}
