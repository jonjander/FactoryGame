---
name: factory-game-mcp-server
description: >-
  Documents the FactoryGame MCP server (tools/factorygame-mcp): all MCP tools,
  HTTP mapping, auth/env setup, smoke/playtest scripts, and limitations vs PWA.
  Use when configuring factorygame MCP, calling MCP tools, extending the server,
  or assessing GUI/API parity for headless testing.
disable-model-invocation: true
---

# FactoryGame — MCP-server

## Vad det är

Stdio MCP-server (`factorygame` / `user-factorygame` i Cursor) som proxar **HTTP-anrop** mot FactoryGame API. Källkod: `tools/factorygame-mcp/`. Servern är **inte** spelmotor — den kör inga ticks lokalt.

**Auktoritet:** samma som webklienten och Swagger (`/swagger/v1/swagger.json`). Spelregler: `KRAVSPEC.md`.

## Setup och verifiering

1. `npm install` + `npm run build` i `tools/factorygame-mcp/` (efter git pull).
2. Cursor MCP-konfiguration (projekt [`.cursor/mcp.json`](../../mcp.json)) — **två servrar**:

```json
{
  "mcpServers": {
    "factorygame": {
      "type": "stdio",
      "command": "node",
      "args": ["${workspaceFolder}/tools/factorygame-mcp/dist/index.js"],
      "env": {
        "FACTORYGAME_BASE_URL": "https://factorygame-h5hmbzgncnazcmgu.swedencentral-01.azurewebsites.net"
      }
    },
    "factorygame-local": {
      "type": "stdio",
      "command": "node",
      "args": ["${workspaceFolder}/tools/factorygame-mcp/dist/index.js"],
      "env": {
        "FACTORYGAME_BASE_URL": "http://localhost:5176"
      }
    }
  }
}
```

| MCP-server | Bas-URL | Användning |
|------------|---------|------------|
| `factorygame` | Azure dev | Standard / repo-ägarens driftverifiering |
| `factorygame-local` | `http://localhost:5176` | Lokal API (`http`-profil, undviker dev-cert i Node) |

**Lokal API:** `dotnet run --project src/FactoryGame.Api --launch-profile http` → `GET http://localhost:5176/health` = `Healthy`.

Valfritt: `envFile` → `tools/factorygame-mcp/.env` (se `.env.example`). **Starta om Cursor** efter MCP-ändringar — båda servrarna kan vara aktiverade samtidigt (olika namn).

3. **Rök Azure:** `npm run smoke`
4. **Rök lokal:** `npm run smoke:local` (väntar på `/health`, kräver körande lokal API)
5. **E2E Azure:** `npm run playtest`
6. **E2E lokal:** `npm run playtest:local`
7. **Bas-URL:** `FACTORYGAME_BASE_URL` (kod-default = Azure om unset). Se `@factory-game-azure-test`.

**Fixtures:** exempelplaner i `tools/factorygame-mcp/fixtures/plans.json`.

## Autentisering

| Typ | MCP | Miljö / argument |
|-----|-----|------------------|
| Gäst | `guest_auth` | `deviceKey` → `sessionToken` |
| Spelare | Bearer på skyddade verktyg | `sessionToken` eller `FACTORYGAME_SESSION_TOKEN` |
| API-nyckel | `X-Api-Key` | `apiKey` eller `FACTORYGAME_API_KEY` (**slår före** bearer) |
| Admin | `admin_*` | **Endast** `FACTORYGAME_ADMIN_TOKEN` (aldrig tool-argument) |

**Checka aldrig in** session, API-nycklar eller admin-token i repo eller commit-meddelanden.

**Utanför MCP:** OAuth / IdP-inloggning (F1). Headless test använder gäst eller API-nyckel.

## Verktyg → HTTP (32 verktyg)

