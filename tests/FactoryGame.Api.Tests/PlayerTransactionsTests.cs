using System.Net.Http.Json;
using FactoryGame.Contracts.Auth;
using FactoryGame.Contracts.Player;

namespace FactoryGame.Api.Tests;

public sealed class PlayerTransactionsTests : IClassFixture<ApiWebApplicationFixture>
{
    private readonly ApiWebApplicationFixture _fixture;

    public PlayerTransactionsTests(ApiWebApplicationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Transactions_returns_paginated_page()
    {
        var client = _fixture.Factory.CreateClient();
        var auth = await client.PostAsJsonAsync("/v1/auth/guest",
            new GuestAuthRequest("tx-page-" + Guid.NewGuid().ToString("N")));
        auth.EnsureSuccessStatusCode();
        var body = await auth.Content.ReadFromJsonAsync<GuestAuthResponse>();
        Assert.NotNull(body);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {body.SessionToken}");

        await client.GetAsync("/v1/me/wallet");

        var res = await client.GetFromJsonAsync<PlayerTransactionsPageDto>(
            "/v1/me/transactions?page=1&pageSize=10");
        Assert.NotNull(res);
        Assert.Equal(1, res.Page);
        Assert.Equal(10, res.PageSize);
        Assert.True(res.TotalCount >= 1);
        Assert.NotEmpty(res.Items);
    }
}
