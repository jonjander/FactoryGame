using System.Net.Http.Json;
using System.Text.Json;
using FactoryGame.Contracts.Auth;
using FactoryGame.Contracts.Boards;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FactoryGame.Api.Tests;

public sealed class BoardCyclePlanTests : IClassFixture<ApiWebApplicationFixture>
{
    private const long E03CatalogDna = 144964032628459529L;

    private readonly ApiWebApplicationFixture _fixture;

    public BoardCyclePlanTests(ApiWebApplicationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Save_plan_with_seaport_boiler_loop_succeeds()
    {
        var client = _fixture.Factory.CreateClient();
        var auth = await client.PostAsJsonAsync("/v1/auth/guest", new GuestAuthRequest("board-cycle-save-1"));
        auth.EnsureSuccessStatusCode();
        var body = await auth.Content.ReadFromJsonAsync<GuestAuthResponse>();
        Assert.NotNull(body);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {body.SessionToken}");

        await client.GetAsync("/v1/me/wallet");

        var boardRes = await client.PostAsJsonAsync("/v1/boards", new CreateBoardRequest("Loop"));
        boardRes.EnsureSuccessStatusCode();
        var board = await boardRes.Content.ReadFromJsonAsync<BoardSummaryDto>();
        Assert.NotNull(board);

        var seaportSettings = JsonSerializer.SerializeToElement(new
        {
            outElementId = 3,
            outMaterialDna = E03CatalogDna.ToString()
        });

        var plan = new BoardPlanDto(
            [
                new MachineDto("seaportconnector1", "SeaportConnector", seaportSettings),
                new MachineDto("boiler1", "Boiler")
            ],
            [
                new ConnectionDto("seaportconnector1", "out", "boiler1", "in"),
                new ConnectionDto("boiler1", "out", "seaportconnector1", "in")
            ]);

        var save = await client.PutAsJsonAsync($"/v1/boards/{board.Id}/plan", new SavePlanRequest(plan));
        save.EnsureSuccessStatusCode();

        var info = await client.GetFromJsonAsync<BoardInfoDto>($"/v1/boards/{board.Id}/info");
        Assert.NotNull(info);
        Assert.Equal(2, info.PlanConnectionCount);
        Assert.True(info.PlanHasCycle);
        Assert.Single(info.Seaport.IntoFactory);
        Assert.Single(info.Seaport.OutOfFactory);

        var planBack = await client.GetFromJsonAsync<BoardPlanDto>($"/v1/boards/{board.Id}/plan");
        Assert.NotNull(planBack);
        Assert.Equal(2, planBack.Connections.Count);
    }

    [Fact]
    public async Task Start_seaport_boiler_e03_loop_produces_without_errors()
    {
        await using var factory = TestWebHostBuilderExtensions.CreateFactoryGameTestFactory(b =>
        {
            b.UseSetting("GameEconomy:SimulationTickIntervalSeconds", "1");
            b.UseSetting("GameEconomy:SimulationMaxCatchUpTicks", "5");
        });

        var client = factory.CreateClient();
        var auth = await client.PostAsJsonAsync("/v1/auth/guest",
            new GuestAuthRequest("board-cycle-run-" + Guid.NewGuid().ToString("N")));
        auth.EnsureSuccessStatusCode();
        var body = await auth.Content.ReadFromJsonAsync<GuestAuthResponse>();
        Assert.NotNull(body);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {body.SessionToken}");

        await client.GetAsync("/v1/me/wallet");

        var boardRes = await client.PostAsJsonAsync("/v1/boards", new CreateBoardRequest("Loop run"));
        boardRes.EnsureSuccessStatusCode();
        var board = await boardRes.Content.ReadFromJsonAsync<BoardSummaryDto>();
        Assert.NotNull(board);

        var seaportSettings = JsonSerializer.SerializeToElement(new
        {
            outElementId = 3,
            outMaterialDna = E03CatalogDna.ToString()
        });

        var plan = new BoardPlanDto(
            [
                new MachineDto("seaportconnector1", "SeaportConnector", seaportSettings),
                new MachineDto("boiler1", "Boiler")
            ],
            [
                new ConnectionDto("seaportconnector1", "out", "boiler1", "in"),
                new ConnectionDto("boiler1", "out", "seaportconnector1", "in")
            ]);

        var save = await client.PutAsJsonAsync($"/v1/boards/{board.Id}/plan", new SavePlanRequest(plan));
        save.EnsureSuccessStatusCode();

        var start = await client.PostAsync($"/v1/boards/{board.Id}/start", null);
        start.EnsureSuccessStatusCode();

        BoardInfoDto? info = null;
        for (var i = 0; i < 12; i++)
        {
            await Task.Delay(500);
            info = await client.GetFromJsonAsync<BoardInfoDto>($"/v1/boards/{board.Id}/info");
            if (info?.Throughput.TotalUnitsPerSecond > 0)
                break;
        }

        Assert.NotNull(info);
        Assert.DoesNotContain(info.Issues, i => i.Severity == "error");
        Assert.True(info.Throughput.TotalUnitsPerSecond > 0, "expected positive throughput for E03+Boiler loop");
    }
}
