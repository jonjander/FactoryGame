---
name: factory-game-web-klient
description: >-
  Builds and reviews FactoryGame web client — PWA, offline edit + queued save,
  merge on conflict, keyframe sync from server, client interpolation, exchange
  offline state, drag-connect factory UI, embedded CLI mapping to same API as GUI.
  Use for frontend architecture, service worker, or sync UX.
disable-model-invocation: true
---

# FactoryGame — webklient och synk

## Synk

- Server är sanning: hämta **keyframes** / snapshot + tick-index periodiskt.
- Lokalt: interpolera/approximera för känsla av realtid; **jamka** vid ny keyframe.
- Offline: redigera och stoppa; **sparning köas**; vid konflikt: **merge**-flöde (inte tyst kasta lokal ändring utan användarval där det är lämpligt).

## Börs-UI offline

- Visa tydligt att börsen är otillgänglig; ev. cache med varning eller dölj handel tills online (följ produktbeslut i `KRAVSPEC.md`).

## Klient vs API

- Officiell klient använder samma öppna API som tredjepart.
- OAuth för interaktiv användning; API-nycklar med scopes för skript/botar (serverstöd).

## Checklista

- [ ] Inga spelkritiska beslut enbart på klienten.
- [ ] Tydliga felmeddelanden vid avvisade kommandon (valideringsorsak från server).
