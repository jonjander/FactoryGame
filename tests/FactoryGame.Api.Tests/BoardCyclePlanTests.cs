using System.Net.Http.Json;
using System.Text.Json;
using FactoryGame.Contracts.Auth;
using FactoryGame.Contracts.Boards;

namespace FactoryGame.Api.Tests;

public sealed class BoardCyclePlanTests : IClassFixture<ApiWebApplicationFixture>
{
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

        var boardRes = await client.PostAsJsonAsync("/v1/boards", new CreateBoardRequest("Loop"));
        boardRes.EnsureSuccessStatusCode();
        var board = await boardRes.Content.ReadFromJsonAsync<BoardSummaryDto>();
        Assert.NotNull(board);

        var plan = new BoardPlanDto(
            [
                new MachineDto("seaportconnector1", "SeaportConnector"),
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
}
