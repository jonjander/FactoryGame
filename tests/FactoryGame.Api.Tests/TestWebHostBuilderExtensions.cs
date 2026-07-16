using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FactoryGame.Api.Tests;

internal static class TestWebHostBuilderExtensions
{
    public static IWebHostBuilder ConfigureFactoryGameTestHost(
        this IWebHostBuilder builder,
        string sqliteInMemoryConnectionString,
        Action<IWebHostBuilder>? configure = null)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", sqliteInMemoryConnectionString);
        builder.UseSetting("GameEconomy:SimulationTickIntervalSeconds", "600");
        builder.UseSetting("MarketLiquidity:BackgroundRefreshEnabled", "false");
        builder.UseSetting("MarketLiquidity:RefreshOnSummaryRequest", "false");
        builder.UseSetting("MarketLiquidity:ElementRefreshCooldownMinutes", "0");
        builder.UseSetting("SponsorCompany:BackgroundRefreshEnabled", "false");
        builder.UseSetting("Admin:BootstrapToken", "test-bootstrap");
        configure?.Invoke(builder);
        return builder;
    }

    public static WebApplicationFactory<Program> CreateFactoryGameTestFactory(
        Action<IWebHostBuilder>? configure = null)
    {
        var dbName = "FactoryGameTest_" + Guid.NewGuid().ToString("N");
        return new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.ConfigureFactoryGameTestHost($"Data Source={dbName};Mode=Memory;Cache=Shared", configure));
    }
}
