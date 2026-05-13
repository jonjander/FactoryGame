---
name: factory-game-server-sim
description: >-
  Implements and reviews FactoryGame server-side simulation — tick loop (max 5s),
  catch-up, deterministic DNA/long bitwise machine rules, sorter routing, Edit vs
  Running, economic audit on start. Use when writing or reviewing .NET simulation,
  machine transforms, or rule engine code.
disable-model-invocation: true
---

# FactoryGame — server-simulering

## Icke förhandlingsbara krav

- **Determinism:** samma input + inställningar + regelversion ⇒ samma utfall. Ingen icke-seedad slump i produktionsmotor.
- **Tick:** max 5 s per logisk tick; vid belastning får servern köra catch-up-ticks ( dokumentera gränser för CPU-spik ).
- **Running:** maskininställningar är låsta; ändringar endast i Edit. Vid start: spara state + ekonomisk granskning.
- **Sorter:** grundämne → port där det är konfigurerat; annars port 4. Inget grundämne på två portar 1–3 (validera vid save/start).

## Implementationstips (.NET)

- Håll bitmask-/DNA-transformationer i ren, testbar domänmodell (inga `if` per grundämne-id).
- Logga transformationstillstånd för replay och wiki-generering.
- Simulering ska kunna köras oberoende av global synk mellan spelare (per spelare/shard).

## Checklista

- [ ] Enhetstester för minst en maskin kedja + sorter edge cases (tom port, okänt ämne → port 4).
- [ ] Versionera regel-/generatorpaket tillsammans med sparad spelplan.