| MCP-verktyg | HTTP | Auth |
|-------------|------|------|
| `guest_auth` | `POST /v1/auth/guest` | nej |
| `content_list_elements` | `GET /v1/content/elements` | nej |
| `content_wiki` | `GET /v1/content/wiki` | nej |
| `content_machine_store` | `GET /v1/content/machine-store` | nej |
| `market_open_orders` | `GET /v1/market/orders/open` | nej |
| `market_recent_trades` | `GET /v1/market/trades` | nej |
| `market_summary` | `GET /v1/market/summary` | ja |
| `market_element_depth` | `GET /v1/market/elements/{id}/depth` | nej |
| `market_element_history` | `GET /v1/market/elements/{id}/history` | nej |
| `market_orders_mine` | `GET /v1/market/orders/mine` | ja |
| `market_place_order` | `POST /v1/market/orders` | ja |
| `player_wallet` | `GET /v1/me/wallet` | ja |
| `player_pool` | `GET /v1/me/pool` | ja |
| `player_pool_view` | `GET /v1/me/pool/view` | ja |
| `player_transactions` | `GET /v1/me/transactions` | ja |
| `player_machine_inventory` | `GET /v1/me/machine-inventory` | ja |
| `player_machine_purchase` | `POST /v1/me/machine-inventory/purchase` | ja |
| `boards_create` | `POST /v1/boards` | ja |
| `boards_list` | `GET /v1/boards` | ja |
| `boards_save_plan` | `PUT /v1/boards/{id}/plan` | ja |
| `boards_get_plan` | `GET /v1/boards/{id}/plan` | ja |
| `boards_place_from_stock` | `POST /v1/boards/{id}/place-from-stock` | ja |
| `boards_info_preview` | `POST /v1/boards/{id}/info/preview` | ja |
| `boards_start` / `boards_stop` | `POST .../start` · `.../stop` | ja |
| `boards_snapshot` | `GET .../snapshot` | ja |
| `boards_info` | `GET .../info` | ja |
| `boards_keyframe_latest` | `GET .../keyframes/latest` | ja |
| `boards_keyframes` | `GET .../keyframes?afterTick=` | ja |
| `admin_list_players` | `GET /v1/admin/players` | admin |
| `admin_create_api_key` | `POST /v1/admin/api-keys` | admin |
| `diagnostics_recent_logs` | `GET /diagnostics/recent-logs` | nej* |

\* Kräver att endpoint är påslagen på värden (Production: `Diagnostics__ExposeRecentLogEndpoint=true`).

### Locale

Flera verktyg accepterar valfritt `locale` (sätter `Accept-Language`; content-routes sätter även `?locale=`). Webben använder ofta `sv`.

### Svarformat

Varje verktyg returnerar text: `HTTP {status}` + JSON-body (eller rå text för diagnostik). Fel → `isError: true`. Saknad auth → status `0` med tydligt meddelande. `HTTP 204` (köp/placera) är OK med tom body.

### Plan-schema

```json
{
  "machines": [{ "id": "string", "type": "string", "settings": {} }],
  "connections": [{ "fromId": "string", "toId": "string", "fromPort": "string", "toPort": "string" }]
}
```

Swagger / `BoardPlanDto` är sanning för maskintyper och portnamn.

## GUI-paritet

Alla web-GUI `/v1`-routes har motsvarande MCP-verktyg. Se [gui-parity.md](gui-parity.md).

## Begränsningar

- **Endast HTTP** — ingen lokal sim, WebSocket eller service worker.
- **Ingen PWA/offline** — köade sparningar, merge-UI och offline-börs (F26).
- **Ingen visuell klient** — drag-connect, animation, mobil-layout (F19).
- **Polling** — `boards_keyframes` / `boards_snapshot`; ingen klientinterpolation.
- **OAuth** — använd gäst eller API-nyckel.

## Utöka servern

1. Lägg verktyg i `tools/factorygame-mcp/src/index.ts`.
2. `npm run build` + starta om Cursor.
3. Uppdatera denna skill och [gui-parity.md](gui-parity.md).
4. Kör `npm run smoke` och ev. `npm run playtest`.

## Relaterat

- **Playtest:** `@factory-game-mcp-playtest`, subagent `factory-game-playtester`.
- **GUI-paritet:** [gui-parity.md](gui-parity.md).
- **Azure:** `@factory-game-azure-test`.
