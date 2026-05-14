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

# FactoryGame — Azure testmiljö (API)

## Bas-URL (dev)

`https://factorygame-h5hmbzgncnazcmgu.swedencentral-01.azurewebsites.net`

- **Region:** Sweden Central (värdnamnet avslöjar `swedencentral-01`).
- **Swagger UI:** `/swagger` (OpenAPI JSON: `/swagger/v1/swagger.json`).
- **Hälsa:** `GET /health` → svarstext `Healthy` vid OK.
- **Buffrade loggar (efter användarens test i Azure):** `GET /diagnostics/recent-logs` — full URL:  
  `https://factorygame-h5hmbzgncnazcmgu.swedencentral-01.azurewebsites.net/diagnostics/recent-logs`  
  Svar: `text/plain` med rader sedan processstart. I **Production** krävs appinställning **`Diagnostics__ExposeRecentLogEndpoint` = `true`** (Azure Portal → Configuration); i **Development** är endpointen alltid på. Agenten kan anropa denna URL (t.ex. `WebFetch`) efter att användaren rapporterat beteende i molnet.

Verifierat externt: `/health` och `/swagger/v1/swagger.json` svarar när appen kör. **Blazor PWA** servas från samma värd som API (rot + `MapFallbackToFile`). **Gäst-inloggning** (`POST /v1/auth/guest`) kräver att SQLite/EF-start fungerar i miljön — vid fel (t.ex. 500) se Azure Log stream och `ConnectionStrings__DefaultConnection` (tom = in-memory; fil-sökväg för beständig data).

## Deploy (hur koden hamnar här)

- **Källa:** GitHub-repo `https://github.com/jonjander/FactoryGame.git` (push till vald branch, t.ex. `master`).
- **Azure:** Deployment Center med **External Git** + **App Service Build Service** (Oryx); **Sync** (eller schemalagd pull) bygger på Azure.
- **Monorepo:** appinställning **`PROJECT`** = `src/FactoryGame.Api/FactoryGame.Api.csproj` (bygg rätt projekt).
- **Stack:** .NET **10** (`net10.0`); repo har **`global.json`** för SDK 10.
- **Detaljer:** se `README.md` → avsnittet **Azure Web App (API)**.

## Snabb smoke (autentiserad gäst)

1. `POST /v1/auth/guest` med JSON `{"deviceKey":"<valfri-sträng>"}` → får `playerId`, `sessionToken` (camelCase i JSON).
2. `GET /v1/me/wallet` med header `Authorization: Bearer <sessionToken>`.

Övriga routes enligt Swagger (boards, market, content, admin m.m.).

## Admin / hemligheter

- **Admin:** `X-Admin-Token` mot `/v1/admin/*` — värdet sätts i Azure **Application settings** (`Admin:BootstrapToken`); **lagra aldrig** token i repo eller i chat.
- **FTP/zip-deploy:** används inte i nuvarande flöde; endast Git → Oryx.

## Klient (Blazor UI)

Själva **gränssnittet** byggs in i samma Web App som API:t (`FactoryGame.Api` refererar `FactoryGame.Web`); publicerad app har PWA på rot och API under `/v1`. **Repo-ägaren** verifierar UI i **Azure** (se bas-URL ovan), inte genom lokal körning på egen maskin.

- **Agenten i Cursor:** kör `dotnet build` / `dotnet test` och vid behov smoke-skript nedan — det är **lokal agentverifiering**, inte något repo-ägaren måste köra.

## Lokal verifiering (agent / CI)

Kör skriptet (PowerShell) i Cursor eller CI:

`.cursor/skills/factory-game-azure-test/scripts/Smoke-AzureApi.ps1`

Använd **`-BaseUrl 'https://factorygame-h5hmbzgncnazcmgu.swedencentral-01.azurewebsites.net'`** för smoke mot den deployade appen, eller `-BaseUrl 'https://...'` om URL ändras. För enbart lokal API-byggvalidering: kör `dotnet test` / `dotnet run` på **agentens** sida enligt `README.md` (repo-ägaren behöver inte göra detta).

## Primära dokument

- Beteende / API-kontrakt: `KRAVSPEC.md`
- Lokal dev, DB, CI: `README.md`
