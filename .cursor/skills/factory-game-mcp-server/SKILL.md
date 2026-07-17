---
name: factory-game-mcp-server
description: >-
  Documents the FactoryGame MCP server (tools/factorygame-mcp): all MCP tools,
  HTTP mapping, auth/env setup, smoke/playtest scripts, and limitations vs PWA.
  Use when configuring factorygame MCP, calling MCP tools, extending the server,
  or assessing GUI/API parity for headless testing.
disable-model-invocation: true
---

# FactoryGame -- MCP server

## What it is

Stdio MCP server (`factorygame` / `user-factorygame` in Cursor) that proxies **HTTP calls** to the FactoryGame API. Source: `tools/factorygame-mcp/`. The server is **not** the game engine -- it does not run ticks locally.

**Authority:** same as web client and Swagger (`/swagger/v1/swagger.json`). Game rules: `KRAVSPEC.md`.

## Setup and verification

1. `npm install` + `npm run build` in `tools/factorygame-mcp/` (after git pull).
2. Cursor MCP configuration (project [`.cursor/mcp.json`](../../mcp.json)) -- **two servers**:

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

| MCP server | Base URL | Usage |
|------------|---------|------------|
| `factorygame` | Azure dev | Default / repo owner operations verification |
| `factorygame-local` | `http://localhost:5176` | Local API (`http` profile, avoids dev cert in Node) |

**Local API:** `dotnet run --project src/FactoryGame.Api --launch-profile http` -> `GET http://localhost:5176/health` = `Healthy`.

Optional: `envFile` -> `tools/factorygame-mcp/.env` (see `.env.example`). **Restart Cursor** after MCP changes -- both servers can be enabled at once (different names).

3. **Smoke Azure:** `npm run smoke`
4. **Smoke local:** `npm run smoke:local` (waits for `/health`, requires running local API)
5. **E2E Azure:** `npm run playtest`
6. **E2E local:** `npm run playtest:local`
7. **Base URL:** `FACTORYGAME_BASE_URL` (code default = Azure if unset). See `@factory-game-azure-test`.

**Fixtures:** example plans in `tools/factorygame-mcp/fixtures/plans.json`.

## Authentication

| Type | MCP | Env / argument |
|-----|-----|------------------|
| Guest | `guest_auth` | `deviceKey` -> `sessionToken` |
| Player | Bearer on protected tools | `sessionToken` or `FACTORYGAME_SESSION_TOKEN` |
| API key | `X-Api-Key` | `apiKey` or `FACTORYGAME_API_KEY` (**takes precedence** over bearer) |
| Admin | `admin_*` | **Only** `FACTORYGAME_ADMIN_TOKEN` (never tool argument) |

**Never check in** session, API keys, or admin token in repo or commit messages.

**Outside MCP:** OAuth / IdP login (F1). Headless test uses guest or API key.

## Tools -> HTTP (32 tools)

| MCP tool | HTTP | Auth |
|-------------|------|------|
| `guest_auth` | `POST /v1/auth/guest` | no |
| `content_list_elements` | `GET /v1/content/elements` | no |
| `content_wiki` | `GET /v1/content/wiki` | no |
| `content_machine_store` | `GET /v1/content/machine-store` | no |
| `market_open_orders` | `GET /v1/market/orders/open` | no |
| `market_recent_trades` | `GET /v1/market/trades` | no |
| `market_summary` | `GET /v1/market/summary` | yes |
| `market_element_depth` | `GET /v1/market/elements/{id}/depth` | no |
| `market_element_history` | `GET /v1/market/elements/{id}/history` | no |
| `market_orders_mine` | `GET /v1/market/orders/mine` | yes |
| `market_place_order` | `POST /v1/market/orders` | yes |
| `player_wallet` | `GET /v1/me/wallet` | yes |
| `player_pool` | `GET /v1/me/pool` | yes |
| `player_pool_view` | `GET /v1/me/pool/view` | yes |
| `player_transactions` | `GET /v1/me/transactions` | yes |
| `player_machine_inventory` | `GET /v1/me/machine-inventory` | yes |
| `player_machine_purchase` | `POST /v1/me/machine-inventory/purchase` | yes |
| `boards_create` | `POST /v1/boards` | yes |
| `boards_list` | `GET /v1/boards` | yes |
| `boards_save_plan` | `PUT /v1/boards/{id}/plan` | yes |
| `boards_get_plan` | `GET /v1/boards/{id}/plan` | yes |
| `boards_place_from_stock` | `POST /v1/boards/{id}/place-from-stock` | yes |
| `boards_info_preview` | `POST /v1/boards/{id}/info/preview` | yes |
| `boards_start` / `boards_stop` | `POST .../start` / `.../stop` | yes |
| `boards_snapshot` | `GET .../snapshot` | yes |
| `boards_info` | `GET .../info` | yes |
| `boards_keyframe_latest` | `GET .../keyframes/latest` | yes |
| `boards_keyframes` | `GET .../keyframes?afterTick=` | yes |
| `admin_list_players` | `GET /v1/admin/players` | admin |
| `admin_create_api_key` | `POST /v1/admin/api-keys` | admin |
| `diagnostics_recent_logs` | `GET /diagnostics/recent-logs` | no* |

\* Requires endpoint enabled on host (Production: `Diagnostics__ExposeRecentLogEndpoint=true`).

### Locale

Several tools accept optional `locale` (sets `Accept-Language`; content routes also set `?locale=`). The web app often uses `en`.

### Response format

Each tool returns text: `HTTP {status}` + JSON body (or raw text for diagnostics). Errors -> `isError: true`. Missing auth -> status `0` with clear message. `HTTP 204` (buy/place) is OK with empty body.

### Plan schema

```json
{
  "machines": [{ "id": "string", "type": "string", "settings": {} }],
  "connections": [{ "fromId": "string", "toId": "string", "fromPort": "string", "toPort": "string" }]
}
```

Swagger / `BoardPlanDto` is truth for machine types and port names.

## GUI parity

All web GUI `/v1` routes have matching MCP tools. See [gui-parity.md](gui-parity.md).

## Limitations

- **HTTP only** -- no local sim, WebSocket, or service worker.
- **No PWA/offline** -- queued saves, merge UI, offline exchange (F26).
- **No visual client** -- drag-connect, animation, mobile layout (F19).
- **Polling** -- `boards_keyframes` / `boards_snapshot`; no client interpolation.
- **OAuth** -- use guest or API key.

## Extend server

1. Add tool in `tools/factorygame-mcp/src/index.ts`.
2. `npm run build` + restart Cursor.
3. Update this skill and [gui-parity.md](gui-parity.md).
4. Run `npm run smoke` and optionally `npm run playtest`.

## Related

- **Playtest:** `@factory-game-mcp-playtest`, subagent `factory-game-playtester`.
- **GUI parity:** [gui-parity.md](gui-parity.md).
- **Azure:** `@factory-game-azure-test`.
