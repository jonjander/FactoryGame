# FactoryGame (MVP)

Server-authoritativ fabriksimulator med börs, seaport-pool, DNA-baserade grundämnen och Blazor WebAssembly-klient (PWA).

## Krav

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Docker (valfritt, t.ex. `docker compose` för att köra API i container; **inte** krav för `dotnet run` lokalt)

## Databas (EF Core + SQLite)

- **Standard:** `ConnectionStrings:DefaultConnection` är **tom** i `appsettings.json` → API startar med **delad SQLite in-memory** (namngiven `Mode=Memory` + `Cache=Shared`). Schema skapas med `EnsureCreatedAsync()` vid start.
- **SQLite fil:** sätt `ConnectionStrings__DefaultConnection` till en sträng som börjar med `Data Source=` eller `Filename=` (t.ex. `Data Source=./dev.db`) för beständig lokal data.
- **Kravbild** (snapshot från webb m.m.): se `KRAVSPEC.md`.

PostgreSQL/Npgsql används **inte** i denna kodbas; andra SQL-providers kan återintroduceras senare om teamet väljer det.

## Snabbstart (API + Blazor PWA på samma värd)

```bash
dotnet run --project src/FactoryGame.Api
```

- **PWA / UI:** bas-URL från `launchSettings.json` (t.ex. `https://localhost:7145/` eller `http://localhost:5176/`) — samma port som API:t.
- **REST API:** under **`/v1/...`** (oförändrade endpoints).
- **Swagger:** `https://localhost:7145/swagger` (https-profilen). Middleware för Swagger körs före Blazor-fallback så `/swagger` inte ersätts av PWA:ns `index.html`.
- **Hälsa:** `GET /health`
- **Diagnostik:** `GET /diagnostics/recent-logs` returnerar buffrade loggrad (text, ingen autentisering) sedan processstart. Påslagen i **Development**; i produktion sätt `Diagnostics:ExposeRecentLogEndpoint` till `true` endast vid behov (lämna `false` som standard).

Klienten väljer API-bas via **`ApiTarget`** (`Auto`, `SameOrigin`, `LocalDev`, `Azure`, `Custom`) i `wwwroot/appsettings.Development.json` (endast Debug-build) och URL:er i `wwwroot/factory-config.json`. **`Auto`** (standard i Azure/Release): samma ursprung som sidan på riktig värd; på localhost med WASM Development → lokal API (`https://localhost:7145`). **`dotnet run --project src/FactoryGame.Web`**: använd VS-profilerna **«https (UI → lokal API)»** eller **«https (UI → Azure API)»** (`FactoryGameApiTarget` som MSBuild-egenskap), eller sätt `ApiTarget` / `ApiBaseUrl` i config. **`dotnet run --project src/FactoryGame.Api`**: UI och API delar port — SameOrigin automatiskt.

## Snabbstart (API i Docker)

```bash
docker compose up --build
```

API exponeras på port **8080** (samma in-memory SQLite som tom `DefaultConnection` om du inte ändrar compose).

API exponeras på port **8080** (samma in-memory SQLite som tom `DefaultConnection` om du inte ändrar compose). PWA och `/v1` följer med samma avbildning.

## Blazor-projektet (`FactoryGame.Web`) separat

```bash
dotnet run --project src/FactoryGame.Web
```

Används för WASM hot reload / isolerad frontendarbete. Standard Debug: **`ApiTarget: LocalDev`** i `appsettings.Development.json`. Profil **«https (UI → Azure API)»** sätter `FactoryGameApiTarget=Azure` vid build (UI lokalt, API i Azure).

För **API i Azure** med **endast** denna host: lämna `ApiBaseUrl` tom i byggda `factory-config.json` (samma webbapp). För **UI på annan domän** (t.ex. Static Web Apps): sätt `ApiBaseUrl` till API-Web App:ens bas-URL (utan avslutande `/`).

På **API** i Azure med **separat** klientdomän: `Cors__Origins__0` = klientens exakta bas-URL; när UI och API delar host behövs normalt ingen separat CORS-ursprung.

**Publicerat UI separat** (valfritt): `dotnet publish` på `FactoryGame.Web` och serva `wwwroot` mot en annan URL med `factory-config.json` som pekar på API:t.

CORS: Development använder `Cors:Origins` i `appsettings.Development.json` på API-projektet.

