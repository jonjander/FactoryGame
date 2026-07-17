---
name: factory-game-game-shell
description: >-
  Builds and reviews FactoryGame desktop game shell — full-viewport factory canvas,
  RCT-style toolbar, draggable/resizable floating windows, ViewportLayoutService
  (900px breakpoint), BoardCanvasSession, Views vs Pages split. Use when changing
  desktop layout, toolbar, floating windows, canvas shell, or mobile/desktop parity.
disable-model-invocation: true
---

# FactoryGame -- desktop game shell

## Quick overview

| Viewport | Layout | Navigation | Content |
|----------|--------|------------|----------|
| `< 900px` | `MainLayout` | Top-nav links | `Pages/*` -> `Views/*` directly |
| `>= 900px` | `GameShellLayout` | `GameToolbar` | Canvas + floating windows (`Views/*` in `DynamicComponent`) |

Breakpoint: `ViewportLayoutService.DesktopMinWidth` = **900px** (`matchMedia` in `wwwroot/js/viewport-layout.js`).

## Architecture (change the right layer)

```
ResponsiveApp -> ViewportLayoutService -> MainLayout | GameShellLayout
GameShellLayout -> GameToolbar + FactoryCanvas(FillViewport) + FloatingWindowHost
GameShellNavigation -> route syncs open window (URL preserved)
GameWindowService -> register/toggle/open/close/focus windows
BoardCanvasSession -> factory state (singleton; mobile + desktop share)
Pages/*.razor -> thin wrappers: @if (!Viewport.UseGameShell) { <XxxView /> }
```

**Rule:** On desktop pages mount with hidden `@Body`; content shows **only** in windows via `GameWindowService` -- do not duplicate state between page and window.

## New feature in existing view

1. Extract/extend `Views/<Name>View.razor` (markup + `@code`).
2. `Pages/<Name>.razor` keeps `@page` + mobile guard.
3. Register window in `GameShellNavigation.EnsureRegistered()` (id, title, `typeof(...View)`, size).
4. Add toolbar button in `GameToolbar.razor` if the user should reach it from shell.
5. CSS under `.game-shell` / `.fg-floating-window` in `app.css` -- **do not** change mobile layout in `MainLayout` without reason.

## Factory on desktop

- Canvas owned by `GameShellLayout`, data from `BoardCanvasSession`.
- Factory tools as separate windows: `boards-picker`, `store`, `machine-settings`, `board-info`, `place-machine`, `pipe-form`, `plan-json`.
- Sim buttons (Save/Start/Stop/Snapshot) are in the toolbar -- not in the footer.
- Last selected board: `BrowserStorage` key `fg_last_board_id`.

New factory logic -> `BoardCanvasSession.cs` first; mobile `BoardsMobileView` and shell should reuse the same service.

## Floating windows

- `FloatingWindow.razor` + `wwwroot/js/floating-window.js` (drag title bar, resize corner).
- `ConfirmDialog` / small modals: keep `fg-modal-backdrop` above windows.
- Toggle: second click on same toolbar button closes the window.

## Checklist

- [ ] Mobile `< 900px`: `MainLayout` + side nav unchanged; no game-shell CSS visible.
- [ ] Desktop: canvas fills area under toolbar; no `max-width: 1100px` on shell.
- [ ] Deep links (`/exchange`, `/boards`, ...) open the right window via `GameShellNavigation`.
- [ ] Login on desktop closes `login` window; redirect to exchange **not** automatic (difference vs mobile).
- [ ] `dotnet build` on `FactoryGame.Web` after change.

## Details

File map, window ids, and CSS classes: [reference.md](reference.md)
