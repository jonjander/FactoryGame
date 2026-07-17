---
name: factory-game-bors-seaport
description: >-
  Implements and reviews FactoryGame exchange and inventory — single volume pool per
  player, seaport nodes per board, spot-only trading, block buy when pool full,
  standing orders executing on server, transaction log. Use for order book, matching,
  delivery to pool, or API design for trading endpoints.
disable-model-invocation: true
---

# FactoryGame -- exchange and seaport

## Domain rules

- **One volume pool per player**; all board seaport nodes connect to the same pool.
- **Spot only:** no short selling; pool balance required for sell.
- **Full pool:** block buy (no queue, no spill).
- **Offline:** no reliable exchange UI; orders execute on server; client shows **transaction log** when online.
- **Price on junk (ash/goo):** market-driven; can be zero without demand.

## API / transactions

- All matching and balance on server; client creates/cancels orders.
- Idempotency for economic commands where needed.
- Abstract units consistent between orders, delivery, and factory.

## Checklist

- [ ] Tests for block on full pool, partial fills if supported, cancellation.
- [ ] Clear separation: exchange engine vs factory tick.
