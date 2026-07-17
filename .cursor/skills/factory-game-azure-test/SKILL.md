---
name: factory-game-azure-test
description: >-
  Documents FactoryGame hosted API test environment in Azure (Sweden Central):
  base URL, deployment via Git sync/Oryx, smoke tests, auth headers, diagnostics
  log URL after user tests, and links to KRAVSPEC/README. Use when testing
  against Azure dev, verifying deploy, or configuring clients/CORS against the
  cloud API.
disable-model-invocation: true
---

# FactoryGame -- Azure test environment (API)

## Base URL (dev)

`https://factorygame-h5hmbzgncnazcmgu.swedencentral-01.azurewebsites.net`

- **Region:** Sweden Central (hostname contains `swedencentral-01`).
- **Swagger UI:** `/swagger` (OpenAPI JSON: `/swagger/v1/swagger.json`).
- **Health:** `GET /health` -> response text `Healthy` when OK.
- **Buffered logs (after user test in Azure):** `GET /diagnostics/recent-logs` -- full URL:  
  `https://factorygame-h5hmbzgncnazcmgu.swedencentral-01.azurewebsites.net/diagnostics/recent-logs`  
  Response: `text/plain` with lines since process start. In **Production** app setting **`Diagnostics__ExposeRecentLogEndpoint` = `true`** is required (Azure Portal -> Configuration); in **Development** the endpoint is always on. The agent can call this URL (e.g. `WebFetch`) after the user reports behavior in the cloud.

Verified externally: `/health` and `/swagger/v1/swagger.json` respond when the app runs. **Blazor PWA** is served from the same host as the API (root + `MapFallbackToFile`). **Guest login** (`POST /v1/auth/guest`) requires EF startup against the database to work -- on failure (e.g. 500) see Azure Log stream and `ConnectionStrings__DefaultConnection` (Azure SQL with `Authentication=Active Directory Default`; Web App managed identity needs DB permission).

## Deploy (how code gets here)

- **Source:** GitHub repo `https://github.com/jonjander/FactoryGame.git` (push to chosen branch, e.g. `master`).
- **Azure:** Deployment Center with **External Git** + **App Service Build Service** (Oryx); **Sync** (or scheduled pull) builds on Azure.
- **Monorepo:** app setting **`PROJECT`** = `src/FactoryGame.Api/FactoryGame.Api.csproj` (build correct project).
- **Stack:** .NET **10** (`net10.0`); repo has **`global.json`** for SDK 10.
- **Details:** see `README.md` -> section **Azure Web App (API)**.

## Quick smoke (authenticated guest)

1. `POST /v1/auth/guest` with JSON `{"deviceKey":"<any-string>"}` -> get `playerId`, `sessionToken` (camelCase in JSON).
2. `GET /v1/me/wallet` with header `Authorization: Bearer <sessionToken>`.

Other routes per Swagger (boards, market, content, admin etc.).

## Admin / secrets

- **Admin:** `X-Admin-Token` against `/v1/admin/*` -- value set in Azure **Application settings** (`Admin:BootstrapToken`); **never store** token in repo or chat.
- **FTP/zip deploy:** not used in current flow; only Git -> Oryx.

## Client (Blazor UI)

The **UI** is built into the same Web App as the API (`FactoryGame.Api` references `FactoryGame.Web`); published app has PWA at root and API under `/v1`. **Repo owner** verifies UI in **Azure** (see base URL above), not by running locally on their machine.

- **Agent in Cursor:** runs `dotnet build` / `dotnet test` and smoke scripts below when needed -- that is **local agent verification**, not something the repo owner must run.

## Local verification (agent / CI)

Run the script (PowerShell) in Cursor or CI:

`.cursor/skills/factory-game-azure-test/scripts/Smoke-AzureApi.ps1`

Use **`-BaseUrl 'https://factorygame-h5hmbzgncnazcmgu.swedencentral-01.azurewebsites.net'`** for smoke against the deployed app, or `-BaseUrl 'https://...'` if URL changes. For local API build validation only: run `dotnet test` / `dotnet run` on the **agent** side per `README.md` (repo owner does not need to do this).

## Primary documents

- Behavior / API contract: `KRAVSPEC.md`
- Local dev, DB, CI: `README.md`
