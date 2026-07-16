---
name: factory-game-game-shell
description: >-
  Builds and reviews FactoryGame desktop game shell — full-viewport factory canvas,
  RCT-style toolbar, draggable/resizable floating windows, ViewportLayoutService
  (900px breakpoint), BoardCanvasSession, Views vs Pages split. Use when changing
  desktop layout, toolbar, floating windows, canvas shell, or mobile/desktop parity.
disable-model-invocation: true
---

# FactoryGame — desktop game shell

## Snabböversikt

| Viewport | Layout | Navigation | Innehåll |
|----------|--------|------------|----------|
| `< 900px` | `MainLayout` | Top-nav länkar | `Pages/*` → `Views/*` direkt |
| `≥ 900px` | `GameShellLayout` | `GameToolbar` | Canvas + flytande fönster (`Views/*` i `DynamicComponent`) |

Breakpoint: `ViewportLayoutService.DesktopMinWidth` = **900px** (`matchMedia` i `wwwroot/js/viewport-layout.js`).

## Arkitektur (ändra rätt lager)

```
ResponsiveApp → ViewportLayoutService → MainLayout | GameShellLayout
GameShellLayout → GameToolbar + FactoryCanvas(FillViewport) + FloatingWindowHost
GameShellNavigation → route synkar öppet fönster (URL behålls)
GameWindowService → register/toggle/open/close/focus fönster
BoardCanvasSession → fabrik-state (singleton; mobil + desktop delar)
Pages/*.razor → tunna wrappers: @if (!Viewport.UseGameShell) { <XxxView /> }
```

**Regel:** På desktop mountas sidor med dolt `@Body`; innehåll visas **endast** i fönster via `GameWindowService` — duplicera inte state mellan page och fönster.

## Ny funktion i befintlig vy

1. Extrahera/utöka `Views/<Namn>View.razor` (markup + `@code`).
2. `Pages/<Namn>.razor` behåll `@page` + mobil-guard.
3. Registrera fönster i `GameShellNavigation.EnsureRegistered()` (id, titel, `typeof(...View)`, storlek).
4. Lägg toolbar-knapp i `GameToolbar.razor` om användaren ska nå det från shell.
5. CSS under `.game-shell` / `.fg-floating-window` i `app.css` — **inte** ändra mobil-layout i `MainLayout` utan skäl.

## Fabrik på desktop

- Canvas ägs av `GameShellLayout`, data från `BoardCanvasSession`.
- Fabrik-verktyg som egna fönster: `boards-picker`, `store`, `machine-settings`, `board-info`, `place-machine`, `pipe-form`, `plan-json`.
- Sim-knappar (Spara/Start/Stopp/Snapshot) ligger i toolbar — inte i sidfot.
- Senast vald bräda: `BrowserStorage` nyckel `fg_last_board_id`.

Ny fabriklogik → `BoardCanvasSession.cs` först; mobil `BoardsMobileView` och shell ska återanvända samma service.

## Flytande fönster

- `FloatingWindow.razor` + `wwwroot/js/floating-window.js` (drag titelrad, resize hörn).
- `ConfirmDialog` / små modaler: behåll `fg-modal-backdrop` ovanpå fönster.
- Toggle: andra klick på samma toolbar-knapp stänger fönstret.

## Checklista

- [ ] Mobil `< 900px`: `MainLayout` + sidnav oförändrat; ingen game-shell-CSS synlig.
- [ ] Desktop: canvas fyller yta under toolbar; inga `max-width: 1100px` på shell.
- [ ] Deep links (`/exchange`, `/boards`, …) öppnar rätt fönster via `GameShellNavigation`.
- [ ] Inloggning på desktop stänger `login`-fönster; redirect till börs **inte** automatiskt (skillnad mot mobil).
- [ ] `dotnet build` på `FactoryGame.Web` efter ändring.

## Detaljer

Filkarta, fönster-id:n och CSS-klasser: [reference.md](reference.md)
