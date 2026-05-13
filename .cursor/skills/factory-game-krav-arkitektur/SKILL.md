---
name: factory-game-krav-arkitektur
description: >-
  Maintains and extends FactoryGame requirements and architecture docs (KRAVSPEC,
  data model, IdP, wiki/name generation). Use when editing KRAVSPEC.md, scoping MVP,
  traceability, Swedish product copy in docs, or when the user asks about game design
  vs implementation boundaries.
disable-model-invocation: true
---

# FactoryGame — krav och arkitektur

## Källor

- Primär sanningskälla för speldesign: `KRAVSPEC.md` i repo-roten.
- Ändra inte krav utan att spegla användarens beslut; håll terminologi konsekvent (seaport-pool, spot, tick, keyframe).

## Arbetsregler

1. Läs relevanta sektioner i `KRAVSPEC.md` innan du föreslår nya krav.
2. Nya beslut: uppdatera `KRAVSPEC.md` i samma PR/ändring som kod om det påverkar beteende.
3. MVP-scope: 20 grundämnen, skalning till tusentals; wiki och namn 100 % genererade från data.
4. Språk: användarens dokumentation på svenska där det redan används; kod och API på engelska om inte projektet säger annat.

## Checklista vid kravändring

- [ ] Konflikt med server-auktoritativ modell eller determinism?
- [ ] Påverkar öppet API / externa klienter?
- [ ] Behövs uppdatering av datamodell- eller API-sektion?

## Delegering

- Djup simuleringslogik → skill `factory-game-server-sim`.
- Börs/seaport → skill `factory-game-bors-seaport`.
- Web/PWA/offline → skill `factory-game-web-klient`.
