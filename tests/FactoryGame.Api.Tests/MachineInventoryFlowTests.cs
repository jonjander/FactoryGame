using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
    public async Task Return_to_stock_after_place_restores_inventory()
    {
        var client = _fixture.Factory.CreateClient();
        var auth = await client.PostAsJsonAsync("/v1/auth/guest", new GuestAuthRequest("integration-return-stock"));
        auth.EnsureSuccessStatusCode();
        var body = await auth.Content.ReadFromJsonAsync<GuestAuthResponse>();
        Assert.NotNull(body);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {body.SessionToken}");

        var purchase = await client.PostAsJsonAsync("/v1/me/machine-inventory/purchase", new PurchaseMachineRequest("Boiler"));
        purchase.EnsureSuccessStatusCode();

        var inv = await client.GetFromJsonAsync<List<PlayerMachineStockDto>>("/v1/me/machine-inventory");
        Assert.NotNull(inv);
        Assert.Single(inv);
        var stockId = inv[0].Id;

        var boardRes = await client.PostAsJsonAsync("/v1/boards", new CreateBoardRequest("Return test"));
        boardRes.EnsureSuccessStatusCode();
        var board = await boardRes.Content.ReadFromJsonAsync<BoardSummaryDto>();
        Assert.NotNull(board);

        var place = await client.PostAsJsonAsync($"/v1/boards/{board.Id}/place-from-stock",
            new PlaceMachineFromStockRequest(stockId, "boiler1"));
        place.EnsureSuccessStatusCode();

        var returnRes = await client.PostAsJsonAsync($"/v1/boards/{board.Id}/return-to-stock",
            new ReturnMachineToStockRequest("boiler1"));
        returnRes.EnsureSuccessStatusCode();

        var invAfter = await client.GetFromJsonAsync<List<PlayerMachineStockDto>>("/v1/me/machine-inventory");
        Assert.NotNull(invAfter);
        Assert.Single(invAfter);
        Assert.Equal("Boiler", invAfter[0].MachineType);

        var plan = await client.GetFromJsonAsync<BoardPlanDto>($"/v1/boards/{board.Id}/plan");
        Assert.NotNull(plan);
        Assert.Empty(plan.Machines);
    }

    [Fact]
    public void BoardPlanDto_roundtrips_connections()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        var plan = new BoardPlanDto(
            [new MachineDto("sea1", "SeaportConnector")],
            [new ConnectionDto("sea1", "out", "mix1", "in1")]);
        var json = JsonSerializer.Serialize(plan, options);
        var back = JsonSerializer.Deserialize<BoardPlanDto>(json, options);
        Assert.NotNull(back);
        Assert.Equal("out", back!.Connections[0].FromPort);
    }

    [Fact]
    public void SavePlanRequest_roundtrips_connections()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        var plan = new BoardPlanDto(
            [new MachineDto("sea1", "SeaportConnector")],
            [new ConnectionDto("sea1", "out", "mix1", "in1")]);
        var json = JsonSerializer.Serialize(new SavePlanRequest(plan), options);
        var back = JsonSerializer.Deserialize<SavePlanRequest>(json, options);
        Assert.NotNull(back);
        Assert.Equal("out", back!.Plan.Connections[0].FromPort);
    }

    [Fact]
    public void ConnectionDto_roundtrips_camelCase()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        var original = new ConnectionDto("sea1", "out", "mix1", "in1");
        var json = JsonSerializer.Serialize(original, options);
        var back = JsonSerializer.Deserialize<ConnectionDto>(json, options);
        Assert.NotNull(back);
        Assert.Equal(original.FromId, back.FromId);
        Assert.Equal(original.FromPort, back.FromPort);
        Assert.Equal(original.ToId, back.ToId);
        Assert.Equal(original.ToPort, back.ToPort);
    }

    [Fact]
    public async Task Board_info_reports_seaport_flows()
    {
        var client = _fixture.Factory.CreateClient();
        var auth = await client.PostAsJsonAsync("/v1/auth/guest", new GuestAuthRequest("integration-board-info"));
        auth.EnsureSuccessStatusCode();
        var body = await auth.Content.ReadFromJsonAsync<GuestAuthResponse>();
        Assert.NotNull(body);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {body.SessionToken}");

        var boardRes = await client.PostAsJsonAsync("/v1/boards", new CreateBoardRequest("Info test"));
        boardRes.EnsureSuccessStatusCode();
        var board = await boardRes.Content.ReadFromJsonAsync<BoardSummaryDto>();
        Assert.NotNull(board);

        var plan = new BoardPlanDto(
        [
            new MachineDto("sea1", "SeaportConnector"),
            new MachineDto("mix1", "Mixer")
        ],
        [
            new ConnectionDto("sea1", "out", "mix1", "in1")
        ]);
        var save = await client.PutAsJsonAsync($"/v1/boards/{board.Id}/plan", new SavePlanRequest(plan));
        save.EnsureSuccessStatusCode();

        var loaded = await client.GetFromJsonAsync<BoardPlanDto>($"/v1/boards/{board.Id}/plan");
        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.Machines.Count);
        Assert.Single(loaded.Connections);
        Assert.Equal("sea1", loaded.Connections[0].FromId);
        Assert.Equal("out", loaded.Connections[0].FromPort);

        var info = await client.GetFromJsonAsync<BoardInfoDto>($"/v1/boards/{board.Id}/info");
        Assert.NotNull(info);
        Assert.Single(info.Seaport.IntoFactory);
        Assert.Contains(info.Issues, i => i.Code == "port_unconnected");
    }

    [Fact]
    public async Task Purchase_seaport_connector_succeeds()
    {
        var client = _fixture.Factory.CreateClient();
        var auth = await client.PostAsJsonAsync("/v1/auth/guest", new GuestAuthRequest("integration-seaport-connector"));
        auth.EnsureSuccessStatusCode();
        var body = await auth.Content.ReadFromJsonAsync<GuestAuthResponse>();
        Assert.NotNull(body);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {body.SessionToken}");

        var purchase = await client.PostAsJsonAsync("/v1/me/machine-inventory/purchase", new PurchaseMachineRequest("SeaportConnector"));
        purchase.EnsureSuccessStatusCode();

        var inv = await client.GetFromJsonAsync<List<PlayerMachineStockDto>>("/v1/me/machine-inventory");
        Assert.NotNull(inv);
        Assert.Contains(inv, x => x.MachineType == "SeaportConnector");
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
