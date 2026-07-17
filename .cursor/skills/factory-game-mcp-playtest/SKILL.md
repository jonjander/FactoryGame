---
name: factory-game-mcp-playtest
description: >-
  Headless FactoryGame playtesting via the factorygame MCP server: guest auth,
  boards, market, player, content, admin, diagnostics. Use when driving the
  hosted API from Cursor without the PWA, or when validating flows tool-by-tool.
  Pair with factory-game-mcp-server (tools/limits) and factory-game-azure-test.
disable-model-invocation: true
---

# FactoryGame -- MCP headless playtest

## Purpose

Run game flows via MCP (`factorygame`) against `/v1` without PWA. Technical reference: **`@factory-game-mcp-server`**.

## Quick start

1. `npm install` + `npm run build` in `tools/factorygame-mcp/`.
2. **Azure:** `npm run smoke` / `npm run playtest`
3. **Local:** start API (`dotnet run --project src/FactoryGame.Api --launch-profile http`), then `npm run smoke:local` / `npm run playtest:local`
4. Cursor: enable `factorygame-local` in MCP for local development (see `@factory-game-mcp-server`).
5. Restart Cursor after MCP/env changes.

**Fixtures:** `tools/factorygame-mcp/fixtures/plans.json` (`minimalLoop`, `liquidSeparatorFlow`).

## Typical flows

**Factory:** `guest_auth` -> `boards_create` -> `boards_save_plan` -> `boards_info_preview` -> `boards_get_plan` -> `boards_start` -> `boards_keyframes` -> `boards_info` -> `boards_stop`

**Machine inventory:** `content_machine_store` -> `player_machine_purchase` -> `player_machine_inventory` -> `boards_place_from_stock` -> `boards_get_plan`

**Economy:** `guest_auth` -> `player_wallet` -> `player_pool_view` -> `player_transactions`

**Exchange:** `market_summary` -> `market_element_depth` -> `market_place_order` -> `market_orders_mine`

**Troubleshooting:** `diagnostics_recent_logs` after Azure test.

## Delegation

Structured requirement validation: subagent **`factory-game-playtester`**.

## Operations

Repo owner verifies in Azure per `.cursor/rules/factory-game-team.mdc`. MCP does not replace requirement review in `KRAVSPEC.md`.
