# GUI-paritet — webklient vs MCP

Mål: en agent via MCP ska kunna göra **samma server-side handlingar** som en inloggad spelare i web-GUI (F18, F19, F20). Klient-only beteende (offline, merge, animation) är **utanför** MCP.

**Status:** alla web-GUI `/v1`-routes har MCP-verktyg (32 totalt). Se `@factory-game-mcp-server`.

## Spelarflöden

### Session och ekonomi

| Användaren i GUI | MCP |
|------------------|-----|
| Gäst / enhetsinloggning | `guest_auth` |
| OAuth (Google m.m.) | **Utanför MCP** — gäst eller API-nyckel |
| Se saldo och pool | `player_wallet`, `player_pool` |
| Pool per ämne (volym-UI) | `player_pool_view` |
| Transaktionshistorik | `player_transactions` |

### Innehåll och wiki

| GUI | MCP |
|-----|-----|
| Periodiska systemet / ämnen | `content_list_elements` |
| Wiki | `content_wiki` |
| Maskinbutik | `content_machine_store` |
| Köp maskin till lager | `player_machine_purchase` |
| Maskininventory | `player_machine_inventory` |

### Fabrik / spelplan

| GUI | MCP |
|-----|-----|
| Skapa plan | `boards_create` |
| Lista planer | `boards_list` |
| Redigera maskiner och kopplingar | `boards_save_plan` |
| Läs sparad plan | `boards_get_plan` |
| Placera från lager | `boards_place_from_stock` |
| Starta / stoppa sim | `boards_start`, `boards_stop` |
| Live fabrik (keyframes) | `boards_keyframe_latest`, `boards_keyframes` |
| Snapshot / tillstånd | `boards_snapshot` |
| Fabriksrapport / seaport | `boards_info` |
| Förhandsgranska rapport | `boards_info_preview` |

### Börs

| GUI | MCP |
|-----|-----|
| Börsöversikt | `market_summary` |
| Orderbok (global) | `market_open_orders` |
| Orderdjup per ämne | `market_element_depth` |
| Kurshistorik | `market_element_history` |
| Senaste affärer | `market_recent_trades` |
| Lägg limitorder | `market_place_order` |
| Mina ordrar | `market_orders_mine` |
| Offline-börs (F26) | **Utanför MCP** |

### Admin / diagnostik

| GUI / drift | MCP |
|-------------|-----|
| Lista spelare | `admin_list_players` |
| Skapa API-nyckel | `admin_create_api_key` |
| Felsökning efter Azure-test | `diagnostics_recent_logs` |

## Automatiserade flöden

```bash
cd tools/factorygame-mcp
npm run smoke           # Azure
npm run smoke:local     # localhost:5176 (API måste köra)
npm run playtest        # Azure E2E
npm run playtest:local  # lokal E2E
```

Referensplaner: `tools/factorygame-mcp/fixtures/plans.json`.

## Acceptanskriterier för ny funktion

1. **Krav:** F-nummer i `KRAVSPEC.md`
2. **API:** route + payload i Swagger
3. **MCP:** nytt verktyg om headless-test ska täckas
4. **Headless steg:** auth → … → assert HTTP 2xx
5. **Utanför MCP:** klient-only beteende tydligt markerat

## Referensflöden

**Minimal:** `guest_auth` → `player_wallet` → `boards_list`

**Fabrik:** `guest_auth` → `boards_create` → `boards_save_plan` → `boards_start` → `boards_keyframes` → `boards_stop`

**Maskinlager:** `content_machine_store` → `player_machine_purchase` → `boards_place_from_stock`

**Börs:** `market_summary` → `market_element_depth` → `market_place_order` → `market_orders_mine`
