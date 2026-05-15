using FactoryGame.Domain.Boards;
using FactoryGame.Infrastructure.Data;
using FactoryGame.Infrastructure.Data.Entities;
using FactoryGame.Infrastructure.Options;
using FactoryGame.Infrastructure.Simulation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactoryGame.Infrastructure.Hosting;

public sealed class SimulationTickHostedService(
    IServiceProvider services,
    IOptions<GameEconomyOptions> economyOptions,
    ILogger<SimulationTickHostedService> logger) : BackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(Math.Max(1, economyOptions.Value.SimulationTickIntervalSeconds));
    private readonly int _maxCatchUpTicks = Math.Max(1, economyOptions.Value.SimulationMaxCatchUpTicks);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, stoppingToken);
                await TickOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Simulation tick failed.");
            }
        }
    }

    private async Task TickOnceAsync(CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var runner = scope.ServiceProvider.GetRequiredService<BoardSimulationRunner>();

        var clock = await db.SimulationClock.FirstOrDefaultAsync(c => c.Id == 1, ct);
        if (clock == null)
        {
            clock = new SimulationClockEntity { Id = 1, CurrentTick = 0, LastAdvancedAt = DateTimeOffset.UtcNow };
            db.SimulationClock.Add(clock);
            await db.SaveChangesAsync(ct);
        }

        var now = DateTimeOffset.UtcNow;
        var elapsed = now - clock.LastAdvancedAt;
        var ticks = (long)(elapsed.TotalSeconds / _interval.TotalSeconds);
        if (ticks <= 0)
            return;
        if (ticks > _maxCatchUpTicks)
            ticks = _maxCatchUpTicks;

        for (var i = 0; i < ticks; i++)
        {
            clock.CurrentTick++;
            var running = await db.Boards.Where(b => b.Mode == BoardMode.Running).ToListAsync(ct);
            foreach (var board in running)
            {
                try
                {
                    await runner.TickBoardAsync(board, clock.CurrentTick, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Board tick failed for {BoardId}", board.Id);
                }
            }
        }

        clock.LastAdvancedAt = now;
        await db.SaveChangesAsync(ct);
    }
}
