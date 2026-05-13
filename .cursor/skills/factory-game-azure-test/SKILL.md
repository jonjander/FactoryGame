---
name: factory-game-azure-test
description: >-
  Documents FactoryGame hosted API test environment in Azure (Sweden Central):
  base URL, deployment via Git sync/Oryx, smoke tests, auth headers, and links
  to KRAVSPEC/README. Use when testing against Azure dev, verifying deploy, or
  configuring clients/CORS against the cloud API.
disable-model-invocation: true
---

# FactoryGame — Azure testmiljö (API)

## Bas-URL (dev)

`https://factorygame-h5hmbzgncnazcmgu.swedencentral-01.azurewebsites.net`

- **Region:** Sweden Central (värdnamnet avslöjar `swedencentral-01`).
- **Swagger UI:** `/swagger` (OpenAPI JSON: `/swagger/v1/swagger.json`).
- **Hälsa:** `GET /health` → svarstext `Healthy` vid OK.

Verifierat externt: `/health` och `/swagger/v1/swagger.json` svarar när appen kör. **Gäst-inloggning** (`POST /v1/auth/guest`) kräver att databas/migrationer fungerar i miljön — vid fel (t.ex. 500) se Azure Log stream och `ConnectionStrings__DefaultConnection`.

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

## Klient (Blazor / annat)

- Peka `factory-config.json` (eller motsvarande) mot bas-URL ovan.
- API **CORS:** sätt `Cors__Origins__0` i Azure till klientens URL om du inte kör `AllowAnyOrigin`.

## Lokal verifiering

Kör skriptet (PowerShell):

`.cursor/skills/factory-game-azure-test/scripts/Smoke-AzureApi.ps1`

Valfritt: `-BaseUrl 'https://...'` om URL ändras.

## Primära dokument

- Beteende / API-kontrakt: `KRAVSPEC.md`
- Lokal dev, DB, CI: `README.md`
