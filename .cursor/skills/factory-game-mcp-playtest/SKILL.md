---
name: factory-game-mcp-playtest
description: >-
  Headless FactoryGame playtesting via the factorygame MCP server (stdio): guest
  auth, boards, market, player, content, admin, diagnostics. Use when driving the
  hosted API from Cursor without the PWA, or when validating flows tool-by-tool.
  Pair with factory-game-azure-test for base URL and Azure verification norms.
disable-model-invocation: true
---

# FactoryGame — MCP headless playtest

## Syfte

Projektets MCP-server (`factorygame` i `.cursor/mcp.json`) exponerar verktyg som mappar 1:1 mot HTTP-API:t under `/v1` (plus diagnostik). Samma auktoritet och regler som i `KRAVSPEC.md`; Swagger (`/swagger/v1/swagger.json` på värd) är schemasanningskälla.

## Innan du kör verktyg

1. **Bygg MCP-paketet** efter git pull: `npm install` och `npm run build` i `tools/factorygame-mcp/`.
2. **Valfri MCP-rök:** `npm run smoke` i samma katalog (bygger och kör stdio-klient mot `dist/index.js` + Azure).
3. **Starta om Cursor** efter ändringar i `.cursor/mcp.json` eller miljövariabler för MCP.
3. **Bas-URL:** sätts med `FACTORYGAME_BASE_URL` (MCP `env` i `.cursor/mcp.json` sätter dev-Azure som standard). Övrigt: se `@factory-game-azure-test`.

## Autentisering

- **Gäst:** anropa `guest_auth` med `deviceKey` → använd `sessionToken` i efterföljande verktyg via argumentet `sessionToken` eller sätt processmiljön `FACTORYGAME_SESSION_TOKEN` (t.ex. i MCP-serverns `env` i Cursor-inställningar, eller valfri `envFile` — se `tools/factorygame-mcp/.env.example`).
- **API-nyckel:** header motsvar `X-Api-Key`; i MCP använd argument `apiKey` eller miljö `FACTORYGAME_API_KEY` (API-nyckel slår före bearer om båda finns).
- **Admin:** verktygen `admin_*` läser **endast** `FACTORYGAME_ADMIN_TOKEN` från miljö. **Checka aldrig in** admin-token, API-nycklar eller sessionstoken i repo eller i spårbara tool-argument i loggar.

## Verktygsöversikt (kort)

| Verktyg | HTTP |
|--------|------|
| `guest_auth` | `POST /v1/auth/guest` |
| `content_list_elements` | `GET /v1/content/elements` |
| `content_wiki` | `GET /v1/content/wiki` |
| `market_open_orders` | `GET /v1/market/orders/open` |
| `market_recent_trades` | `GET /v1/market/trades` |
| `market_place_order` | `POST /v1/market/orders` |
| `player_wallet` | `GET /v1/me/wallet` |
| `player_pool` | `GET /v1/me/pool` |
| `player_transactions` | `GET /v1/me/transactions` |
| `boards_create` | `POST /v1/boards` |
| `boards_list` | `GET /v1/boards` |
| `boards_save_plan` | `PUT /v1/boards/{id}/plan` |
| `boards_start` / `boards_stop` | `POST .../start` · `POST .../stop` |
| `boards_snapshot` | `GET .../snapshot` |
| `admin_list_players` | `GET /v1/admin/players` |
| `admin_create_api_key` | `POST /v1/admin/api-keys` |
| `diagnostics_recent_logs` | `GET /diagnostics/recent-logs` |

Typiskt headless-flöde: `guest_auth` → `boards_list` / `boards_create` → `boards_save_plan` → `boards_start` → upprepa `boards_snapshot`.

## MCP vs andra metoder

- **MCP:** strukturerade verktyg i Cursor, bra för iterativ playtest och subagenter.
- **WebFetch / curl:** snabba engångsanrop; samma HTTP.
- **Smoke-skript:** se `factory-game-azure-test` (`Smoke-AzureApi.ps1`).

## Driftverifiering

Repo-ägaren verifierar mot Azure enligt `.cursor/rules/factory-game-team.mdc`. Agenten kör `dotnet build` / `dotnet test` i Cursor; använd **inte** MCP som ersättning för kravgranskning i `KRAVSPEC.md`.
