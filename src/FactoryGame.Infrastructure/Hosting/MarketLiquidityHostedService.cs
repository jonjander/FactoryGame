using FactoryGame.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FactoryGame.Infrastructure.Options;

namespace FactoryGame.Infrastructure.Hosting;

public sealed class MarketLiquidityHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<MarketLiquidityOptions> options,
    ILogger<MarketLiquidityHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled || !options.Value.BackgroundRefreshEnabled)
            return;

        await RefreshAsync(stoppingToken);

        var interval = TimeSpan.FromMinutes(Math.Max(1, options.Value.RefreshIntervalMinutes));
        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RefreshAsync(stoppingToken);
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var liquidity = scope.ServiceProvider.GetRequiredService<MarketLiquidityService>();
            await liquidity.EnsureLiquidityForAllPooledElementsAsync(ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Market liquidity refresh failed.");
        }
    }
}
