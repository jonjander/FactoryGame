# Version history

Concise list of what each delivered version includes. Git commit for a release has **only** semver as message (same value as `Version` in `Directory.Build.props`); git tag is `v{Version}`.

## 0.3.21

- **Machines:** **Gas Mixer** (two gas inputs → blended gas out) and **Burner** (consumes burnable gas completely — no output).
- **Sim:** Condenser and Destilator wiki/input compatibility clarified for gas phase; Burner blocks inert or overly explosive gas.
- **Pool:** factory seaport flow indicators (↓ withdraw / ↑ deposit per element) with detail in the info modal.

## 0.3.20

- **Account:** total estimated value (cash + pool + machines), performance chart over time, and change % for 1D / 1W / 1M / 1Y / MAX.
- **Exchange:** order form layout fix (quantity vs submit).
- **Wiki:** element detail lists machines suitable as input (phase, spread, safety — not just liquid/solid); sticky sidebar layout; cleaner machine lore; action buttons without trailing arrows.
- **Pool:** info modal header shows variant name; DNA breakdown chart with per-band colors (shared with wiki).
- **Sim:** legacy SeaportIn/SeaportOut removed — SeaportConnector only; player-facing block messages without DNA jargon.

## 0.3.19

- **Pool info modal:** fix DNA hex display (`0x…` instead of literal Razor text); pass pool variant DNA; **Show in Exchange** opens `/exchange?elementId=&dna=&side=sell`.

## 0.3.18

- **Exchange:** variant badge only in holdings, trade list and ticker header — full name on hover (`title`), no duplicate `E03-161903` text beside badge.

## 0.3.17

- **Exchange:** fix Razor bug that showed literal `?? item.Symbol` next to variant badges in holdings and sell insights.

## 0.3.16

- **Hotfix:** EF migration `AddMarketPriceCandleDna` now registers correctly (Designer + `[Migration]` attribute) so Azure SQL gets the `Dna` column on `MarketPriceCandles` — fixes HTTP 500 on Exchange/history after 0.3.15.

## 0.3.15

- **Exchange:** holdings and trade list show full variant labels (`E03-161903 (Base-Variant)`) like Pool; price chart and history are per DNA variant.
- **Market:** candles, last price, 24h change, NPC liquidity and 15-min drift run per `(ElementId, Dna)` — `E03-161903` and `E03-323659` are separate markets with distinct prices and order books.

## 0.3.14

- **Pool:** each variant row shows full material label (`E02-123456 (BaseName-VariantName)`) instead of duplicate base symbol `E02`.

## 0.3.13

- **Market:** every 15 minutes prices drift slightly — rare materials (low global pool supply) nudge up, common ones nudge down; small jitter keeps the exchange feeling alive at low player activity.

## 0.3.12

- **UI:** material labels show unique variant code plus generated names, e.g. `E03-509866 (MetaFeroneine-VolNitonide)` instead of plain `E03` on board info, port hints, pool dropdown, and seaport flow.
- **Sim/info:** mixer/boiler/destillator preview uses DNA-aware labels; canvas port hints use compact variant code with full label in tooltips.

## 0.3.11

- **UI:** machine progress bars on factory canvas (overall + step, input readiness segments, live shimmer); poll every 2s while running.
- **UI:** place-from-inventory — auto-select stock, clear blocked hints, polling no longer locks Place button; disable when factory is running.
- **Sim/info:** melter processing slot in port flow; pool variant empty warning; runtime progress in board info API.

## 0.3.10

- **UI:** version badge moved to bottom-left with safe-area insets so it is not clipped on iPhone rounded corners (PWA).

## 0.3.9

- **Sim:** melter incremental heating; seaport clears block each tick; withdraw rollback when output full; depleted pool stacks kept as discovered markers.
- **API:** `POST /v1/boards/{id}/return-to-stock` — removing a machine returns it to inventory.
- **UI:** place dialog icon grid; pipe auto-select single port; preview report banner before start; seaport dropdown keeps depleted variants; wiki mobile detail sheet; seaport label clarifies withdraw-only.

## 0.3.8

- **Azure crash fix:** stop publishing `appsettings.Local.json` (localhost SQL) which overrode empty/Production connection and made migrate fail on App Service.
- **Deploy:** strip `appsettings.Local.json` from publish output as a safety net.

## 0.3.7

