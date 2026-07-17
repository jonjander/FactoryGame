---
name: factory-game-krav-arkitektur
description: >-
  Maintains and extends FactoryGame requirements and architecture docs (KRAVSPEC,
  data model, IdP, wiki/name generation). Use when editing KRAVSPEC.md, scoping MVP,
  traceability, product copy in docs, or when the user asks about game design
  vs implementation boundaries.
disable-model-invocation: true
---

# FactoryGame -- requirements and architecture

## Sources

- Primary source of truth for game design: `KRAVSPEC.md` in repo root.
- Do not change requirements without reflecting the user's decision; keep terminology consistent (seaport pool, spot, tick, keyframe).

## Working rules

1. Read relevant sections in `KRAVSPEC.md` before proposing new requirements.
2. New decisions: update `KRAVSPEC.md` in the same PR/change as code if it affects behavior.
3. MVP scope: 20 base elements, scaling to thousands; wiki and names 100% generated from data.
4. Language: user documentation in English; code and API in English unless the project says otherwise.

## Checklist for requirement changes

- [ ] Conflict with server-authoritative model or determinism?
- [ ] Affects open API / external clients?
- [ ] Need update to data model or API section?

## Delegation

- Deep simulation logic -> skill `factory-game-server-sim`.
- Exchange/seaport -> skill `factory-game-bors-seaport`.
- Web/PWA/offline -> skill `factory-game-web-klient`.
