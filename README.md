# FactoryGame (MVP)

Server-authoritative factory simulator with exchange, seaport pool, DNA-based base elements, and Blazor WebAssembly client (PWA).

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Docker (optional, e.g. `docker compose` to run the API in a container; **not** required for local `dotnet run`)

## Database (EF Core -- SQL Server + SQLite)

- **Local development:** copy [`src/FactoryGame.Api/appsettings.Local.example.json`](src/FactoryGame.Api/appsettings.Local.example.json) to `appsettings.Local.json` (gitignored) with your SQL Server connection string. ASP.NET Core loads `appsettings.Local.json` automatically. Schema is created/updated with **EF Core migrations** (`Migrate()`) at startup.
- **Azure (production):** set app setting `ConnectionStrings__DefaultConnection` to an Azure SQL string with `Authentication=Active Directory Default` (Web App managed identity). Example:
  `Server=tcp:YOUR_SERVER.database.windows.net,1433;Initial Catalog=fg;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Default;`
- **Tests / Docker (default):** empty `ConnectionStrings:DefaultConnection` in `appsettings.json` -> **shared SQLite in-memory** (`Mode=Memory` + `Cache=Shared`). Schema is created with `EnsureCreatedAsync()` at startup.
- **SQLite file (optional):** connection string starting with `Data Source=` or `Filename=` (e.g. `Data Source=./dev.db`) for persistent local data without SQL Server.
- **Requirements spec** (snapshot from web, etc.): see `KRAVSPEC.md`.

PostgreSQL/Npgsql is **not** used in this codebase.

## Quick start (API + Blazor PWA on the same host)

```bash
dotnet run --project src/FactoryGame.Api
```

- **PWA / UI:** base URL from `launchSettings.json` (e.g. `https://localhost:7145/` or `http://localhost:5176/`) -- same port as the API.
- **REST API:** under **`/v1/...`** (endpoints unchanged).
- **Swagger:** `https://localhost:7145/swagger` (https profile). Swagger middleware runs before the Blazor fallback so `/swagger` is not replaced by the PWA `index.html`.
- **Health:** `GET /health`
- **Diagnostics:** `GET /diagnostics/recent-logs` returns buffered log lines (text, no authentication) since process start. Enabled in **Development**; in production set `Diagnostics:ExposeRecentLogEndpoint` to `true` only when needed (leave `false` by default).

The client selects the API base via **`ApiTarget`** (`Auto`, `SameOrigin`, `LocalDev`, `Azure`, `Custom`) in `wwwroot/appsettings.Development.json` (Debug build only) and URLs in `wwwroot/factory-config.json`. **`Auto`** (default in Azure/Release): same origin as the page on a real host; on localhost with WASM Development -> local API (`https://localhost:7145`). **`dotnet run --project src/FactoryGame.Web`**: use VS profiles **"https (UI -> local API)"** or **"https (UI -> Azure API)"** (`FactoryGameApiTarget` as MSBuild property), or set `ApiTarget` / `ApiBaseUrl` in config. **`dotnet run --project src/FactoryGame.Api`**: UI and API share a port -- SameOrigin automatically.

## Quick start (API in Docker)

```bash
docker compose up --build
```

The API is exposed on port **8080** (same in-memory SQLite as empty `DefaultConnection` unless you change compose). PWA and `/v1` ship with the same image.

## Blazor project (`FactoryGame.Web`) separately

```bash
dotnet run --project src/FactoryGame.Web
```

Used for WASM hot reload / isolated frontend work. Default Debug: **`ApiTarget: LocalDev`** in `appsettings.Development.json`. Profile **"https (UI -> Azure API)"** sets `FactoryGameApiTarget=Azure` at build time (UI locally, API in Azure).

For **API in Azure** with **only** this host: leave `ApiBaseUrl` empty in built `factory-config.json` (same web app). For **UI on another domain** (e.g. Static Web Apps): set `ApiBaseUrl` to the API Web App base URL (no trailing `/`).

On **API** in Azure with a **separate** client domain: `Cors__Origins__0` = the client's exact base URL; when UI and API share a host, a separate CORS origin is normally not needed.

**Published UI separately** (optional): `dotnet publish` on `FactoryGame.Web` and serve `wwwroot` from another URL with `factory-config.json` pointing at the API.

