using System.Net.Http.Json;
using FactoryGame.Contracts.Admin;
using FactoryGame.Contracts.Auth;
using FactoryGame.Contracts.Market;
using FactoryGame.Domain.Content;
using FactoryGame.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryGame.Api.Tests;

public sealed class SponsorCompanyMarketTests
{
    private const int ElementId = 1;
    private static readonly long ElementDna = ElementCatalogLookup.CatalogDnaFor(ElementId);
    private const string AdminToken = "test-bootstrap";

    [Fact]
    public async Task Player_sell_matches_sponsor_buy_and_shows_company_name()
    {
        await using var factory = CreateFactory();

        var admin = factory.CreateClient();
        var player = factory.CreateClient();

        var company = await CreateCompanyAsync(admin, "Företag AB", fundingMode: "Budget", budget: 50_000m);
        await CreateStandingOrderAsync(admin, company.Id, "buy", limitPrice: 12m, qty: 100);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var trading = scope.ServiceProvider.GetRequiredService<SponsorCompanyTradingService>();
            await trading.RefreshAllActiveCompaniesAsync();
        }

        var auth = await player.PostAsJsonAsync("/v1/auth/guest",
            new GuestAuthRequest("sponsor-sell-" + Guid.NewGuid().ToString("N")));
        auth.EnsureSuccessStatusCode();
        var body = await auth.Content.ReadFromJsonAsync<GuestAuthResponse>();
        Assert.NotNull(body);
        player.DefaultRequestHeaders.Add("Authorization", $"Bearer {body.SessionToken}");
        await player.GetAsync("/v1/market/summary");

        var sell = await player.PostAsJsonAsync("/v1/market/orders",
            new PlaceOrderRequest(ElementId, ElementDna, "sell", 12m, 5, "sponsor-match-sell"));
        sell.EnsureSuccessStatusCode();
        var sellResult = await sell.Content.ReadFromJsonAsync<PlaceOrderResponse>();
        Assert.NotNull(sellResult);
        Assert.Equal("Filled", sellResult.Status, ignoreCase: true);

