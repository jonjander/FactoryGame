using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace FactoryGame.Api.Tests;

public sealed class PostgresApiFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();
        await _container.StartAsync();

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:DefaultConnection", _container.GetConnectionString());
            b.UseSetting("GameEconomy:SimulationTickIntervalSeconds", "600");
            b.UseSetting("Admin:BootstrapToken", "test-bootstrap");
        });
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        if (_container != null)
            await _container.DisposeAsync();
    }
}
