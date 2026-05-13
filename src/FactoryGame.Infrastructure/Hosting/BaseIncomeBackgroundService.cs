using FactoryGame.Infrastructure.Data;
using FactoryGame.Infrastructure.Data.Entities;
using FactoryGame.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactoryGame.Infrastructure.Hosting;

public sealed class BaseIncomeBackgroundService(
    IServiceProvider services,
    IOptions<GameEconomyOptions> economyOptions,
    ILogger<BaseIncomeBackgroundService> logger) : BackgroundService
{
    private readonly GameEconomyOptions _economy = economyOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(1, _economy.BaseIncomeIntervalMinutes));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
                await ApplyBaseIncomeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Base income tick failed.");
            }
        }
    }

    private async Task ApplyBaseIncomeAsync(CancellationToken ct)
    {
        var amount = _economy.BaseIncomeAmount;
        if (amount <= 0)
            return;

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerIds = await db.Players.AsNoTracking().Select(p => p.Id).ToListAsync(ct);
        foreach (var playerId in playerIds)
        {
            var balance = await db.PlayerBalances.FirstOrDefaultAsync(b => b.PlayerId == playerId, ct);
            if (balance == null)
                continue;

            balance.Cash += amount;
            db.EconomyTransactions.Add(new EconomyTransactionEntity
            {
                Id = Guid.NewGuid(),
                PlayerId = playerId,
                Type = "BaseIncome",
                CashDelta = amount,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
