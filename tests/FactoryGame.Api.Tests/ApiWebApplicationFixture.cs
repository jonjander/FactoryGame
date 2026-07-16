using Microsoft.AspNetCore.Mvc.Testing;

namespace FactoryGame.Api.Tests;

/// <summary>
/// API host with SQLite in-memory, same as local default (Testing env skips appsettings.Local.json).
/// </summary>
public sealed class ApiWebApplicationFixture : IAsyncLifetime
{
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public Task InitializeAsync()
    {
        Factory = TestWebHostBuilderExtensions.CreateFactoryGameTestFactory();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await Factory.DisposeAsync();
}