- **Azure Zip Deploy:** create zip entries with `/` paths (Windows `CreateFromDirectory` used `\`, which broke Linux Kudu rsync with EINVAL).
- **Deploy:** clear `/home/site/wwwroot` before Zip Deploy so leftover backslash filenames cannot block sync.

## 0.3.6

- **Azure SQL:** production uses Azure SQL (`rqii` / `fg`) via managed identity (`Authentication=Active Directory Default`); deploy injects connection from `.local/azure-sql-connection.txt` into `appsettings.Production.json`.
- **Azure startup:** stop publishing `FactoryGame.Web.runtimeconfig.json` so Linux/Oryx starts `FactoryGame.Api` instead of `hostingstart`.
- **Deploy:** longer health smoke for cold SQL migrate; optional Kudu connection-string post.

## 0.3.5

- **Factory canvas:** status badges (blocked/waiting/processing) with icons, colors, and short guidance; legend while running.
- **Pipe persistence:** auto-save connections; save plan before place-from-stock so pipes survive new machines.
- **Canvas refresh:** re-render after machine placement without extra clicks.
- **Economy:** passive base income removed (service, config, wallet UI).
- **UI:** version in fixed bottom-right footer; fix Razor version display.

## 0.3.4

- **English-only:** all user-facing UI, domain diagnostics, wiki text, and API defaults use English (ASCII); fixes garbled Swedish characters in the browser.
- **Locale:** default `locale=en` across web client, API fallbacks, MCP tools, and playtest scripts.
- **Names:** element name generator uses English morphemes only.
- **Docs:** KRAVSPEC, README, AGENTS, Cursor rules/skills, and backlog translated to English.

## 0.3.3

- **Playtest fixes:** `minimalLoop` uses E03 (liquid) + Boiler; playtest asserts production without errors.
- **Factory analysis:** throughput follows flow lines when latest tick lacks seaport delta.
- **Exchange:** summary always seeds pool liquidity; hint when sell ask missing; onboarding for phase requirements.
- **Economy:** wallet shows base income countdown; StarterPool metadata without duplicates.
- **Account:** English types for StarterPack/StartingCash; paginated transactions API tested.
- **Factory (Game Shell):** board list prefetches issues for warning/error so muted hints work in the list.

## 0.3.2

- **Admin:** `GET /v1/admin/players` sorts in database via indexed `CreatedAtUtcTicks` (no in-memory sort).
- **Exchange:** `ExchangeService` uses `CreateExecutionStrategy()` so market orders work with SQL Server retry.
- **Local dev:** `appsettings.Local.json` loaded optionally at startup.

## 0.3.1

- **UI -- Account:** new page `/transactions` with economy history; nav link in main menu.
- **UI -- Exchange:** onboarding panel (core loop) that can be dismissed permanently.
- **UI -- Home:** device key saved in browser between visits.
- **Economy:** machine purchase includes `MachinePlacementCost`; removed double charge on factory start.
- **Balance:** `BaseIncomeAmount` 15, interval 3 min.
- **Factory analysis:** clearer seaport warnings (missing connector, missing outElementId).
- **Diagnostics:** `/diagnostics/recent-logs` always registered (403 in Production without setting).

## 0.3.0

- **Database -- SQL Server:** dual-provider (SQL Server locally/Azure, SQLite in-memory for tests/Docker).
- **EF Core migrations:** `InitialSqlServer` -- schema migrates at startup (`Migrate()`) against SQL Server.
- **Configuration:** `appsettings.Local.example.json` for local SQL Server; Azure SQL with `Authentication=Active Directory Default` (managed identity).
- **P2 backlog:** SQLite lock on long-running API addressed via server DB.

## 0.2.23

- **Guest login:** `/v1/market/insights` requires session in middleware -- fixes logout right after login on exchange.

## 0.2.22

- **Simulation -- rate engine:** per-machine throughput, port ratio, and effective rate (permille); transfer limited per pipe connection.
- **New machines:** Tank (buffer small/medium/large), Junction (1->2 split), RateLimiter (max flow).
- **Operating speed:** `operationRatePermille` 50/80/100 % on process machines.
- **Time DNA:** heat/cool/condense over multiple ticks; fast operation risks overshoot.
- **Keyframe state:** `BoardLineState` RuleVersion 2 (tank/junction/processing-slot).

## 0.2.21

- **Board plans -- list:** cards/tiles with status color (running, warning, error/stopped), machine/pipe counts and short status line.
- **Board plans -- name:** rename saved plan (`PATCH /v1/boards/{id}`) directly in factory view.
- **Seaport -- pool:** warning in machine settings and factory analysis when selected out element missing from pool.

## 0.2.20

- **Sponsored companies (ads):** admin CRUD (`/admin`, `/v1/admin/companies`), multiple standing orders per company, budget or utopia mode.
- **Exchange:** block sponsor<->sponsor, counterparty name on fills, sell opportunities panel (`/v1/market/insights`).
- **Market:** analysis page `/market` with leaderboards players/companies (`/v1/market/leaderboards`), company profile `/v1/market/sponsors/{id}`.
- **Tests:** `SponsorCompanyMarketTests` (player<->sponsor, anti-sponsor match, utopia spend).

## 0.2.19

- **Pool -- DNA variants:** one stack per unique DNA `(ElementId, Dna)`; grouped pool view with phase (gas/liquid/solid).
- **Seaport OUT:** choose pool variant (`outMaterialDna`) -- e.g. E04 gas vs E04 liquid.
- **Exchange:** separate order books per variant; `PlaceOrderRequest.Dna`, depth with `?dna=`.
- **Factory UX:** phase badges and process status on transform (Condenser E04 gas->liquid); tooltips in canvas and BoardInfoPanel.
- **Tests:** `PoolDnaStackTests`, extended `MachinePortFlowAnalyzerTests`; API tests updated for DNA orders.

## 0.2.18

- **Factory -- UX:** seaport OUT default "(nothing)"; pool filter on OUT; clearer start/stop and locked settings while running.
- **Factory -- canvas:** drag-and-drop pipes, remove machine/pipe, run status (flow/block), tooltips on pipes and machines.
- **Factory -- guidance:** actionable blocking messages; DNA warnings on unsuitable element; info dialog with icon in machine store.

## 0.2.17

- **Dev-lead loop:** `npm run iter3:local` (liquid separator -> Running); `iter2:local` more stable (fetch timeout, fewer API rounds after P2P).
- **MCP scripts:** `dev-lead-iter3-separator.mjs`; iter2 reuses exchange depth and verifies Running + seaport out.

## 0.2.16

- **Exchange -- bootstrap:** all catalog elements (e.g. E07) tradable via `GET .../depth` without any player already having them in pool (synthetic liquidity).
- **Dev:** `appsettings.Development.json` -- faster sim (1 s), `BaseIncome` every 1 min, exchange updates on `market/summary`, no liquidity cooldown.
- **Tests:** `MarketTradeableBootstrapTests`, `TwoPlayerMarketTests` (P2P matching).
- **MCP:** `npm run iter2:local`, `dev-lead-iter2-two-player.mjs`; fixture `liquidSeparatorFlow` uses element 7.

## 0.2.15

- **API -- SQLite:** `GET /v1/market/orders/mine`, `GET /v1/me/transactions`, and admin player list sort `DateTimeOffset` in memory (fixes 500 on SQLite).
- **Test:** `MarketMyOrdersTests`; `npm run playtest:local` includes `market_orders_mine` without errors.
- **Dev-lead:** subagent `factory-game-dev-lead`, skill `factory-game-dev-lead`, `docs/dev-lead-backlog.md`, script `dev-lead-economy-iter.mjs`; `AGENTS.md` updated.

## 0.2.14

- **MCP -- local development:** second MCP server `factorygame-local` in `.cursor/mcp.json` (`http://localhost:5176`); `npm run smoke:local` and `npm run playtest:local` with health wait.

## 0.2.13

- **MCP -- GUI parity:** 11 new tools (machine store, pool/view, machine inventory, exchange summary/depth/history/mine, plan read/preview, place-from-stock).
- **MCP -- playtest:** `npm run playtest` (E2E against Azure), extended `npm run smoke`, fixtures `tools/factorygame-mcp/fixtures/plans.json`.
- **Skills/agents:** updated `factory-game-mcp-server`, `factory-game-playtester`.

## 0.2.12

- **Factory -- simulation:** Destilator, Liquid separator, Condenser, Crystallizer, Melter; extended Mixer (poor/unstable mix).
- **Factory -- settings:** shared panel with dropdowns per machine (heat, cut, mix intensity, sorter ports, seaport elements); canvas click selects machine.

## 0.2.11

- **Factory -- seaport out:** choose base element from pool per connector (`outElementId`) in dedicated panel; label `E01 ->` at out port in canvas.
- **Factory -- machine results:** factory info and canvas show in->out per output (e.g. `E01->E02`, "heated in Boiler"); API `machinePortFlows` on board info.

## 0.2.10

- **Exchange -- fix 500:** liquidity refresh crashed on empty player order book (`Max()`/`Min()`); bulk cancel synthetic orders; depth returned even if liquidity failed.
- **Client:** clearer errors on HTTP 500 (not "cannot connect"); orders attempted even if depth call fails.

## 0.2.9

- **Client -- API URL:** `ApiTarget` (Auto / LocalDev / Azure / ...), `ApiEndpointResolver`, VS profiles "UI -> local API" and "UI -> Azure API" (`FactoryGameApiTarget`).
- **Client:** `factory-config.json` with Azure and local base URL; Debug default `LocalDev` in `appsettings.Development.json`.
- **API:** CORS in Development allows all localhost ports (Blazor dev server + API).

## 0.2.8

- **Pool:** inventory view with name, quantity, market and row value, global exchange rank (price), total estimated value and volume -- replaces JSON dump.
- **Deep analysis:** Info modal with DNA composition, property text, and wiki link.
- **API:** `GET /v1/me/pool/view` (composite pool + prices + rank).
- **Wiki:** element card at top on `?elementId=` from pool link.

## 0.2.7

- **Exchange -- fills:** "Recent trades" shows player trades (synthetic seed hidden); row "Just now" after filled order; more stable trade list reload.
- **Exchange -- holdings:** table "Your holdings (pool)" with quantity per base element; clear "You own X" for selected element.
- **Exchange -- orders:** preview of direct match; quick buttons buy at ask / ask+0.01 / sell at bid; list "Your open orders"; clear feedback on filled/partial/open order (incl. why no match).
- **API:** `GET /v1/market/orders/mine`; `PlaceOrder` response with `quantityFilled` and `averageFillPrice`. Client fetches depth before order so counterparty exists for match.

## 0.2.6

- **Factory -- pipes:** remove pipe in canvas (click + toolbar) and in list under "Connect pipes"; curved pipes with lane offset and colors.
- **Factory -- connection:** all ports selectable; confirm dialog when changing existing connection (canvas + form).
- **Factory -- seaport in:** clear view in factory info (element, source, summary) and label `<- E01` at port in canvas; upstream/runtime analysis in API (`seaportPorts` on board info).
- **Performance / exchange:** synthetic liquidity not rebuilt on every `GET /market/summary` (15 min cooldown); background job limited. Exchange UI polls every 60 s. Factory tick default 2 s.

## 0.2.5

- **Performance / exchange:** synthetic liquidity not rebuilt on every `GET /market/summary` (15 min cooldown per element); background job limited (max 25 elements/tick, every 15 min). Depth (`/depth`) refreshes on demand. Exchange UI polls every 60 s (was 12 s). Factory tick default 2 s (was 1 s) in appsettings.

## 0.2.4

- **Login:** guest `POST /v1/auth/guest` responds quickly again (starter pack moved to `GET /v1/me/wallet` / pool / exchange instead of blocking login).
- **Client:** clear status "Logging in..." / "Contacting server..."; better error messages; no Bearer header on guest login.
- **Legacy players:** missing inventory pool created on login.

## 0.2.3

- **Exchange / orders:** clear feedback on "Submit order" -- loading text, colored status (filled/open/error), snackbar, wallet in header, validation (pool/quantity), estimated buy cost; API errors shown in English instead of hidden JSON.

## 0.2.2

- **Starter material in pool:** new players (and existing without prior starter pack) get **5 base elements** (id 1-5), **25 each**, on guest login or first exchange visit.
- Configuration: `GameEconomy:StartingElementIds` and `StartingElementQuantityPerStack` in appsettings.

## 0.2.1

- **Loops allowed:** save/start plan with e.g. seaport -> boiler -> seaport (cycle no longer blocked on `PUT .../plan`).
- **Simulation:** more transfer passes per tick + stable machine order on loop.
- **Factory information:** analyzes plan in JSON editor (preview) in Edit mode; shows machine/pipe counts and "loop"; MCP/API `GET .../info` incl. `planConnectionCount` / `planHasCycle`.

## 0.2.0

- **Factory simulation:** deterministic tick engine (`BoardTickEngine`) with material packets, port buffers, and machine processors (Boiler, Heater, Cooler, Mixer, Sorter, SeaportConnector); DNA blocking during run.
- **Seaport pool in tick:** `SeaportPoolGateway` -- connector pulls/deposits elements in player pool at `Running`.
- **Keyframes:** persistence + `GET /v1/boards/{id}/keyframes/latest` and `.../keyframes?afterTick=`; `SimulationTickHostedService` runs real simulation.
- **Factory information v2:** `GET/POST .../info` with runtime from keyframe, pool and spot price; cycle validation on save/start.
- **Web client Factory:** visual **SVG canvas** (drag placement, port connection), factory info panel, auto-refresh while running; header with **wallet**, snackbar, **SeaportConnector** in store (replaces in/out buttons), auto machine id.
- **Exchange:** synthetic order depth and seeded price history; API `GET /v1/market/summary`, `.../depth`, `.../history`; page `/exchange`.
- **MCP:** tools `boards_info`, `boards_keyframe_latest`, `boards_keyframes` for Azure smoke.

## 0.1.9

- Web client: **Log out** in top bar; invalid saved session cleared automatically on **401** and at Home start (avoids "Logged in" with broken token after deploy).
- Clearer error text on **401** (no longer incorrectly "Cannot connect to API").

## 0.1.8

- Web client **Factory** (`/boards`): machine store with prices (e.g. **Liquid boiler** / `Boiler` for 4000), **machine inventory** after purchase, **placement on plan** via API, **GET plan** from server when selecting plan, pipe UI with **dropdowns** for machines and **free** in/out ports, buttons for **Seaport in/out** connections.
- API: `GET /v1/content/machine-store`, `GET/POST /v1/me/machine-inventory`, `GET /v1/boards/{id}/plan`, `POST /v1/boards/{id}/place-from-stock`; domain: `MachinePortCatalog`, `MachineStoreCatalog`, wiki entries `SeaportIn` / `SeaportOut`.
- Economy: raised **StartingCash** (appsettings + default) so new players can afford machine store; API tests use **own** in-memory SQLite per fixture (avoids collision between parallel test classes).

## 0.1.7

- Web client: **"Fetch latest app"** button in footer -- unregisters service worker, clears Cache API and reloads (fresh WASM after deploy / when cache stuck).

## 0.1.6

- Web client: **always normalizes** `HttpClient.BaseAddress` with trailing `/` (otherwise relative calls like `/v1/auth/guest` to Azure can hit wrong host/URL and `TypeError: Load failed`). Extra guard if API base remains on loopback when PWA is not.
- `index.html`: service worker registered with `updateViaCache: 'none'` so newer `service-worker.js` is fetched more often after deploy (reduces risk of old cached WASM).
- Guest login: on connection error shows **HttpClient.BaseAddress** in error text for easier Azure troubleshooting.

## 0.1.5

- API: `UseSwagger` / `UseSwaggerUI` run **before** Blazor static files (previously `UseWhen` after them). Avoids `MapFallbackToFile("index.html")` catching `GET /swagger` so Blazor shows "nothing at this address".

## 0.1.4

- Web client: if `ApiBaseUrl` in config points to **localhost** but PWA loads from **real host** (e.g. Azure), localhost URL is ignored and same origin as page is used -- avoids `TypeError: Load failed` when App Service has `ASPNETCORE_ENVIRONMENT=Development` and WASM read dev URL to `localhost`.
- `wwwroot/appsettings.Development.json` no longer contains `ApiBaseUrl` (local dev API handled by port heuristic in `Program.cs`).

## 0.1.3

- API: `GET /diagnostics/recent-logs` -- unformatted text with log lines since process start (no authentication). Enabled in **Development** always; in other environments set `Diagnostics:ExposeRecentLogEndpoint` to `true` if needed for e.g. Azure troubleshooting (leave off in production).
- Web client: clearer API base on local Blazor dev server (`WasmApplicationEnvironmentName`, fallback to `https://localhost:7145` for known dev ports) and better error text on connection failure.
- Infrastructure: EF Core migration files removed; SQLite schema created with `EnsureCreatedAsync()` (existing database strategy in codebase).
- Other: updates in Docker/README/KRAVSPEC and API tests (SQLite fixture instead of Postgres fixture).

## 0.1.2

- API: Swagger/Swashbuckle runs only for paths under `/swagger`, so root URL (`/`) goes to Blazor PWA and fallback to `index.html` without Swagger middleware in the way.

## 0.1.1

- Version flow: commit message on release is semver only; release notes moved to this file (`releases.md`).

## 0.1.0

- Central `Version` in `Directory.Build.props` for all projects; version line in web client footer and in Swagger.
- Cursor rules for commit/push, version/tags, and delivery message in chat.
