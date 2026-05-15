using System.Net.Http.Json;
using FactoryGame.Contracts.Auth;
using FactoryGame.Contracts.Pool;
using FactoryGame.Domain.Content;
using FactoryGame.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryGame.Api.Tests;

public sealed class PoolViewTests : IClassFixture<ApiWebApplicationFixture>
{
    private readonly ApiWebApplicationFixture _fixture;

    public PoolViewTests(ApiWebApplicationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Pool_view_returns_line_values_and_total()
    {
        var client = await CreateAuthedClientAsync();
        var overview = await client.GetFromJsonAsync<PoolOverviewDto>("/v1/me/pool/view");
        Assert.NotNull(overview);
        Assert.True(overview.MaxVolume > 0);
        Assert.Equal(5, overview.Stacks.Count);

        decimal sum = 0;
        foreach (var row in overview.Stacks)
        {
            Assert.Equal(Math.Round(row.LastPrice * row.Quantity, 2), row.LineValue);
            Assert.Equal(20, row.CatalogSize);
            Assert.InRange(row.PriceRank, 1, 20);
            sum += row.LineValue;
        }

        Assert.Equal(sum, overview.TotalEstimatedValue);
    }

    [Fact]
    public async Task Global_price_rank_orders_by_last_price_descending()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var query = scope.ServiceProvider.GetRequiredService<MarketQueryService>();
        var ranks = await query.GetGlobalPriceRanksAsync();

        Assert.Equal(ElementCatalog.All.Count, ranks.Count);

        var prices = new List<(int Id, decimal Price)>();
        foreach (var element in ElementCatalog.All)
        {
            var (price, _) = await query.GetLastPriceAndChangeAsync(element.Id, element.Dna, default);
            prices.Add((element.Id, price));
        }

        var expectedOrder = prices
            .OrderByDescending(p => p.Price)
            .ThenBy(p => p.Id)
            .Select((p, i) => (p.Id, Rank: i + 1))
            .ToList();

        foreach (var (id, rank) in expectedOrder)
            Assert.Equal(rank, ranks[id]);

        var highest = expectedOrder[0];
        var lowest = expectedOrder[^1];
        Assert.Equal(1, ranks[highest.Id]);
        Assert.Equal(ElementCatalog.All.Count, ranks[lowest.Id]);
    }

    private async Task<HttpClient> CreateAuthedClientAsync()
    {
        var client = _fixture.Factory.CreateClient();
        var auth = await client.PostAsJsonAsync("/v1/auth/guest",
            new GuestAuthRequest("pool-view-" + Guid.NewGuid().ToString("N")));
        auth.EnsureSuccessStatusCode();
        var body = await auth.Content.ReadFromJsonAsync<GuestAuthResponse>();
        Assert.NotNull(body);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {body.SessionToken}");
        return client;
    }
}
