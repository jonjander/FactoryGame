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

# FactoryGame -- web client and sync

## Layout: mobile vs desktop

- **Mobile (`< 900px`):** `MainLayout` + side nav; pages in `Pages/*` render `Views/*`.
- **Desktop (`>= 900px`):** `GameShellLayout` -- canvas fills screen, toolbar + floating windows.

Detailed shell architecture: **`@factory-game-game-shell`** (`.cursor/skills/factory-game-game-shell/`).

## Sync

- Server is truth: fetch **keyframes** / snapshot + tick index periodically.
- Locally: interpolate/approximate for realtime feel; **reconcile** on new keyframe.
- Offline: edit and stop; **save is queued**; on conflict: **merge** flow (do not silently discard local change without user choice where appropriate).

## Exchange UI offline

- Show clearly that the exchange is unavailable; optional cache with warning or hide trading until online (follow product decision in `KRAVSPEC.md`).

## Client vs API

- Official client uses the same open API as third parties.
- OAuth for interactive use; API keys with scopes for scripts/bots (server support).

## Mobile (F19)

- Responsive web client: same API and features as desktop.
- On mobile: **less decoration**, **clearer menus**, touch-friendly targets -- **not** game shell.
- Do not change `MainLayout` / mobile CSS when you only work on desktop shell.

## Checklist

- [ ] No game-critical decisions on client only.
- [ ] Clear error messages on rejected commands (validation reason from server).
- [ ] Mobile: parity + navigation per F19 (no cut-down logic).
- [ ] Desktop shell: content in `Views/`, state in services (`BoardCanvasSession`), not duplicated in hidden `@Body`.
