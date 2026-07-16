using System.Net.Http.Json;
using FactoryGame.Contracts.Auth;
using FactoryGame.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryGame.Api.Tests;

public sealed class StarterPoolTests : IClassFixture<ApiWebApplicationFixture>
{
    private readonly ApiWebApplicationFixture _fixture;

    public StarterPoolTests(ApiWebApplicationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task New_guest_receives_five_starter_elements()
    {
        var client = _fixture.Factory.CreateClient();
        var auth = await client.PostAsJsonAsync("/v1/auth/guest",
            new GuestAuthRequest("starter-pack-" + Guid.NewGuid().ToString("N")));
        auth.EnsureSuccessStatusCode();
        var body = await auth.Content.ReadFromJsonAsync<GuestAuthResponse>();
        Assert.NotNull(body);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {body.SessionToken}");

        var walletRes = await client.GetAsync("/v1/me/wallet");
        walletRes.EnsureSuccessStatusCode();

        var poolRes = await client.GetAsync("/v1/me/pool");
        poolRes.EnsureSuccessStatusCode();

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stacks = await db.PoolStacks.AsNoTracking()
            .Where(s => s.PlayerId == body.PlayerId && s.Quantity > 0)
            .OrderBy(s => s.ElementId)
            .ToListAsync();

        Assert.Equal(5, stacks.Count);
        Assert.Equal([1, 2, 3, 4, 5], stacks.Select(s => s.ElementId).ToArray());
        Assert.All(stacks, s => Assert.Equal(25, s.Quantity));

        var starterTxn = await db.EconomyTransactions.AsNoTracking()
            .FirstOrDefaultAsync(t => t.PlayerId == body.PlayerId && t.Type == "StarterPool");
        Assert.NotNull(starterTxn);
        Assert.Equal("1,2,3,4,5", starterTxn.Metadata);
    }

    [Fact]
    public async Task Market_summary_includes_best_ask_for_starter_holdings()
    {
        var client = _fixture.Factory.CreateClient();
        var auth = await client.PostAsJsonAsync("/v1/auth/guest",
            new GuestAuthRequest("summary-ask-" + Guid.NewGuid().ToString("N")));
        auth.EnsureSuccessStatusCode();
        var body = await auth.Content.ReadFromJsonAsync<GuestAuthResponse>();
        Assert.NotNull(body);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {body.SessionToken}");

        var summaryRes = await client.GetAsync("/v1/market/summary");
        summaryRes.EnsureSuccessStatusCode();
        var summary = await summaryRes.Content.ReadFromJsonAsync<List<FactoryGame.Contracts.Market.MarketElementSummaryDto>>();
        Assert.NotNull(summary);
        Assert.NotEmpty(summary);
        Assert.Contains(summary, row => row.PoolQuantity > 0 && row.BestAsk is > 0);
    }

    [Fact]
    public async Task Legacy_player_without_starter_gets_pack_on_market_summary()
    {
        var client = _fixture.Factory.CreateClient();
        var deviceKey = "legacy-empty-" + Guid.NewGuid().ToString("N");
        var auth = await client.PostAsJsonAsync("/v1/auth/guest", new GuestAuthRequest(deviceKey));
        auth.EnsureSuccessStatusCode();
        var body = await auth.Content.ReadFromJsonAsync<GuestAuthResponse>();
        Assert.NotNull(body);

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stacks = await db.PoolStacks.Where(s => s.PlayerId == body.PlayerId).ToListAsync();
            db.PoolStacks.RemoveRange(stacks);
            var starterTxn = await db.EconomyTransactions
                .Where(t => t.PlayerId == body.PlayerId && t.Type == "StarterPool")
                .ToListAsync();
            db.EconomyTransactions.RemoveRange(starterTxn);
            await db.SaveChangesAsync();
        }

        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {body.SessionToken}");
        var summary = await client.GetAsync("/v1/market/summary");
        summary.EnsureSuccessStatusCode();

        await using var verifyScope = _fixture.Factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await verifyDb.PoolStacks.CountAsync(s => s.PlayerId == body.PlayerId && s.Quantity > 0);
        Assert.Equal(5, count);
    }
}
