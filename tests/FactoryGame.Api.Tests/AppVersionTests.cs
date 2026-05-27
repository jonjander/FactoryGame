using System.Net.Http.Json;
using FactoryGame.Contracts.App;

namespace FactoryGame.Api.Tests;

public sealed class AppVersionTests : IClassFixture<ApiWebApplicationFixture>
{
    private readonly ApiWebApplicationFixture _fixture;

    public AppVersionTests(ApiWebApplicationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task App_version_is_public_without_auth()
    {
        var client = _fixture.Factory.CreateClient();
        var response = await client.GetAsync("/v1/app/version");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<AppVersionDto>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.Version));
        Assert.Matches(@"^\d+\.\d+\.\d+", body.Version);
    }
}
