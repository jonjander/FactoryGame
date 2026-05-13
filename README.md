# FactoryGame (MVP)

Server-authoritativ fabriksimulator med börs, seaport-pool, DNA-baserade grundämnen och Blazor WebAssembly-klient (PWA).

## Krav

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
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

Standard-API mot `http://localhost:5176`. Ändra `src/FactoryGame.Web/wwwroot/factory-config.json` om API körs på annan bas-URL.

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

Repot innehåller workflow [`.github/workflows/azure-webapp-api.yml`](.github/workflows/azure-webapp-api.yml) som bygger och publicerar **endast** `FactoryGame.Api` till en Linux Web App (.NET 8).

**GitHub (Actions → secrets/variables):**

| Typ | Namn | Innehåll |
|-----|------|----------|
| Secret | `AZURE_WEBAPP_PUBLISH_PROFILE` | Innehållet i nedladdad *Publish profile* från Azure Portal (Web App → **Get publish profile**) |
| Variable | `AZURE_WEBAPP_NAME` | Web App-resursens namn (t.ex. `factorygame-api`) |

Om variabeln `AZURE_WEBAPP_NAME` saknas hoppar workflow över deploy-steget (bygget körs ändå).

**Azure Portal → Web App → Configuration → Application settings** (minsta för riktig drift):

- `ConnectionStrings__DefaultConnection` – Npgsql-sträng till Azure Database for PostgreSQL (eller annan Postgres). Utan denna kör appen SQLite in-memory (data försvinner vid omstart).
- `ASPNETCORE_ENVIRONMENT` = `Production`
- `Cors__Origins__0` = bas-URL till din Blazor WASM / frontend (tom lista ger `AllowAnyOrigin` i kodläget, men sätt explicit i prod om du kan).

**Deployment Center:** om Azure skapar en *egen* GitHub-workflow vid koppling, antingen ta bort den duplicerade eller använd bara en av workflows så samma push inte deployar dubbelt.

**Deploy från Git utan GitHub Actions (Oryx/Kudu):** sätt appinställningen **`PROJECT`** till `src/FactoryGame.Api/FactoryGame.Api.csproj` så rätt projekt byggs i en monorepo-lösning.

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
