using Microsoft.AspNetCore.Mvc.Testing;

namespace FactoryGame.Api.Tests;

/// <summary>
/// API host with SQLite in-memory (empty DefaultConnection), same as local default.
/// </summary>
public sealed class ApiWebApplicationFixture : IAsyncLifetime
{
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public Task InitializeAsync()
    {
        var dbName = "FactoryGameTest_" + Guid.NewGuid().ToString("N");
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={dbName};Mode=Memory;Cache=Shared");
            b.UseSetting("GameEconomy:SimulationTickIntervalSeconds", "600");
            b.UseSetting("MarketLiquidity:BackgroundRefreshEnabled", "false");
            b.UseSetting("Admin:BootstrapToken", "test-bootstrap");
        });
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await Factory.DisposeAsync();
}
