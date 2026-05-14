# Versionshistorik

Kortfattad lista över vad som ingår i varje levererad version. Git-commit för en release har **endast** semver som meddelande (samma värde som `Version` i `Directory.Build.props`); git-tag är `v{Version}`.

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
