using System.Net.Http.Json;
using FactoryGame.Contracts.Auth;
using FactoryGame.Domain.Content;
using FactoryGame.Domain.Names;
using FactoryGame.Infrastructure.Data;
using FactoryGame.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryGame.Api.Tests;

public sealed class MarketVariantTests : IClassFixture<ApiWebApplicationFixture>
{
    private readonly ApiWebApplicationFixture _fixture;

    public MarketVariantTests(ApiWebApplicationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Summary_uses_variant_code_and_full_label()
    {
        var client = _fixture.Factory.CreateClient();
        var auth = await client.PostAsJsonAsync("/v1/auth/guest", new GuestAuthRequest("variant-label-" + Guid.NewGuid().ToString("N")));
        auth.EnsureSuccessStatusCode();
        var body = await auth.Content.ReadFromJsonAsync<GuestAuthResponse>();
        Assert.NotNull(body);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {body.SessionToken}");
        await client.GetAsync("/v1/me/wallet");

        var summaryRes = await client.GetAsync("/v1/market/summary");
        summaryRes.EnsureSuccessStatusCode();
        var items = await summaryRes.Content.ReadFromJsonAsync<List<FactoryGame.Contracts.Market.MarketElementSummaryDto>>();
        Assert.NotNull(items);
        Assert.NotEmpty(items);

        var row = items[0];
        Assert.Matches(@"^E\d{2}-\d{6}$", row.Symbol);
        Assert.Contains(row.Symbol, row.DisplayName);
    }

    [Fact]
    public async Task Per_dna_candles_and_prices_differ_for_same_element()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var liquidity = scope.ServiceProvider.GetRequiredService<MarketLiquidityService>();

        const int elementId = 3;
        var catalogDna = ElementCatalogLookup.CatalogDnaFor(elementId);
        var altDna = catalogDna + 42_424L;

        await liquidity.EnsureLiquidityForElementAsync(elementId, force: true, dna: catalogDna);
        await liquidity.EnsureLiquidityForElementAsync(elementId, force: true, dna: altDna);

        var catalogCandles = await db.MarketPriceCandles.CountAsync(c => c.ElementId == elementId && c.Dna == catalogDna);
        var altCandles = await db.MarketPriceCandles.CountAsync(c => c.ElementId == elementId && c.Dna == altDna);
        Assert.True(catalogCandles > 0);
        Assert.True(altCandles > 0);

        var query = scope.ServiceProvider.GetRequiredService<MarketQueryService>();
        var (catalogPrice, _) = await query.GetLastPriceAndChangeAsync(elementId, catalogDna);
        var (altPrice, _) = await query.GetLastPriceAndChangeAsync(elementId, altDna);

        Assert.NotEqual(catalogPrice, altPrice);
    }

    [Fact]
    public async Task History_endpoint_filters_by_dna()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var liquidity = scope.ServiceProvider.GetRequiredService<MarketLiquidityService>();

        const int elementId = 4;
        var catalogDna = ElementCatalogLookup.CatalogDnaFor(elementId);
        var altDna = catalogDna + 99L;
        await liquidity.EnsureLiquidityForElementAsync(elementId, force: true, dna: catalogDna);
        await liquidity.EnsureLiquidityForElementAsync(elementId, force: true, dna: altDna);

        var client = _fixture.Factory.CreateClient();
        var catalogHistory = await client.GetFromJsonAsync<List<FactoryGame.Contracts.Market.MarketCandleDto>>(
            $"/v1/market/elements/{elementId}/history?dna={catalogDna}&points=5");
        var altHistory = await client.GetFromJsonAsync<List<FactoryGame.Contracts.Market.MarketCandleDto>>(
            $"/v1/market/elements/{elementId}/history?dna={altDna}&points=5");

        Assert.NotNull(catalogHistory);
        Assert.NotNull(altHistory);
        Assert.NotEmpty(catalogHistory);
        Assert.NotEmpty(altHistory);
        Assert.NotEqual(catalogHistory[^1].Close, altHistory[^1].Close);
    }
}
