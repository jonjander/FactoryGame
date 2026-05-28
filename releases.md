# Versionshistorik

Kortfattad lista över vad som ingår i varje levererad version. Git-commit för en release har **endast** semver som meddelande (samma värde som `Version` i `Directory.Build.props`); git-tag är `v{Version}`.

## 0.3.1

- **UI — Konto:** ny sida `/transactions` med ekonomihistorik; nav-länk i huvudmenyn.
- **UI — Börs:** onboarding-panel (kärnloopen) som kan stängas permanent.
- **UI — Home:** enhetsnyckel sparas i webbläsaren mellan besök.
- **Ekonomi:** maskinköp inkluderar `MachinePlacementCost`; borttagen dubbel debitering vid fabrikstart.
- **Balans:** `BaseIncomeAmount` 15, intervall 3 min.
- **Fabrikanalys:** tydligare seaport-varningar (saknad connector, saknat outElementId).
- **Diagnostik:** `/diagnostics/recent-logs` registreras alltid (403 i Production utan inställning).

## 0.3.0

- **Databas — SQL Server:** dual-provider (SQL Server lokalt/Azure, SQLite in-memory för tester/Docker).
- **EF Core migrations:** `InitialSqlServer` — schema migreras vid startup (`Migrate()`) mot SQL Server.
- **Konfiguration:** `appsettings.Local.example.json` för lokal SQL Server; Azure SQL med `Authentication=Active Directory Default` (managed identity).
- **P2 backlog:** SQLite-lås vid långkörande API adresserat via server-DB.

## 0.2.23

- **Gästinloggning:** `/v1/market/insights` kräver session i middleware — fixar utloggning direkt efter inloggning på börsen.

## 0.2.22

- **Simulering — rate-motor:** per-maskin genomströmning, port-ratio och effektiv rate (permille); transfer begränsas per rörkoppling.
- **Nya maskiner:** Tank (buffert small/medium/large), Junction (1→2 fördelning), RateLimiter (max flöde).
- **Drifthastighet:** `operationRatePermille` 50/80/100 % på processmaskiner.
- **Tids-DNA:** värme/kyl/kondens över flera tick; snabb drift riskerar overshoot.
- **Keyframe-state:** `BoardLineState` RuleVersion 2 (tank/junction/processing-slot).

## 0.2.21

- **Spelplaner — lista:** kort/tiles med statusfärg (kör, varning, fel/stoppad), maskin-/rörantal och kort statusrad.
- **Spelplaner — namn:** byt namn på sparad plan (`PATCH /v1/boards/{id}`) direkt i fabrik-vyn.
- **Seaport — pool:** varning i maskininställningar och fabrikanalys när valt ut-element saknas i poolen.

## 0.2.20

- **Sponsrade företag (reklam):** admin-CRUD (`/admin`, `/v1/admin/companies`), flera stående order per företag, budget- eller utopi-läge.
- **Börs:** blockering sponsor↔sponsor, motpartsnamn på avslut, säljmöjligheter-panel (`/v1/market/insights`).
- **Marknad:** analys-sida `/market` med topplistor spelare/företag (`/v1/market/leaderboards`), företagsprofil `/v1/market/sponsors/{id}`.
- **Tester:** `SponsorCompanyMarketTests` (spelare↔sponsor, anti-sponsor-match, utopi spend).

## 0.2.19

- **Pool — DNA-varianter:** en stack per unikt DNA `(ElementId, Dna)`; grupperad pool-vy med fas (gasform/flytande/fast).
- **Seaport OUT:** välj pool-variant (`outMaterialDna`) — t.ex. E04 gasform vs E04 flytande.
- **Börs:** separata orderböcker per variant; `PlaceOrderRequest.Dna`, depth med `?dna=`.
- **Fabrik UX:** fas-badges och processstatus vid transform (Condenser E04 gas→vätska); tooltips i canvas och BoardInfoPanel.
- **Tester:** `PoolDnaStackTests`, utökade `MachinePortFlowAnalyzerTests`; API-tester uppdaterade för DNA-ordrar.

## 0.2.18

- **Fabrik — UX:** seaport OUT default «(ingenting)»; pool-filter på OUT; tydligare start/stop och låsta inställningar vid körning.
- **Fabrik — canvas:** drag-and-drop rör, ta bort maskin/rör, körstatus (flöde/blockering), tooltips på rör och maskiner.
- **Fabrik — vägledning:** actionable blockeringsmeddelanden; DNA-varningar vid olämpligt ämne; info-dialog med ikon i maskinbutiken.

