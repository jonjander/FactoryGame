---
name: factory-game-web-klient
description: >-
  Builds and reviews FactoryGame web client — PWA, offline edit + queued save,
  merge on conflict, keyframe sync, exchange offline state, FactoryCanvas,
  desktop game shell (canvas + floating windows, 900px breakpoint) and mobile
  MainLayout (F19 parity). Use for frontend architecture, service worker, sync UX,
  or layout changes.
disable-model-invocation: true
---

# FactoryGame — webklient och synk

## Layout: mobil vs desktop

- **Mobil (`< 900px`):** `MainLayout` + sidnav; sidor i `Pages/*` renderar `Views/*`.
- **Desktop (`≥ 900px`):** `GameShellLayout` — canvas fyller skärmen, toolbar + flytande fönster.

Detaljerad shell-arkitektur: **`@factory-game-game-shell`** (`.cursor/skills/factory-game-game-shell/`).

## Synk

- Server är sanning: hämta **keyframes** / snapshot + tick-index periodiskt.
- Lokalt: interpolera/approximera för känsla av realtid; **jamka** vid ny keyframe.
- Offline: redigera och stoppa; **sparning köas**; vid konflikt: **merge**-flöde (inte tyst kasta lokal ändring utan användarval där det är lämpligt).

## Börs-UI offline

- Visa tydligt att börsen är otillgänglig; ev. cache med varning eller dölj handel tills online (följ produktbeslut i `KRAVSPEC.md`).

## Klient vs API

- Officiell klient använder samma öppna API som tredjepart.
- OAuth för interaktiv användning; API-nycklar med scopes för skript/botar (serverstöd).

## Mobil (F19)

- Responsiv webklient: samma API och funktioner som desktop.
- På mobil: **mindre dekor**, **tydligare menyer**, touch-vänliga mål — **inte** game shell.
- Ändra inte `MainLayout` / mobil-CSS när du bara jobbar med desktop shell.

## Checklista

- [ ] Inga spelkritiska beslut enbart på klienten.
- [ ] Tydliga felmeddelanden vid avvisade kommandon (valideringsorsak från server).
- [ ] Mobil: paritet + navigering enligt F19 (ingen avkapad logik).
- [ ] Desktop shell: innehåll i `Views/`, state i services (`BoardCanvasSession`), inte duplicerat i dold `@Body`.
