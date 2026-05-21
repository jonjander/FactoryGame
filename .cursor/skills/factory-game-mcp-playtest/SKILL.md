---
name: factory-game-mcp-playtest
description: >-
  Headless FactoryGame playtesting via the factorygame MCP server: guest auth,
  boards, market, player, content, admin, diagnostics. Use when driving the
  hosted API from Cursor without the PWA, or when validating flows tool-by-tool.
  Pair with factory-game-mcp-server (tools/limits) and factory-game-azure-test.
disable-model-invocation: true
---

# FactoryGame — MCP headless playtest

## Syfte

Kör spelflöden via MCP (`factorygame`) mot `/v1` utan PWA. Teknisk referens: **`@factory-game-mcp-server`**.

## Snabbstart

1. `npm install` + `npm run build` i `tools/factorygame-mcp/`.
2. **Azure:** `npm run smoke` / `npm run playtest`
3. **Lokal:** starta API (`dotnet run --project src/FactoryGame.Api --launch-profile http`), sedan `npm run smoke:local` / `npm run playtest:local`
4. Cursor: aktivera `factorygame-local` i MCP för lokal utveckling (se `@factory-game-mcp-server`).
5. Starta om Cursor efter MCP/env-ändringar.

**Fixtures:** `tools/factorygame-mcp/fixtures/plans.json` (`minimalLoop`, `liquidSeparatorFlow`).

## Typiska flöden

**Fabrik:** `guest_auth` → `boards_create` → `boards_save_plan` → `boards_info_preview` → `boards_get_plan` → `boards_start` → `boards_keyframes` → `boards_info` → `boards_stop`

**Maskinlager:** `content_machine_store` → `player_machine_purchase` → `player_machine_inventory` → `boards_place_from_stock` → `boards_get_plan`

**Ekonomi:** `guest_auth` → `player_wallet` → `player_pool_view` → `player_transactions`

**Börs:** `market_summary` → `market_element_depth` → `market_place_order` → `market_orders_mine`

**Felsökning:** `diagnostics_recent_logs` efter Azure-test.

## Delegering

Strukturerad kravvalidering: subagent **`factory-game-playtester`**.

## Drift

Repo-ägaren verifierar i Azure enligt `.cursor/rules/factory-game-team.mdc`. MCP ersätter inte kravgranskning i `KRAVSPEC.md`.