## 0.2.17

- **Dev-lead loop:** `npm run iter3:local` (liquid separator → Running); `iter2:local` stabilare (fetch-timeout, färre API-rundor efter P2P).
- **MCP-skript:** `dev-lead-iter3-separator.mjs`; iter2 återanvänder börsdjup och verifierar Running + seaport out.

## 0.2.16

- **Börs — bootstrap:** alla katalog-element (t.ex. E07) kan handlas via `GET .../depth` utan att någon spelare redan har dem i pool (syntetisk likviditet).
- **Dev:** `appsettings.Development.json` — snabbare sim (1 s), `BaseIncome` var 1 min, börs uppdateras vid `market/summary`, ingen likviditets-cooldown.
- **Tester:** `MarketTradeableBootstrapTests`, `TwoPlayerMarketTests` (P2P-matchning).
- **MCP:** `npm run iter2:local`, `dev-lead-iter2-two-player.mjs`; fixture `liquidSeparatorFlow` använder element 7.

## 0.2.15

- **API — SQLite:** `GET /v1/market/orders/mine`, `GET /v1/me/transactions` och admin-spelarlista sorterar `DateTimeOffset` i minnet (fixar 500 på SQLite).
- **Test:** `MarketMyOrdersTests`; `npm run playtest:local` inkluderar `market_orders_mine` utan fel.
- **Dev-lead:** subagent `factory-game-dev-lead`, skill `factory-game-dev-lead`, `docs/dev-lead-backlog.md`, skript `dev-lead-economy-iter.mjs`; `AGENTS.md` uppdaterad.

## 0.2.14

- **MCP — lokal utveckling:** andra MCP-server `factorygame-local` i `.cursor/mcp.json` (`http://localhost:5176`); `npm run smoke:local` och `npm run playtest:local` med health-wait.

## 0.2.13

- **MCP — GUI-paritet:** 11 nya verktyg (maskinbutik, pool/view, maskinlager, börs-summary/djup/historik/mine, plan läs/preview, place-from-stock).
- **MCP — playtest:** `npm run playtest` (E2E mot Azure), utökad `npm run smoke`, fixtures `tools/factorygame-mcp/fixtures/plans.json`.
- **Skills/agenter:** uppdaterad `factory-game-mcp-server`, `factory-game-playtester`.

## 0.2.12

- **Fabrik — simulering:** Destilator, Liquid separator, Condenser, Crystallizer, Melter; utökad Mixer (fattig/ostabil blandning).
- **Fabrik — inställningar:** gemensam panel med dropdowns per maskin (värme, cut, mix-intensitet, sorter-portar, seaport-element); klick i canvas markerar maskin.

## 0.2.11

- **Fabrik — seaport ut:** välj grundämne från pool per connector (`outElementId`) i egen panel; etikett `E01 →` vid out-port i canvas.
- **Fabrik — maskinresultat:** fabrikinfo och canvas visar in→ut per utgång (t.ex. `E01→E02`, «värms i Boiler»); API `machinePortFlows` på board info.

## 0.2.10

- **Börs — fix 500:** likviditetsuppdatering kraschade på tom spelar-orderbok (`Max()`/`Min()`); bulk-avbryt syntetiska ordrar; djup returneras även om likviditet misslyckas.
- **Klient:** tydligare fel vid HTTP 500 (inte «kan inte ansluta»); order försöker även om djup-anropet faller.

## 0.2.9

- **Klient — API-URL:** `ApiTarget` (Auto / LocalDev / Azure / …), `ApiEndpointResolver`, VS-profiler «UI → lokal API» och «UI → Azure API» (`FactoryGameApiTarget`).
- **Klient:** `factory-config.json` med Azure- och lokal bas-URL; Debug-default `LocalDev` i `appsettings.Development.json`.
- **API:** CORS i Development tillåter alla localhost-portar (Blazor dev-server + API).

## 0.2.8

- **Pool:** inventarievy med namn, antal, marknads- och radvärde, global börs-rank (pris), totalt uppskattat värde och volym — ersätter JSON-dump.
- **Djupanalys:** Info-modal med DNA-uppbyggnad, egenskapstext och wiki-länk.
- **API:** `GET /v1/me/pool/view` (sammansatt pool + priser + rank).
- **Wiki:** elementkort överst vid `?elementId=` från pool-länken.

## 0.2.7

