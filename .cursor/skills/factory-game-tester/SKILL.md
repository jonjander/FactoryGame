---
name: factory-game-tester
description: >-
  Writes and fixes FactoryGame xUnit tests (Domain, Api, Web). Chooses domain
  vs integration coverage, uses in-memory SQLite API host patterns, and debugs
  failing tests. Use when adding tests, fixing test regressions, or asking for
  test strategy for FactoryGame features.
disable-model-invocation: true
---

# FactoryGame -- tests (xUnit)

You handle **repo tests** (`dotnet test`), not MCP playtest. For headless API via MCP: `@factory-game-mcp-playtest` / subagent `factory-game-playtester`.

## When should tests be created?

| Signal | Recommendation |
|--------|----------------|
| New domain rule (DNA, tick, machine processor) | `FactoryGame.Domain.Tests` -- directly against `BoardTickEngine` |
| New API contract or HTTP flow | `FactoryGame.Api.Tests` -- `WebApplicationFactory<Program>` |
| End-to-end game flow (buy -> factory -> sell) | One focused test class (e.g. `*FlowTests`) |
| Pure UI URL/config | `FactoryGame.Web.Tests` |
| Trivial getter/setter | **No test** |

Create only tests that catch **behavior** or **regression** -- not obvious truths.

## Quick commands

```bash
dotnet test tests/FactoryGame.Domain.Tests
dotnet test tests/FactoryGame.Api.Tests
dotnet test tests/FactoryGame.Api.Tests --filter "FullyQualifiedName~SimpleGameFlow"
dotnet test
```

Always run in **Cursor**; do not ask the repo owner to run locally (see `factory-game-team`).

## Test level choice

**Domain** -- fast, no HTTP:

```csharp
var plan = new SimulationPlan([new SimulationMachine("b1", "Boiler", """{"heatDelta":32}""")], []);
var state = BoardTickEngine.CreateInitialState(plan);
var result = BoardTickEngine.Advance(plan, state, 1, 1m, pool);
```

**API** -- shared or own host:

- `IClassFixture<ApiWebApplicationFixture>` for fast tests without tick loop
- `IAsyncLifetime` + own `WebApplicationFactory` when tick interval or hosted services must be controlled (see `SimpleGameFlowTests`)

Details: [reference.md](reference.md)

## API test -- required settings

```csharp
b.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={dbName};Mode=Memory;Cache=Shared");
b.UseSetting("MarketLiquidity:BackgroundRefreshEnabled", "false");
b.UseSetting("Admin:BootstrapToken", "test-bootstrap");
```

Guest + bearer:

```csharp
var auth = await client.PostAsJsonAsync("/v1/auth/guest", new GuestAuthRequest(deviceKey));
client.DefaultRequestHeaders.Add("Authorization", $"Bearer {body.SessionToken}");
```

## Factory simulation in tests

- **Deterministic ticks:** disable `SimulationTickHostedService` and run `BoardSimulationRunner.TickBoardAsync` manually, or poll keyframes with short delay (pattern in `BoardSimulationFlowTests`).
- **Machine settings:** `MachineDto` with `JsonElement` -- simulation reads via `SimulationPlanMapper` (`JsonSerializer.Serialize`, not `GetRawText()`).
- **Seaport:** `{"outElementId":N}` on `SeaportConnector`; verify plan roundtrips via `GET .../plan`.
- **Two outputs:** connect `out1` and `out2` to same `SeaportConnector.in` (LiquidSeparator, Destilator).

## Market in tests

- Fresh liquidity: `MarketLiquidityService.EnsureLiquidityForElementAsync(elementId, force: true)` in scope, or `GET /v1/market/summary` with `RefreshOnSummaryRequest`.
- Buy: limit >= `max(referencePrice + epsilon, depth.BestAsk)`.
- Sell: requires pool inventory; stop factory first if pool mutates during run.

## Debugging broken tests

1. Run **narrow filter**: `--filter "FullyQualifiedName~ClassName"`.
2. Read **error line** -- API: status code vs assertion; domain: `BlockedReason` / `SummaryNote` (`withdrawn=`, `deposited=`).
3. **Flaky timing:** switch from `Task.Delay` poll to manual ticks or increase attempts; avoid parallel DB collision (unique in-memory DB name per fixture).
4. **404 keyframes:** factory not `Running`, or no ticks yet -- check `POST .../start` and revision.
5. **Pool/seaport:** wrong `outElementId` -> withdrawal from element 1 despite buying another element.
6. After fix: run whole test project, not just one test.

## Minimal checklist for new API flow test

```
- [ ] Unique guest deviceKey + auth header
- [ ] Market liquidity for chosen element
- [ ] Buy verified (Filled + pool)
- [ ] Plan saved with correct connections
- [ ] Start -> tick/keyframes -> Running
- [ ] Assertions against behavior, not implementation details
- [ ] Cleanup (stop board) before sell orders if needed
```

## Related

- Requirements: `KRAVSPEC.md`
- Sim: `@factory-game-server-sim`
- Exchange: `@factory-game-bors-seaport`
- Examples in repo: `tests/FactoryGame.Api.Tests/SimpleGameFlowTests.cs`, `BoardCyclePlanTests.cs`, `BoardSimulationFlowTests.cs`
- Patterns: [reference.md](reference.md)
