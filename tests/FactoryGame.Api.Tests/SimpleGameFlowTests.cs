using System.Net.Http.Json;
using System.Text.Json;
using FactoryGame.Contracts.Auth;
using FactoryGame.Contracts.Boards;
using FactoryGame.Contracts.Machines;
using FactoryGame.Contracts.Market;
using FactoryGame.Domain.Content;
using FactoryGame.Domain.Dna;
using FactoryGame.Domain.Market;
using FactoryGame.Domain.Boards;
using FactoryGame.Infrastructure.Data;
using FactoryGame.Infrastructure.Data.Entities;
using FactoryGame.Infrastructure.Hosting;
using FactoryGame.Infrastructure.Services;
using FactoryGame.Domain.Simulation;
using FactoryGame.Infrastructure.Simulation;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FactoryGame.Api.Tests;

/// <summary>
/// End-to-end spelflöde: köp grundämne → fabrik (seaport → maskin → seaport) → sälj.
/// </summary>
public sealed class SimpleGameFlowTests : IAsyncLifetime
{
    private const int BuyQuantity = 30;
    private const int SimulationTicks = 6;
    private const int BoughtElementId = 7;

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        var dbName = "FactoryGameSimpleFlow_" + Guid.NewGuid().ToString("N");
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={dbName};Mode=Memory;Cache=Shared");
            b.UseSetting("GameEconomy:SimulationTickIntervalSeconds", "1");
            b.UseSetting("GameEconomy:SimulationMaxCatchUpTicks", "8");
            b.UseSetting("MarketLiquidity:BackgroundRefreshEnabled", "false");
            b.UseSetting("MarketLiquidity:RefreshOnSummaryRequest", "true");
            b.UseSetting("MarketLiquidity:ElementRefreshCooldownMinutes", "0");
            b.UseSetting("Admin:BootstrapToken", "test-bootstrap");
            b.ConfigureServices(services =>
            {
                var hosted = services.Where(d =>
                    d.ServiceType == typeof(IHostedService) &&
                    d.ImplementationType == typeof(SimulationTickHostedService)).ToList();
                foreach (var d in hosted)
                    services.Remove(d);
            });
        });
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Buy_element_run_liquid_separator_factory_and_place_sell_orders()
    {
        const int elementId = BoughtElementId;
        var element = ElementCatalog.All.First(e => e.Id == elementId);
        Assert.Equal(MaterialPhase.Liquid, DnaDecoder.Decode(element.Dna).Phase);
        var referencePrice = ElementReferencePrice.Compute(element.Dna);

        var auth = await _client.PostAsJsonAsync("/v1/auth/guest",
            new GuestAuthRequest("simple-game-flow-" + Guid.NewGuid().ToString("N")));
        auth.EnsureSuccessStatusCode();
        var authBody = await auth.Content.ReadFromJsonAsync<GuestAuthResponse>();
        Assert.NotNull(authBody);
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {authBody.SessionToken}");

        var poolBeforeBuy = await GetPoolQuantityAsync(authBody.PlayerId, elementId);
        Assert.Equal(0, poolBeforeBuy);

        await _client.GetAsync("/v1/market/summary");

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var liquidity = scope.ServiceProvider.GetRequiredService<MarketLiquidityService>();
            await liquidity.EnsureLiquidityForElementAsync(elementId, force: true);
        }

        var depth = await _client.GetFromJsonAsync<MarketDepthDto>($"/v1/market/elements/{elementId}/depth");
        Assert.NotNull(depth);
        Assert.NotNull(depth.BestAsk);

        var buyLimit = Math.Max(referencePrice + 2m, depth.BestAsk.Value);
        var buy = await _client.PostAsJsonAsync("/v1/market/orders",
            new PlaceOrderRequest(elementId, "buy", buyLimit, BuyQuantity, "game-buy-element"));
        buy.EnsureSuccessStatusCode();
        var buyResult = await buy.Content.ReadFromJsonAsync<PlaceOrderResponse>();
        Assert.NotNull(buyResult);
        Assert.Equal("Filled", buyResult.Status, ignoreCase: true);
        Assert.Equal(BuyQuantity, buyResult.QuantityFilled);
        Assert.True(buyLimit > referencePrice, "Buy limit should be above reference price.");

        var poolAfterBuy = await GetPoolQuantityAsync(authBody.PlayerId, elementId);
        Assert.Equal(poolBeforeBuy + BuyQuantity, poolAfterBuy);

        var trades = await _client.GetFromJsonAsync<List<MarketTradeDto>>(
            $"/v1/market/trades?elementId={elementId}&limit=5");
        Assert.NotNull(trades);
        Assert.Contains(trades, t => t.Quantity == BuyQuantity);

        (await _client.PostAsJsonAsync("/v1/me/machine-inventory/purchase",
            new PurchaseMachineRequest("SeaportConnector"))).EnsureSuccessStatusCode();
        (await _client.PostAsJsonAsync("/v1/me/machine-inventory/purchase",
            new PurchaseMachineRequest("LiquidSeparator"))).EnsureSuccessStatusCode();

        var boardRes = await _client.PostAsJsonAsync("/v1/boards", new CreateBoardRequest("Simple game factory"));
        boardRes.EnsureSuccessStatusCode();
        var board = await boardRes.Content.ReadFromJsonAsync<BoardSummaryDto>();
        Assert.NotNull(board);

        var plan = new BoardPlanDto(
            [
                Machine("sea1", "SeaportConnector", "{\"outElementId\":" + elementId + "}"),
                Machine("sep1", "LiquidSeparator", """{"cutFreeze":2048}""")
            ],
            [
                new ConnectionDto("sea1", "out", "sep1", "in"),
                new ConnectionDto("sep1", "out1", "sea1", "in"),
                new ConnectionDto("sep1", "out2", "sea1", "in")
            ]);

        var save = await _client.PutAsJsonAsync($"/v1/boards/{board.Id}/plan", new SavePlanRequest(plan));
        save.EnsureSuccessStatusCode();

        var planBack = await _client.GetFromJsonAsync<BoardPlanDto>($"/v1/boards/{board.Id}/plan");
        Assert.NotNull(planBack);
        var seaSettings = planBack.Machines.First(m => m.Id == "sea1").Settings;
        Assert.NotNull(seaSettings);
        Assert.Equal(elementId, seaSettings.Value.GetProperty("outElementId").GetInt32());

        var infoBeforeStart = await _client.GetFromJsonAsync<BoardInfoDto>($"/v1/boards/{board.Id}/info");
        Assert.NotNull(infoBeforeStart);
        Assert.Equal(3, infoBeforeStart.PlanConnectionCount);
        Assert.True(infoBeforeStart.PlanHasCycle);
        Assert.Single(infoBeforeStart.Seaport.OutOfFactory);
        Assert.True(infoBeforeStart.Seaport.IntoFactory.Count >= 1);
        Assert.Contains(plan.Connections, c => c.FromId == "sep1" && c.FromPort == "out1");
        Assert.Contains(plan.Connections, c => c.FromId == "sep1" && c.FromPort == "out2");

        var start = await _client.PostAsync($"/v1/boards/{board.Id}/start", null);
        start.EnsureSuccessStatusCode();

        await AdvanceRunningBoardTicksAsync(board.Id, SimulationTicks);

        var lastKeyframe = await _client.GetFromJsonAsync<BoardKeyframeDto>(
            $"/v1/boards/{board.Id}/keyframes/latest");
        Assert.NotNull(lastKeyframe);
        Assert.Equal("Running", lastKeyframe.Mode);

        var poll = await _client.GetFromJsonAsync<BoardKeyframesResponseDto>(
            $"/v1/boards/{board.Id}/keyframes?afterTick=0");
        Assert.NotNull(poll);
        Assert.NotEmpty(poll.Keyframes);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var boardNote = await db.Boards.AsNoTracking()
                .Where(b => b.Id == board.Id)
                .Select(b => b.LastSnapshotNote)
                .FirstAsync();
            Assert.Contains("active=2", boardNote);
            Assert.Contains("withdrawn=1", boardNote);
            Assert.Contains("deposited=", boardNote);
        }

        var infoRunning = await _client.GetFromJsonAsync<BoardInfoDto>($"/v1/boards/{board.Id}/info");
        Assert.NotNull(infoRunning);
        Assert.Equal("Running", infoRunning.Mode);
        var seaOut = infoRunning.SeaportPorts.First(p =>
            p.MachineId == "sea1" && p.Port.Equals("out", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(elementId, seaOut.ElementId);

        var poolAfterRun = await GetPoolQuantityAsync(authBody.PlayerId, elementId);
        Assert.True(poolAfterRun >= 2, "Need pool stock for two sell orders.");

        await _client.PostAsync($"/v1/boards/{board.Id}/stop", null);

        var sellPrice = referencePrice;
        var sellHeavy = await _client.PostAsJsonAsync("/v1/market/orders",
            new PlaceOrderRequest(elementId, "sell", sellPrice, 1, "game-sell-fraction-1"));
        sellHeavy.EnsureSuccessStatusCode();
        var sellHeavyBody = await sellHeavy.Content.ReadFromJsonAsync<PlaceOrderResponse>();
        Assert.NotNull(sellHeavyBody);
        Assert.Equal("Open", sellHeavyBody.Status, ignoreCase: true);

        var sellLight = await _client.PostAsJsonAsync("/v1/market/orders",
            new PlaceOrderRequest(elementId, "sell", sellPrice + 1m, 1, "game-sell-fraction-2"));
        sellLight.EnsureSuccessStatusCode();
        var sellLightBody = await sellLight.Content.ReadFromJsonAsync<PlaceOrderResponse>();
        Assert.NotNull(sellLightBody);
        Assert.Equal("Open", sellLightBody.Status, ignoreCase: true);
    }

    private async Task AdvanceRunningBoardTicksAsync(Guid boardId, int tickCount)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var runner = scope.ServiceProvider.GetRequiredService<BoardSimulationRunner>();
        var board = await db.Boards.FirstAsync(b => b.Id == boardId);
        var clock = await db.SimulationClock.FirstOrDefaultAsync(c => c.Id == 1);
        if (clock == null)
        {
            clock = new SimulationClockEntity { Id = 1, CurrentTick = 0, LastAdvancedAt = DateTimeOffset.UtcNow };
            db.SimulationClock.Add(clock);
            await db.SaveChangesAsync();
        }

        for (var i = 0; i < tickCount; i++)
        {
            clock.CurrentTick++;
            await runner.TickBoardAsync(board, clock.CurrentTick, CancellationToken.None);
        }

        board.SimulationTick = clock.CurrentTick;
        clock.LastAdvancedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }

    private static MachineDto Machine(string id, string type, string? settingsJson = null)
    {
        if (settingsJson == null)
            return new MachineDto(id, type);

        var settings = JsonSerializer.Deserialize<JsonElement>(settingsJson);
        return new MachineDto(id, type, settings);
    }

    private async Task<long> GetPoolQuantityAsync(Guid playerId, int elementId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stack = await db.PoolStacks.AsNoTracking()
            .FirstOrDefaultAsync(s => s.PlayerId == playerId && s.ElementId == elementId);
        return stack?.Quantity ?? 0;
    }
}
