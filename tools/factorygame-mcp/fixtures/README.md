# Playtest plan fixtures

Reference board plans for MCP playtests. Source: `tests/FactoryGame.Api.Tests/`.

| Key | Machines | Notes |
|-----|----------|-------|
| `minimalLoop` | SeaportConnector, Boiler | E03 (liquid) from starter pool; good for E2E smoke |
| `liquidSeparatorFlow` | SeaportConnector, LiquidSeparator | Needs `outElementId` + often market buy |

## Port names (from `MachinePortCatalog`)

| Type | In | Out |
|------|----|-----|
| Boiler | in | out |
| LiquidSeparator | in | out1, out2 |
| Destilator | in | out1, out2 |
| Mixer | in1, in2 | out |
| Heater, Cooler, Condenser, Crystallizer, Melter | in | out |
| Sorter | in | out1–out4 |
| SeaportConnector | in | out |

Purchasable types: see `GET /v1/content/machine-store` or MCP `content_machine_store`.
