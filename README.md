# FactoryGame (MVP)

Server-authoritativ fabriksimulator med börs, seaport-pool, DNA-baserade grundämnen och Blazor WebAssembly-klient (PWA).

## Krav

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Docker (för PostgreSQL och integrationstester; **inte** krav för `dotnet run` på API med standardinställningar)

## Databas (EF Core)

- **Standard:** `ConnectionStrings:DefaultConnection` är **tom** i `appsettings.json` → API startar med **SQLite in-memory** (`Data Source=:memory:;Cache=Shared`), **utan Docker** och **utan Postgres**. Schema skapas med `EnsureCreatedAsync()` (befintliga migrationer är Npgsql-specifika).
- **Postgres:** sätt `ConnectionStrings__DefaultConnection` till en Npgsql-sträng (t.ex. via `docker compose` eller miljövariabel). Vid start körs då `MigrateAsync()` som tidigare.
- **SQLite fil:** anslutningssträng som börjar med `Data Source=` eller `Filename=` → `UseSqlite` + `EnsureCreatedAsync()`.
- **Kravbild** (snapshot från webb m.m.): se `KRAVSPEC.md`.

## Snabbstart (API utan databas-server)

```bash
dotnet run --project src/FactoryGame.Api
```

Swagger: `https://localhost:7145/swagger` (profiler i `launchSettings.json`).

Hälsa: `GET /health`

## Snabbstart (API + PostgreSQL)

```bash
docker compose up -d
dotnet run --project src/FactoryGame.Api
```

Med Postgres kör API `Database.MigrateAsync()` vid start. För manuella migrationer:  
`dotnet ef database update --project src/FactoryGame.Infrastructure --startup-project src/FactoryGame.Api`  
(design-time factory förväntar Postgres om `ConnectionStrings__DefaultConnection` inte pekar på SQLite.)

## Webbklient (Blazor WASM)

```bash
dotnet run --project src/FactoryGame.Web
```

Standard-API i `wwwroot/factory-config.json` är `http://localhost:5176` (lokal API). För att använda **API i Azure** istället: sätt `ApiBaseUrl` till din Web App-URL, t.ex. `https://factorygame-h5hmbzgncnazcmgu.swedencentral-01.azurewebsites.net` (utan avslutande `/`). Då når du UI:t på den port `dotnet run` skriver ut (t.ex. `https://localhost:7048`) medan anrop går till molnet.

På **API** i Azure: lägg till `Cors__Origins__0` = klientens exakta bas-URL (t.ex. `https://localhost:7048`) så webbläsaren får kalla API:t; annars blockeras anrop från Blazor.

**Publicerat UI i Azure** (val när du vill slippa `dotnet run`): bygg `FactoryGame.Web` (`dotnet publish`), lägg ut `wwwroot`-innehållet på t.ex. **Azure Static Web Apps**, **Blob Storage + CDN**, eller en **andra** Web App som bara servar statiska filer — samma `factory-config.json`/`ApiBaseUrl` mot API-Web App:en.

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
| `ConnectionStrings__DefaultConnection` | **Tom** → SQLite in-memory. Annars Npgsql-sträng, eller SQLite (`Data Source=...`) |
| `ASPNETCORE_ENVIRONMENT` | `Development` / `Production` |
| `Cors__Origins__0` | (valfritt) tillåten klient-URL |

## Tester

```bash
dotnet test FactoryGame.sln
```

Integrationstester (`FactoryGame.Api.Tests`) startar Postgres via Testcontainers och kräver Docker.

## Azure Web App (API)

Deploy sker genom att **pusha till GitHub**; Azure **Deployment Center** (External Git + **App Service Build Service** / Oryx) hämtar repot och bygger vid **Sync** eller enligt schemat du satt i portalen.

**Konfiguration som ska stämma med denna kodbas (.NET 10, monorepo):**

1. **Stack:** .NET **10** (matchar `net10.0` i alla `.csproj`).
2. **Application setting `PROJECT`:** `src/FactoryGame.Api/FactoryGame.Api.csproj` så Oryx bygger API-projektet.
3. **`global.json`** i repo-roten styr **.NET 10 SDK** för Oryx.
4. **Branch** i Deployment Center ska matcha den gren du pushar till (t.ex. `master`).

**Azure Portal → Configuration → Application settings** (drift):

- `ConnectionStrings__DefaultConnection` – Npgsql i produktion (tom sträng → SQLite in-memory, data försvinner vid omstart).
- `ASPNETCORE_ENVIRONMENT` = `Production`
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

- `src/FactoryGame.Api` – HTTP, OpenAPI, middleware
- `src/FactoryGame.Domain` – DNA, simuleringsstubbar, innehåll
- `src/FactoryGame.Infrastructure` – EF Core, tjänster, bakgrundstjänster
- `src/FactoryGame.Contracts` – delade DTO:er
- `src/FactoryGame.Web` – Blazor WASM PWA
- `tests/*` – enhets- och integrationstester
