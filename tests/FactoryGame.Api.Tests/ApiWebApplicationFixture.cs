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
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:DefaultConnection", "");
            b.UseSetting("GameEconomy:SimulationTickIntervalSeconds", "600");
            b.UseSetting("Admin:BootstrapToken", "test-bootstrap");
        });
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await Factory.DisposeAsync();
}