CORS: Development uses `Cors:Origins` in `appsettings.Development.json` on the API project.

## Authentication

- **Guest:** `POST /v1/auth/guest` with JSON `{ "deviceKey": "any-string" }`, then `Authorization: Bearer <token>`.
- **API key:** `X-Api-Key: <plaintext>` (hash stored in the database). Create a key with admin bootstrap below.

## Admin (bootstrap)

Set `Admin:BootstrapToken` (e.g. in `appsettings.Development.json`). Call:

- `GET /v1/admin/players` with header `X-Admin-Token: <token>`
- `POST /v1/admin/api-keys` with the same header and body `{ "playerId": "...", "name": "bot", "scopes": "market,boards" }`  
  The response contains `key` once.

## Environment variables (API)

| Variable | Description |
|----------|-------------|
| `ConnectionStrings__DefaultConnection` | **Empty** -> SQLite in-memory (tests/Docker). **SQL Server** (`Server=...`) locally/Azure. **SQLite file** (`Data Source=...` / `Filename=...`) |
| `ASPNETCORE_ENVIRONMENT` | `Development` / `Production` |
| `Cors__Origins__0` | (optional) allowed client URL |

## Tests

```bash
dotnet test FactoryGame.sln
```

Integration tests (`FactoryGame.Api.Tests`) use the same SQLite in-memory as the default API and **do not require** Docker.

## Azure Web App (API)

**Primary deploy (agent / releases):** local `dotnet publish` + **Zip Deploy** using Azure PublishSettings (gitignored under `.local/`). See skill [`factory-game-azure-deploy`](.cursor/skills/factory-game-azure-deploy/SKILL.md) (`@factory-game-azure-deploy`). After each **Version** bump the agent must deploy this way.

**Optional:** Azure **Deployment Center** (External Git / Oryx) may still be configured in the portal; it is not the agent release path.

**Configuration that must match this codebase (.NET 10, monorepo):**

1. **Stack:** .NET **10** (matches `net10.0` in all `.csproj` files).
2. If Oryx is still used: application setting **`PROJECT`** = `src/FactoryGame.Api/FactoryGame.Api.csproj`.
3. **`global.json`** in the repo root pins **.NET 10 SDK**.
4. Zip Deploy publishes `FactoryGame.Api` (embeds Blazor WASM).

**Azure Portal -> Configuration -> Application settings** (operations):

- `ConnectionStrings__DefaultConnection` -- **Azure SQL** with `Authentication=Active Directory Default` (Web App managed identity). Schema migrates at startup. Empty string = SQLite in-memory (data lost on restart).
- `ASPNETCORE_ENVIRONMENT` = `Production` (important for hosted Blazor WASM: `Development` can cause the client to load dev settings and incorrectly point API calls at `localhost` in the browser.)
- `Cors__Origins__0` -- client base URL if you restrict CORS.

**Logs:** After Zip Deploy, use **Log stream** / Application Insights for app logs. For **app logs**, enable **App Service logs -> Application logging (Filesystem)** and optionally `Logging__LogLevel__Default` = `Information`.

**CI in GitHub:** workflow [`.github/workflows/ci.yml`](.github/workflows/ci.yml) runs build + tests on push/PR to `main`/`master` -- it does **not** deploy to Azure.

For **Azure dev URL, smoke, auth against the cloud:** see skill [`factory-game-azure-test`](.cursor/skills/factory-game-azure-test/SKILL.md) (`@factory-game-azure-test`).

**Publish profile:** store as `.local/FactoryGame.PublishSettings` (gitignored). Do not commit publish profiles or passwords.

## Security (MVP)

- Change `Admin:BootstrapToken` in production or lock admin routes behind the network.
- API keys are stored as SHA-256 hashes.
- Rate limiting: global fixed window per player or IP (see `Program.cs`).

## Structure

- `src/FactoryGame.Api` -- host: OpenAPI/Swagger, `/v1` API, static Blazor WASM files (via reference to `FactoryGame.Web`)
- `src/FactoryGame.Domain` -- DNA, simulation stubs, content
- `src/FactoryGame.Infrastructure` -- EF Core, services, background services
- `src/FactoryGame.Contracts` -- shared DTOs
- `src/FactoryGame.Web` -- Blazor WASM PWA (built into the API host; can run isolated for dev)
- `tests/*` -- unit and integration tests