- **Börs — avslut:** «Senaste avslut» visar spelarhandel (syntetisk seed döljs); rad «Nyss» efter fylld order; stabilare omladdning av trade-lista.
- **Börs — innehav:** tabell «Ditt innehav (pool)» med antal per grundämne; tydlig «Du äger X st» vid valt ämne.
- **Börs — order:** förhandsvisning om direktmatchning; snabbknappar köp till ask / ask+0,01 / sälj till bid; lista «Dina öppna order»; tydlig feedback vid fylld/delvis/öppen order (inkl. varför ingen match).
- **API:** `GET /v1/market/orders/mine`; `PlaceOrder`-svar med `quantityFilled` och `averageFillPrice`. Klienten hämtar djup före order så motpart finns vid matchning.

## 0.2.6

- **Fabrik — rör:** ta bort rör i canvas (klick + verktygsrad) och i listan under «Koppla rör»; slingrör med lane-offset och färger.
- **Fabrik — koppling:** alla portar kan väljas; bekräftelsedialog vid byte av befintlig koppling (canvas + formulär).
- **Fabrik — seaport in:** tydlig vy i fabrikinfo (element, källa, sammanfattning) och etikett `← E01` vid porten i canvas; upströms-/runtime-analys i API (`seaportPorts` på board info).
- **Prestanda / börs:** syntetisk likviditet byggs inte om vid varje `GET /market/summary` (15 min cooldown); bakgrundsjobb begränsat. Börs-UI pollar var 60:e s. Fabrik-tick standard 2 s.

## 0.2.5

- **Prestanda / börs:** syntetisk likviditet byggs inte om vid varje `GET /market/summary` (15 min cooldown per ämne); bakgrundsjobb begränsat (max 25 ämnen/tick, var 15:e min). Djup (`/depth`) uppdaterar vid behov. Börs-UI pollar var 60:e s (tidigare 12 s). Fabrik-tick standard 2 s (tidigare 1 s) i appsettings.

## 0.2.4

- **Inloggning:** gäst-`POST /v1/auth/guest` svarar snabbt igen (startpaket flyttat till `GET /v1/me/wallet` / pool / börs i stället för att blockera login).
- **Klient:** tydlig status «Loggar in…» / «Kontaktar servern…»; bättre felmeddelanden; ingen Bearer-header på gäst-login.
- **Äldre spelare:** saknad inventory-pool skapas vid inloggning.

## 0.2.3

- **Börs / order:** tydlig feedback vid «Skicka order» — laddningstext, färgad status (fylld/öppen/fel), snackbar, kassa i sidhuvud, validering (pool/kvantitet), uppskattad köpkostnad; fel från API visas på svenska i stället för dold JSON.

## 0.2.2

- **Startmaterial i pool:** nya spelare (och befintliga utan tidigare startpaket) får **5 grundämnen** (id 1–5), **25 st** vardera, vid gästinloggning eller första besök på börsen.
- Konfiguration: `GameEconomy:StartingElementIds` och `StartingElementQuantityPerStack` i appsettings.

## 0.2.1

- **Slingor tillåtna:** spara/starta plan med t.ex. seaport → boiler → seaport (cykel blockerade inte längre vid `PUT .../plan`).
- **Simulering:** fler transfer-pass per tick + stabil maskinordning vid slinga.
- **Fabrikinformation:** analyserar planen i JSON-editorn (preview) i Edit-läge; visar antal maskiner/rör och «slinga»; MCP/API `GET .../info` inkl. `planConnectionCount` / `planHasCycle`.

## 0.2.0

- **Fabrik-simulering:** deterministisk tick-motor (`BoardTickEngine`) med materialpaket, portbuffertar och maskinprocessorer (Boiler, Heater, Cooler, Mixer, Sorter, SeaportConnector); DNA-blockering under körning.
- **Seaport-pool i tick:** `SeaportPoolGateway` — connector drar/sätter element i spelarens pool vid `Running`.
- **Keyframes:** persistence + `GET /v1/boards/{id}/keyframes/latest` och `.../keyframes?afterTick=`; `SimulationTickHostedService` kör riktig simulering.
- **Fabrikinformation v2:** `GET/POST .../info` med runtime från keyframe, pool och spotpris; cykelvalidering vid spara/start.
- **Webbklient Fabrik:** visuell **SVG-canvas** (drag-placering, port-koppling), fabrikinfo-panel, auto-refresh vid körning; header med **kassa**, snackbar, **SeaportConnector** i butik (ersätter in/out-knappar), auto maskin-id.
- **Börs:** syntetiskt orderdjup och seedad kurshistorik; API `GET /v1/market/summary`, `.../depth`, `.../history`; sida `/exchange`.
- **MCP:** verktyg `boards_info`, `boards_keyframe_latest`, `boards_keyframes` för Azure-smoke.

