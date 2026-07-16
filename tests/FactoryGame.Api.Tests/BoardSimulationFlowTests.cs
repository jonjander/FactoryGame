using System.Net.Http.Json;
using FactoryGame.Contracts.Auth;
using FactoryGame.Contracts.Boards;
using FactoryGame.Contracts.Machines;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FactoryGame.Api.Tests;

public sealed class BoardSimulationFlowTests : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _factory = TestWebHostBuilderExtensions.CreateFactoryGameTestFactory(b =>
        {
            b.UseSetting("GameEconomy:SimulationTickIntervalSeconds", "1");
            b.UseSetting("GameEconomy:SimulationMaxCatchUpTicks", "5");
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
    public async Task Start_running_board_produces_keyframe_and_info()
    {
        var auth = await _client.PostAsJsonAsync("/v1/auth/guest", new GuestAuthRequest("board-sim-flow-1"));
        auth.EnsureSuccessStatusCode();
        var body = await auth.Content.ReadFromJsonAsync<GuestAuthResponse>();
        Assert.NotNull(body);
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {body.SessionToken}");

        var purchase = await _client.PostAsJsonAsync("/v1/me/machine-inventory/purchase",
            new PurchaseMachineRequest("SeaportConnector"));
        purchase.EnsureSuccessStatusCode();
        var inv = await _client.GetFromJsonAsync<List<PlayerMachineStockDto>>("/v1/me/machine-inventory");
        Assert.NotNull(inv);
        Assert.NotEmpty(inv);

        var boardRes = await _client.PostAsJsonAsync("/v1/boards", new CreateBoardRequest("Sim board"));
        boardRes.EnsureSuccessStatusCode();
        var board = await boardRes.Content.ReadFromJsonAsync<BoardSummaryDto>();
        Assert.NotNull(board);

        var place = await _client.PostAsJsonAsync($"/v1/boards/{board.Id}/place-from-stock",
            new PlaceMachineFromStockRequest(inv[0].Id, "sea1"));
        place.EnsureSuccessStatusCode();

        var start = await _client.PostAsync($"/v1/boards/{board.Id}/start", null);
        start.EnsureSuccessStatusCode();

        BoardKeyframeDto? keyframe = null;
        for (var i = 0; i < 8; i++)
        {
            await Task.Delay(500);
            var kfRes = await _client.GetAsync($"/v1/boards/{board.Id}/keyframes/latest");
            if (kfRes.IsSuccessStatusCode)
            {
                keyframe = await kfRes.Content.ReadFromJsonAsync<BoardKeyframeDto>();
                if (keyframe != null)
                    break;
            }
        }

        Assert.NotNull(keyframe);
        Assert.Equal(board.Id, keyframe.BoardId);

        var info = await _client.GetFromJsonAsync<BoardInfoDto>($"/v1/boards/{board.Id}/info");
        Assert.NotNull(info);
        Assert.Equal("Running", info.Mode);

        var poll = await _client.GetFromJsonAsync<BoardKeyframesResponseDto>(
            $"/v1/boards/{board.Id}/keyframes?afterTick=0");
        Assert.NotNull(poll);
        Assert.NotEmpty(poll.Keyframes);

        await _client.PostAsync($"/v1/boards/{board.Id}/stop", null);
    }
}
