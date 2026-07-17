# GUI parity -- web client vs MCP

Goal: an agent via MCP should be able to perform **the same server-side actions** as a logged-in player in the web GUI (F18, F19, F20). Client-only behavior (offline, merge, animation) is **outside** MCP.

**Status:** all web GUI `/v1` routes have MCP tools (32 total). See `@factory-game-mcp-server`.

## Player flows

### Session and economy

| User in GUI | MCP |
|------------------|-----|
| Guest / device login | `guest_auth` |
| OAuth (Google etc.) | **Outside MCP** -- guest or API key |
| See balance and pool | `player_wallet`, `player_pool` |
| Pool per element (volume UI) | `player_pool_view` |
| Transaction history | `player_transactions` |

### Content and wiki

| GUI | MCP |
|-----|-----|
| Periodic system / elements | `content_list_elements` |
| Wiki | `content_wiki` |
| Machine store | `content_machine_store` |
| Buy machine to inventory | `player_machine_purchase` |
| Machine inventory | `player_machine_inventory` |

### Factory / board plan

| GUI | MCP |
|-----|-----|
| Create plan | `boards_create` |
| List plans | `boards_list` |
| Edit machines and connections | `boards_save_plan` |
| Read saved plan | `boards_get_plan` |
| Place from inventory | `boards_place_from_stock` |
| Start / stop sim | `boards_start`, `boards_stop` |
| Live factory (keyframes) | `boards_keyframe_latest`, `boards_keyframes` |
| Snapshot / state | `boards_snapshot` |
| Factory report / seaport | `boards_info` |
| Preview report | `boards_info_preview` |

### Exchange

| GUI | MCP |
|-----|-----|
| Exchange overview | `market_summary` |
| Order book (global) | `market_open_orders` |
| Depth per element | `market_element_depth` |
| Price history | `market_element_history` |
| Recent trades | `market_recent_trades` |
| Place limit order | `market_place_order` |
| My orders | `market_orders_mine` |
| Offline exchange (F26) | **Outside MCP** |

### Admin / diagnostics

| GUI / ops | MCP |
|-------------|-----|
| List players | `admin_list_players` |
| Create API key | `admin_create_api_key` |
| Troubleshooting after Azure test | `diagnostics_recent_logs` |

## Automated flows

```bash
cd tools/factorygame-mcp
npm run smoke           # Azure
npm run smoke:local     # localhost:5176 (API must be running)
npm run playtest        # Azure E2E
npm run playtest:local  # local E2E
```

Reference plans: `tools/factorygame-mcp/fixtures/plans.json`.

## Acceptance criteria for new functionality

1. **Requirements:** F-number in `KRAVSPEC.md`
2. **API:** route + payload in Swagger
3. **MCP:** new tool if headless test should cover it
4. **Headless steps:** auth -> ... -> assert HTTP 2xx
5. **Outside MCP:** client-only behavior clearly marked

## Reference flows

**Minimal:** `guest_auth` -> `player_wallet` -> `boards_list`

**Factory:** `guest_auth` -> `boards_create` -> `boards_save_plan` -> `boards_start` -> `boards_keyframes` -> `boards_stop`

**Machine inventory:** `content_machine_store` -> `player_machine_purchase` -> `boards_place_from_stock`

**Exchange:** `market_summary` -> `market_element_depth` -> `market_place_order` -> `market_orders_mine`
