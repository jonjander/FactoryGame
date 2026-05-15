# Versionshistorik

Kortfattad lista över vad som ingår i varje levererad version. Git-commit för en release har **endast** semver som meddelande (samma värde som `Version` i `Directory.Build.props`); git-tag är `v{Version}`.

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
