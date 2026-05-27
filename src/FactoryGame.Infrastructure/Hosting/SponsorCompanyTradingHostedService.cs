using FactoryGame.Infrastructure.Options;
using FactoryGame.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactoryGame.Infrastructure.Hosting;

public sealed class SponsorCompanyTradingHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<SponsorCompanyOptions> options,
    ILogger<SponsorCompanyTradingHostedService> logger) : BackgroundService
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
            var trading = scope.ServiceProvider.GetRequiredService<SponsorCompanyTradingService>();
            await trading.RefreshAllActiveCompaniesAsync(ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Sponsor company trading refresh failed.");
        }
    }
}