        var trades = await player.GetFromJsonAsync<List<MarketTradeDto>>(
            $"/v1/market/trades?elementId={ElementId}&limit=20");
        Assert.NotNull(trades);
        var match = trades.FirstOrDefault(t => t.Dna == ElementDna && t.Quantity == 5);
        Assert.NotNull(match);
        Assert.True(match.BuyerIsSponsor);
        Assert.Equal("Företag AB", match.BuyerLabel);
    }

    [Fact]
    public async Task Two_sponsors_do_not_trade_with_each_other()
    {
        await using var factory = CreateFactory();

        var admin = factory.CreateClient();
        var a = await CreateCompanyAsync(admin, "Sponsor A", "Budget", 100_000m);
        var b = await CreateCompanyAsync(admin, "Sponsor B", "Budget", 100_000m);

        await CreateStandingOrderAsync(admin, a.Id, "buy", 15m, 50);
        await CreateStandingOrderAsync(admin, b.Id, "sell", 10m, 50);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var trading = scope.ServiceProvider.GetRequiredService<SponsorCompanyTradingService>();
            await trading.RefreshAllActiveCompaniesAsync();
            await trading.RefreshAllActiveCompaniesAsync();
        }

        var trades = await admin.GetFromJsonAsync<List<MarketTradeDto>>(
            $"/v1/market/trades?elementId={ElementId}&limit=50");
        Assert.NotNull(trades);
        Assert.DoesNotContain(trades, t =>
            t.BuyerIsSponsor && t.SellerIsSponsor);
    }

    [Fact]
    public async Task Utopia_sponsor_increases_virtual_spend_on_trade()
    {
        await using var factory = CreateFactory();
        var admin = factory.CreateClient();
        var company = await CreateCompanyAsync(admin, "Utopia Co", "Utopia", null);
        await CreateStandingOrderAsync(admin, company.Id, "buy", 11m, 30);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var trading = scope.ServiceProvider.GetRequiredService<SponsorCompanyTradingService>();
            await trading.RefreshAllActiveCompaniesAsync();
        }

        var player = factory.CreateClient();
        var auth = await player.PostAsJsonAsync("/v1/auth/guest",
            new GuestAuthRequest("utopia-sell-" + Guid.NewGuid().ToString("N")));
        auth.EnsureSuccessStatusCode();
        var body = await auth.Content.ReadFromJsonAsync<GuestAuthResponse>();
        Assert.NotNull(body);
        player.DefaultRequestHeaders.Add("Authorization", $"Bearer {body.SessionToken}");
        await player.GetAsync("/v1/market/summary");

        var sell = await player.PostAsJsonAsync("/v1/market/orders",
            new PlaceOrderRequest(ElementId, ElementDna, "sell", 11m, 4, "utopia-sell"));
        sell.EnsureSuccessStatusCode();

        var updated = await GetCompanyAsync(admin, company.Id);
        Assert.True(updated.VirtualSpend >= 44m);
    }

    [Fact]
    public async Task Budget_sponsor_depletes_budget_on_buy_trades()
    {
        await using var factory = CreateFactory();
        var admin = factory.CreateClient();
        var company = await CreateCompanyAsync(admin, "Budget Co", "Budget", 30m);
        await CreateStandingOrderAsync(admin, company.Id, "buy", 10m, 10);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var trading = scope.ServiceProvider.GetRequiredService<SponsorCompanyTradingService>();
            await trading.RefreshAllActiveCompaniesAsync();
        }

        var player = factory.CreateClient();
        var auth = await player.PostAsJsonAsync("/v1/auth/guest",
            new GuestAuthRequest("budget-sell-" + Guid.NewGuid().ToString("N")));
        auth.EnsureSuccessStatusCode();
        var body = await auth.Content.ReadFromJsonAsync<GuestAuthResponse>();
        Assert.NotNull(body);
        player.DefaultRequestHeaders.Add("Authorization", $"Bearer {body.SessionToken}");
        await player.GetAsync("/v1/market/summary");

        var sell = await player.PostAsJsonAsync("/v1/market/orders",
            new PlaceOrderRequest(ElementId, ElementDna, "sell", 10m, 3, "budget-sell"));
        sell.EnsureSuccessStatusCode();

        var updated = await GetCompanyAsync(admin, company.Id);
        Assert.True(updated.BudgetRemaining <= 0m);
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        var dbName = "FactoryGameSponsorTest_" + Guid.NewGuid().ToString("N");
        return new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={dbName};Mode=Memory;Cache=Shared");
            b.UseSetting("GameEconomy:SimulationTickIntervalSeconds", "600");
            b.UseSetting("MarketLiquidity:Enabled", "false");
            b.UseSetting("MarketLiquidity:BackgroundRefreshEnabled", "false");
            b.UseSetting("MarketLiquidity:RefreshOnSummaryRequest", "false");
            b.UseSetting("SponsorCompany:BackgroundRefreshEnabled", "false");
            b.UseSetting("Admin:BootstrapToken", AdminToken);
        });
    }

    private static async Task<SponsorCompanyDto> CreateCompanyAsync(
        HttpClient admin,
        string name,
        string fundingMode,
        decimal? budget)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/admin/companies");
        req.Headers.Add("X-Admin-Token", AdminToken);
        req.Content = JsonContent.Create(new CreateSponsorCompanyRequest(
            name, "Test sponsor", "", fundingMode, budget, 3));
        var res = await admin.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var company = await res.Content.ReadFromJsonAsync<SponsorCompanyDto>();
        Assert.NotNull(company);
        return company;
    }

    private static async Task<SponsorCompanyDto> GetCompanyAsync(HttpClient admin, Guid id)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/v1/admin/companies/{id}");
        req.Headers.Add("X-Admin-Token", AdminToken);
        var res = await admin.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var company = await res.Content.ReadFromJsonAsync<SponsorCompanyDto>();
        Assert.NotNull(company);
        return company;
    }

    private static async Task CreateStandingOrderAsync(
        HttpClient admin,
        Guid companyId,
        string side,
        decimal limitPrice,
        long qty)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/v1/admin/companies/{companyId}/orders");
        req.Headers.Add("X-Admin-Token", AdminToken);
        req.Content = JsonContent.Create(new CreateSponsorCompanyOrderRequest(
            ElementId, ElementDna, side, limitPrice, qty));
        var res = await admin.SendAsync(req);
        res.EnsureSuccessStatusCode();
    }
}
