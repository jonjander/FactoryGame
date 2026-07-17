# FactoryGame -- test reference

## Test projects

| Project | Path | Content |
|---------|--------|----------|
| Domain | `tests/FactoryGame.Domain.Tests` | `BoardTickEngine`, DNA, analyzers |
| Api | `tests/FactoryGame.Api.Tests` | HTTP against `Program`, SQLite in-memory |
| Web | `tests/FactoryGame.Web.Tests` | `ApiEndpointResolver` etc. |

## API fixtures

**Shared** -- `ApiWebApplicationFixture`:

- Long tick interval (600 s) -- good for market/pool, not factory sim
- `MarketLiquidity:RefreshOnSummaryRequest: true`

**Own host** -- `IAsyncLifetime` (e.g. `SimpleGameFlowTests`, `BoardSimulationFlowTests`):

- Short `SimulationTickIntervalSeconds` or unregistered `SimulationTickHostedService`
- Unique `FactoryGameTest_{guid}` DB name

## Manual factory ticks (API test)

```csharp
await using var scope = factory.Services.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
var runner = scope.ServiceProvider.GetRequiredService<BoardSimulationRunner>();
var board = await db.Boards.FirstAsync(b => b.Id == boardId);
var clock = await db.SimulationClock.FirstOrDefaultAsync(c => c.Id == 1)
    ?? /* create SimulationClockEntity Id=1 */;
for (var i = 0; i < n; i++)
{
    clock.CurrentTick++;
    await runner.TickBoardAsync(board, clock.CurrentTick, CancellationToken.None);
}
await db.SaveChangesAsync();
```

## Machine -> test

| Machine | Domain test | API tip |
|--------|-------------|----------|
| Boiler | Liquid DNA | `heatDelta` in settings |
| LiquidSeparator | `MeasureDnaSpreadPermille` >= 220 | `out1`+`out2` -> seaport |
| Destilator | Gas phase | `cutBoiling` |
| SeaportConnector | `outElementId` | Pool per `ElementId` |

## Element choice

- Starter pack: elements 1-5 (`StartingElementIds`)
- Buy outside starter: e.g. 6-20 via exchange
- Choose phase/DNA the machine accepts (`MachineDnaCompatibility`)

## Common assertion pitfalls

| Symptom | Cause |
|---------|--------|
| Buy `Open` | Limit below `BestAsk` / no liquidity |
| `keyframes/latest` 404 | No ticks or board not Running |
| `withdrawn=0` in sum | Wrong `outElementId`; check `LastSnapshotNote` |
| HTTP 500 on sell | Insufficient pool; factory still active |
| Hang ~100 s | Parallel tick + HTTP; isolate tick or disable hosted service |

## Reference tests in repo

- `SimpleGameFlowTests` -- buy E07, LiquidSeparator loop, sell orders
- `BoardCyclePlanTests` -- seaport <-> boiler plan
- `BoardSimulationFlowTests` -- start + keyframe poll
- `MarketSoloTradeTests` -- solo buy against synthetic liquidity
- `GuestFlowTests` -- two guests match
- `BoardTickEngineTests` -- all machine processors
