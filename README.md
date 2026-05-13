# FactoryGame (MVP)

Server-authoritativ fabriksimulator med börs, seaport-pool, DNA-baserade grundämnen och Blazor WebAssembly-klient (PWA).

## Krav

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Docker (för Postgres och integrationstester)

## Snabbstart (API + databas)

```bash
docker compose up -d
dotnet run --project src/FactoryGame.Api
```

API kör `Database.MigrateAsync()` vid start. För manuella migrationer:  
`dotnet ef database update --project src/FactoryGame.Infrastructure --startup-project src/FactoryGame.Api`

Swagger: `https://localhost:7145/swagger` (profiler i `launchSettings.json`).

Hälsa: `GET /health`

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
| `ConnectionStrings__DefaultConnection` | Npgsql-anslutningssträng |
| `ASPNETCORE_ENVIRONMENT` | `Development` / `Production` |
| `Cors__Origins__0` | (valfritt) tillåten klient-URL |

## Tester

```bash
dotnet test FactoryGame.sln
```

Integrationstester (`FactoryGame.Api.Tests`) startar Postgres via Testcontainers och kräver Docker.

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