## Autentisering

- **Gäst:** `POST /v1/auth/guest` med JSON `{ "deviceKey": "valfri-sträng" }`, sedan `Authorization: Bearer <token>`.
- **API-nyckel:** `X-Api-Key: <plaintext>` (hash lagras i databasen). Skapa nyckel med admin-bootstrap nedan.

## Admin (bootstrap)

Sätt `Admin:BootstrapToken` (t.ex. i `appsettings.Development.json`). Anropa:

- `GET /v1/admin/players` med header `X-Admin-Token: <token>`
- `POST /v1/admin/api-keys` med samma header och body `{ "playerId": "...", "name": "bot", "scopes": "market,boards" }`  
  Svaret innehåller `key` en gång.

## Miljövariabler (API)

| Variabel | Beskrivning |
|----------|-------------|
| `ConnectionStrings__DefaultConnection` | **Tom** → SQLite in-memory. Annars SQLite (`Data Source=...` / `Filename=...`) |
| `ASPNETCORE_ENVIRONMENT` | `Development` / `Production` |
| `Cors__Origins__0` | (valfritt) tillåten klient-URL |

## Tester

```bash
dotnet test FactoryGame.sln
```

Integrationstester (`FactoryGame.Api.Tests`) använder samma SQLite in-memory som standard-API och **kräver inte** Docker.

## Azure Web App (API)

Deploy sker genom att **pusha till GitHub**; Azure **Deployment Center** (External Git + **App Service Build Service** / Oryx) hämtar repot och bygger vid **Sync** eller enligt schemat du satt i portalen.

**Konfiguration som ska stämma med denna kodbas (.NET 10, monorepo):**

1. **Stack:** .NET **10** (matchar `net10.0` i alla `.csproj`).
2. **Application setting `PROJECT`:** `src/FactoryGame.Api/FactoryGame.Api.csproj` så Oryx bygger API-projektet.
3. **`global.json`** i repo-roten styr **.NET 10 SDK** för Oryx.
4. **Branch** i Deployment Center ska matcha den gren du pushar till (t.ex. `master`).

**Azure Portal → Configuration → Application settings** (drift):

- `ConnectionStrings__DefaultConnection` – SQLite-fil för beständig data i molnet, eller tom sträng för in-memory (data försvinner vid omstart / instansbyte).
- `ASPNETCORE_ENVIRONMENT` = `Production` (viktigt för värdad Blazor WASM: `Development` kan få klienten att ladda dev-inställningar och felaktigt peka API-anrop mot `localhost` i webbläsaren.)
- `Cors__Origins__0` – klientens bas-URL om du begränsar CORS.

**Loggar:** bygg-/deploy-logg för Oryx finns i **Deployment Center** / **Log stream** när källan är Azure-bygge. För **apploggar**, slå på **App Service logs → Application logging (Filesystem)** och ev. `Logging__LogLevel__Default` = `Information`.

**CI i GitHub:** workflow [`.github/workflows/ci.yml`](.github/workflows/ci.yml) kör build + tester på push/PR till `main`/`master` — den deployar **inte** till Azure.

För **Azure dev-URL, smoke, auth mot molnet:** se skill [`factory-game-azure-test`](.cursor/skills/factory-game-azure-test/SKILL.md) (`@factory-game-azure-test`).

Eventuella **gamla GitHub-secrets** för FTPS/publish profile används inte längre av detta repo; du kan ta bort dem i repo-inställningar om du vill städa.

## Säkerhet (MVP)

- Byt `Admin:BootstrapToken` i produktion eller stäng av admin-routes bakom nätverk.
- API-nycklar lagras som SHA-256-hash.
- Rate limiting: global fast fönster per spelare eller IP (se `Program.cs`).

## Struktur

- `src/FactoryGame.Api` – värd: OpenAPI/Swagger, `/v1`-API, statiska Blazor WASM-filer (via referens till `FactoryGame.Web`)
- `src/FactoryGame.Domain` – DNA, simuleringsstubbar, innehåll
- `src/FactoryGame.Infrastructure` – EF Core, tjänster, bakgrundstjänster
- `src/FactoryGame.Contracts` – delade DTO:er
- `src/FactoryGame.Web` – Blazor WASM PWA (byggs in i API-värden; kan köras isolerat för dev)
- `tests/*` – enhets- och integrationstester
