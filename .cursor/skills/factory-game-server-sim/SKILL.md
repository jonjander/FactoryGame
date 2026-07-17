---
name: factory-game-server-sim
description: >-
  Implements and reviews FactoryGame server-side simulation — tick loop (max 5s),
  catch-up, deterministic DNA/long bitwise machine rules, sorter routing, Edit vs
  Running, economic audit on start. Use when writing or reviewing .NET simulation,
  machine transforms, or rule engine code.
disable-model-invocation: true
---

# FactoryGame -- server simulation

## Non-negotiable requirements

- **Determinism:** same input + settings + rule version => same outcome. No non-seeded randomness in the production engine.
- **Tick:** max 5 s per logical tick; under load the server may run catch-up ticks (document limits for CPU spikes).
- **Running:** machine settings are locked; changes only in Edit. On start: save state + economic audit.
- **Sorter:** base element -> port where configured; otherwise port 4. No base element on two ports 1-3 (validate on save/start).

## Implementation tips (.NET)

- Keep bitmask/DNA transformations in pure, testable domain model (no `if` per base-element id).
- Log transformation state for replay and wiki generation.
- Simulation should run independently of global sync between players (per player/shard).

## Checklist

- [ ] Unit tests for at least one machine chain + sorter edge cases (empty port, unknown element -> port 4).
- [ ] Version rule/generator packages together with saved board plan.