## 0.1.9

- Webbklient: **Logga ut** i toppraden; ogiltig sparad session rensas automatiskt vid **401** och vid start på Home (undviker «Inloggad» med trasig token efter deploy).
- Tydligare feltext vid **401** (inte längre felaktigt «Kan inte ansluta till API»).

## 0.1.8

- Webbklient **Fabrik** (`/boards`): maskinbutik med priser (t.ex. **Liquid boiler** / `Boiler` för 4000), **maskinlager** efter köp, **placering på plan** via API, **GET plan** från server vid val av plan, rör-UI med **dropdowns** för maskiner och **lediga** in/ut-portar, knappar för **Seaport in/out**-kopplingar.
- API: `GET /v1/content/machine-store`, `GET/POST /v1/me/machine-inventory`, `GET /v1/boards/{id}/plan`, `POST /v1/boards/{id}/place-from-stock`; domän: `MachinePortCatalog`, `MachineStoreCatalog`, wiki poster `SeaportIn` / `SeaportOut`.
- Ekonomi: höjd **StartingCash** (appsettings + standard) så nya spelare har råd med maskinbutiken; API-tester använder **egen** in-memory SQLite per fixture (undviker krock mellan parallella testklasser).

## 0.1.7

- Webbklient: knapp **«Hämta senaste app»** i sidfoten — avregistrerar service worker, rensar Cache API och laddar om (färsk WASM efter deploy / vid kvarhängande cache).

## 0.1.6

- Webbklient: **normaliserar alltid** `HttpClient.BaseAddress` med avslutande `/` (annars kan relativa anrop som `/v1/auth/guest` mot Azure resultera i fel host/URL och `TypeError: Load failed`). Extra skydd om API-bas råkar ligga kvar på loopback när PWA:n inte gör det.
- `index.html`: service worker registreras med `updateViaCache: 'none'` så nyare `service-worker.js` hämtas oftare efter deploy (minskar risk för gammal cachad WASM).
- Gästinloggning: vid anslutningsfel visas **HttpClient.BaseAddress** i feltexten för enklare felsökning i Azure.

## 0.1.5

- API: `UseSwagger` / `UseSwaggerUI` körs **före** Blazor-statiska filer (tidigare `UseWhen` efter dem). Undviker att `MapFallbackToFile("index.html")` fångar `GET /swagger` så att Blazor visar «nothing at this address».

## 0.1.4

- Webbklient: om `ApiBaseUrl` i konfiguration pekar på **localhost** men PWA:n laddas från en **riktig värd** (t.ex. Azure), ignoreras localhost-URL:en och samma ursprung som sidan används — undviker `TypeError: Load failed` när App Service råkar ha `ASPNETCORE_ENVIRONMENT=Development` och WASM därmed läste in dev-URL mot `localhost`.
- `wwwroot/appsettings.Development.json` innehåller inte längre `ApiBaseUrl` (lokal dev-api hanteras av port-heuristik i `Program.cs`).

## 0.1.3

- API: `GET /diagnostics/recent-logs` — oformaterad text med loggrad sedan processstart (ingen autentisering). Aktiveras i **Development** alltid; i övriga miljöer sätt `Diagnostics:ExposeRecentLogEndpoint` till `true` om du behöver den i t.ex. Azure-felsökning (lämna av i produktion).
- Webbklient: tydligare API-bas vid lokal Blazor dev-server (`WasmApplicationEnvironmentName`, fallback till `https://localhost:7145` för kända dev-portar) samt bättre feltext vid anslutningsfel.
- Infrastruktur: EF Core-migrationsfiler borttagna; SQLite-schema skapas med `EnsureCreatedAsync()` (befintlig databasstrategi i kodbasen).
- Övrigt: uppdateringar i Docker/README/KRAVSPEC och API-tester (SQLite-fixture i stället för Postgres-fixture).

## 0.1.2

- API: Swagger/Swashbuckle körs endast för sökvägar under `/swagger`, så rot-URL (`/`) lämnas till Blazor-PWA och fallback till `index.html` utan att Swagger-middleware kan lägga sig i vägen.

## 0.1.1

- Versionsflöde: commit-meddelande vid release är enbart semver; release-noteringar flyttas till denna fil (`releases.md`).

## 0.1.0

- Central `Version` i `Directory.Build.props` för alla projekt; versionsrad i webbklientens sidfot och i Swagger.
- Cursor-regler för commit/push, version/taggar och leveransmeddelande i chatten.
