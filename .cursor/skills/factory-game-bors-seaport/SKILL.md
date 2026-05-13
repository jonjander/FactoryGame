---
name: factory-game-bors-seaport
description: >-
  Implements and reviews FactoryGame exchange and inventory — single volume pool per
  player, seaport nodes per board, spot-only trading, block buy when pool full,
  standing orders executing on server, transaction log. Use for order book, matching,
  delivery to pool, or API design for trading endpoints.
disable-model-invocation: true
---

# FactoryGame — börs och seaport

## Domänregler

- **En volym-pool per spelare**; alla spelplaners seaport-noder ansluter till samma pool.
- **Spot only:** inga blankningar; saldo i poolen krävs för sälj.
- **Full pool:** blockera köp (ingen kö, ingen spill).
- **Offline:** ingen börs-UI som pålitlig; ordrar exekveras på server; klienten visar **transaktionslogg** när online.
- **Pris på skräp (aska/goo):** marknadsdrivet; kan bli noll utan efterfrågan.

## API / transaktioner

- All matchning och saldo på server; klient skapar/annullerar ordrar.
- Idempotens för ekonomiska kommandon där det behövs.
- Abstrakta enheter konsekvent mellan order, leverans och fabrik.

## Checklista

- [ ] Tester för block vid full pool, partial fills om ni stöder det, annullering.
- [ ] Tydlig separation: börsmotor vs fabrik-tick.
