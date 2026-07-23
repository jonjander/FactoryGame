using FactoryGame.Domain.Content;
using FactoryGame.Infrastructure.Data;
using FactoryGame.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryGame.Api.Tests;

public sealed class MarketHistorySeedTests : IClassFixture<ApiWebApplicationFixture>
{
    private readonly ApiWebApplicationFixture _fixture;

    public MarketHistorySeedTests(ApiWebApplicationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task History_seed_is_idempotent()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var liquidity = scope.ServiceProvider.GetRequiredService<MarketLiquidityService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        const int elementId = 2;
        var catalogDna = ElementCatalogLookup.CatalogDnaFor(elementId);
        await liquidity.EnsureLiquidityForElementAsync(elementId, force: true, dna: catalogDna);
        var count1 = await db.MarketPriceCandles.CountAsync(c => c.ElementId == elementId && c.Dna == catalogDna);

        await liquidity.EnsureLiquidityForElementAsync(elementId, force: true, dna: catalogDna);
        var count2 = await db.MarketPriceCandles.CountAsync(c => c.ElementId == elementId && c.Dna == catalogDna);

        Assert.True(count1 > 0);
        Assert.Equal(count1, count2);
    }
}
