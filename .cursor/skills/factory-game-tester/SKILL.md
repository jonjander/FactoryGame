---
name: factory-game-tester
description: >-
  Writes and fixes FactoryGame xUnit tests (Domain, Api, Web). Chooses domain
  vs integration coverage, uses in-memory SQLite API host patterns, and debugs
  failing tests. Use when adding tests, fixing test regressions, or asking for
  test strategy for FactoryGame features.
disable-model-invocation: true
---

# FactoryGame — tester (xUnit)

Du sköter **repo-testerna** (`dotnet test`), inte MCP-playtest. För headless API via MCP: `@factory-game-mcp-playtest` / subagent `factory-game-playtester`.

## När ska tester skapas?

| Signal | Rekommendation |
|--------|----------------|
| Ny domänregel (DNA, tick, maskinprocessor) | `FactoryGame.Domain.Tests` — direkt mot `BoardTickEngine` |
| Nytt API-kontrakt eller HTTP-flöde | `FactoryGame.Api.Tests` — `WebApplicationFactory<Program>` |
| End-to-end spelflöde (köp → fabrik → sälj) | Ett fokuserat testklass (t.ex. `*FlowTests`) |
| Ren UI-URL/config | `FactoryGame.Web.Tests` |
| Trivial getter/setter | **Inget test** |

Skapa bara tester som fångar **beteende** eller **regression** — inte uppenbara sanningar.

## Snabbkommandon

```bash
dotnet test tests/FactoryGame.Domain.Tests
dotnet test tests/FactoryGame.Api.Tests
dotnet test tests/FactoryGame.Api.Tests --filter "FullyQualifiedName~SimpleGameFlow"
dotnet test
```

Kör alltid i **Cursor**; be inte repo-ägaren köra lokalt (se `factory-game-team`).

## Val av testnivå

**Domän** — snabbt, ingen HTTP:

```csharp
var plan = new SimulationPlan([new SimulationMachine("b1", "Boiler", """{"heatDelta":32}""")], []);
var state = BoardTickEngine.CreateInitialState(plan);
var result = BoardTickEngine.Advance(plan, state, 1, 1m, pool);
```

**API** — delad eller egen host:

- `IClassFixture<ApiWebApplicationFixture>` för snabba tester utan tick-loop
- `IAsyncLifetime` + egen `WebApplicationFactory` när tick-intervall eller hosted services måste styras (se `SimpleGameFlowTests`)

Detaljer: [reference.md](reference.md)

## API-test — obligatoriska inställningar

```csharp
b.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={dbName};Mode=Memory;Cache=Shared");
b.UseSetting("MarketLiquidity:BackgroundRefreshEnabled", "false");
b.UseSetting("Admin:BootstrapToken", "test-bootstrap");
```

Gäst + bearer:

```csharp
var auth = await client.PostAsJsonAsync("/v1/auth/guest", new GuestAuthRequest(deviceKey));
client.DefaultRequestHeaders.Add("Authorization", $"Bearer {body.SessionToken}");
```

## Fabrik-simulering i tester

- **Deterministiska tick:** stäng av `SimulationTickHostedService` och kör `BoardSimulationRunner.TickBoardAsync` manuellt, eller polla keyframes med kort delay (mönster i `BoardSimulationFlowTests`).
- **Maskininställningar:** `MachineDto` med `JsonElement` — simuleringen läser via `SimulationPlanMapper` (`JsonSerializer.Serialize`, inte `GetRawText()`).
- **Seaport:** `{"outElementId":N}` på `SeaportConnector`; verifiera att planen roundtripar via `GET .../plan`.
- **Två utgångar:** koppla `out1` och `out2` till samma `SeaportConnector.in` (LiquidSeparator, Destilator).

## Marknad i tester

- Fräscha likviditet: `MarketLiquidityService.EnsureLiquidityForElementAsync(elementId, force: true)` i scope, eller `GET /v1/market/summary` med `RefreshOnSummaryRequest`.
- Köp: limit ≥ `max(referencePrice + epsilon, depth.BestAsk)`.
- Sälj: kräver pool-lager; stoppa fabrik först om pool muteras under körning.

## Felsökning trasiga tester

1. Kör **smal filter**: `--filter "FullyQualifiedName~Klassnamn"`.
2. Läs **felrad** — API: statuskod vs assertion; domän: `BlockedReason` / `SummaryNote` (`withdrawn=`, `deposited=`).
3. **Flaky timing:** byt från `Task.Delay`-poll till manuella tick eller öka försök; undvik parallell DB-krock (unikt in-memory DB-namn per fixture).
4. **404 keyframes:** fabrik inte `Running`, eller inga tick än — kontrollera `POST .../start` och revision.
5. **Pool/seaport:** fel `outElementId` → uttag från element 1 trots köp av annat element.
6. Efter fix: kör hela testprojektet, inte bara ett test.

## Minimal checklista för nytt API-flödestest

```
- [ ] Unik guest deviceKey + auth header
- [ ] Marknadslikviditet för valt element
- [ ] Köp verifierat (Filled + pool)
- [ ] Plan sparad med rätt connections
- [ ] Start → tick/keyframes → Running
- [ ] Påståenden mot beteende, inte implementationdetaljer
- [ ] Städa (stop board) före säljordrar om det behövs
```

## Relaterat

- Krav: `KRAVSPEC.md`
- Sim: `@factory-game-server-sim`
- Börs: `@factory-game-bors-seaport`
- Exempel i repo: `tests/FactoryGame.Api.Tests/SimpleGameFlowTests.cs`, `BoardCyclePlanTests.cs`, `BoardSimulationFlowTests.cs`
- Mönster: [reference.md](reference.md)
