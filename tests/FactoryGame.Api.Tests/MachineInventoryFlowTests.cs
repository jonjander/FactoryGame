using System.Net;
using System.Net.Http.Json;
using FactoryGame.Contracts.Auth;
using FactoryGame.Contracts.Boards;
using FactoryGame.Contracts.Machines;

namespace FactoryGame.Api.Tests;

public sealed class MachineInventoryFlowTests : IClassFixture<ApiWebApplicationFixture>
{
    private readonly ApiWebApplicationFixture _fixture;

    public MachineInventoryFlowTests(ApiWebApplicationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Purchase_place_from_stock_and_plan_roundtrip()
    {
        var client = _fixture.Factory.CreateClient();
        var auth = await client.PostAsJsonAsync("/v1/auth/guest", new GuestAuthRequest("integration-machine-stock-1"));
        auth.EnsureSuccessStatusCode();
        var body = await auth.Content.ReadFromJsonAsync<GuestAuthResponse>();
        Assert.NotNull(body);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {body.SessionToken}");

        var storeRes = await client.GetAsync("/v1/content/machine-store");
        storeRes.EnsureSuccessStatusCode();

        var purchase = await client.PostAsJsonAsync("/v1/me/machine-inventory/purchase", new PurchaseMachineRequest("Boiler"));
        purchase.EnsureSuccessStatusCode();

        var inv = await client.GetFromJsonAsync<List<PlayerMachineStockDto>>("/v1/me/machine-inventory");
        Assert.NotNull(inv);
        Assert.Single(inv);
        var stockId = inv[0].Id;

        var boardRes = await client.PostAsJsonAsync("/v1/boards", new CreateBoardRequest("Plan A"));
        boardRes.EnsureSuccessStatusCode();
        var board = await boardRes.Content.ReadFromJsonAsync<BoardSummaryDto>();
        Assert.NotNull(board);

        var place = await client.PostAsJsonAsync($"/v1/boards/{board.Id}/place-from-stock",
            new PlaceMachineFromStockRequest(stockId, "boiler1"));
        place.EnsureSuccessStatusCode();

        var inv2 = await client.GetFromJsonAsync<List<PlayerMachineStockDto>>("/v1/me/machine-inventory");
        Assert.NotNull(inv2);
        Assert.Empty(inv2);

        var planRes = await client.GetAsync($"/v1/boards/{board.Id}/plan");
        planRes.EnsureSuccessStatusCode();
        var plan = await planRes.Content.ReadFromJsonAsync<BoardPlanDto>();
        Assert.NotNull(plan);
        Assert.Single(plan.Machines);
        Assert.Equal("boiler1", plan.Machines[0].Id);
        Assert.Equal("Boiler", plan.Machines[0].Type);
    }

    [Fact]
    public async Task Purchase_rejects_unknown_type()
    {
        var client = _fixture.Factory.CreateClient();
        var auth = await client.PostAsJsonAsync("/v1/auth/guest", new GuestAuthRequest("integration-machine-stock-2"));
        auth.EnsureSuccessStatusCode();
        var body = await auth.Content.ReadFromJsonAsync<GuestAuthResponse>();
        Assert.NotNull(body);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {body.SessionToken}");

        var purchase = await client.PostAsJsonAsync("/v1/me/machine-inventory/purchase", new PurchaseMachineRequest("NotAMachine"));
        Assert.Equal(HttpStatusCode.BadRequest, purchase.StatusCode);
    }
}
