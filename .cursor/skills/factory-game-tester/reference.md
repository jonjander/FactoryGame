# FactoryGame — testreferens

## Testprojekt

| Projekt | Sökväg | Innehåll |
|---------|--------|----------|
| Domain | `tests/FactoryGame.Domain.Tests` | `BoardTickEngine`, DNA, analyzers |
| Api | `tests/FactoryGame.Api.Tests` | HTTP mot `Program`, SQLite in-memory |
| Web | `tests/FactoryGame.Web.Tests` | `ApiEndpointResolver` m.m. |

## API-fixtures

**Delad** — `ApiWebApplicationFixture`:

- Långt tick-intervall (600 s) — bra för marknad/pool, inte fabrik-sim
- `MarketLiquidity:RefreshOnSummaryRequest: true`

**Egen host** — `IAsyncLifetime` (t.ex. `SimpleGameFlowTests`, `BoardSimulationFlowTests`):

- Kort `SimulationTickIntervalSeconds` eller avregistrerad `SimulationTickHostedService`
- Unikt `FactoryGameTest_{guid}` DB-namn

## Manuella fabrik-tick (API-test)

```csharp
await using var scope = factory.Services.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
var runner = scope.ServiceProvider.GetRequiredService<BoardSimulationRunner>();
var board = await db.Boards.FirstAsync(b => b.Id == boardId);
var clock = await db.SimulationClock.FirstOrDefaultAsync(c => c.Id == 1)
    ?? /* skapa SimulationClockEntity Id=1 */;
for (var i = 0; i < n; i++)
{
    clock.CurrentTick++;
    await runner.TickBoardAsync(board, clock.CurrentTick, CancellationToken.None);
}
await db.SaveChangesAsync();
```

## Maskin → test

| Maskin | Domäntest | API-tips |
|--------|-------------|----------|
| Boiler | Vätske-DNA | `heatDelta` i settings |
| LiquidSeparator | `MeasureDnaSpreadPermille` ≥ 220 | `out1`+`out2` → seaport |
| Destilator | Gasfas | `cutBoiling` |
| SeaportConnector | `outElementId` | Pool per `ElementId` |

## Elementval

- Startpaket: element 1–5 (`StartingElementIds`)
- Köp utanför start: t.ex. 6–20 via börs
- Välj fas/DNA som maskinen accepterar (`MachineDnaCompatibility`)

## Vanliga assertion-fällor

| Symptom | Orsak |
|---------|--------|
| Köp `Open` | Limit under `BestAsk` / ingen likviditet |
| `keyframes/latest` 404 | Inga tick eller board ej Running |
| `withdrawn=0` i summa | Fel `outElementId`; kolla `LastSnapshotNote` |
| HTTP 500 på sälj | Otillräcklig pool; fabrik fortfarande aktiv |
| Hang ~100 s | Parallell tick + HTTP; isolera tick eller stäng hosted service |

## Referenstester i repo

- `SimpleGameFlowTests` — köp E07, LiquidSeparator-loop, säljordrar
- `BoardCyclePlanTests` — seaport ↔ boiler plan
- `BoardSimulationFlowTests` — start + keyframe poll
- `MarketSoloTradeTests` — solo-köp mot syntetisk likviditet
- `GuestFlowTests` — två gäster matchar
- `BoardTickEngineTests` — alla maskinprocessorer
