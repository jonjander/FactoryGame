# Game shell -- reference

## Core files

| File | Role |
|-----|------|
| `Components/ResponsiveApp.razor` | Chooses layout by viewport |
| `Services/ViewportLayoutService.cs` | `UseGameShell`, `Changed` event |
| `wwwroot/js/viewport-layout.js` | `matchMedia (min-width: 900px)` |
| `Layout/GameShellLayout.razor` | Canvas + toolbar + windows + hidden `@Body` |
| `Layout/MainLayout.razor` | Mobile -- unchanged top nav |
| `Components/GameToolbar.razor` | Desktop buttons + sim controls |
| `Components/FloatingWindow.razor` | One floating window |
| `Components/FloatingWindowHost.razor` | Renders open windows |
| `Services/GameWindowService.cs` | Window registry, z-index, position/size |
| `Services/GameShellNavigation.cs` | Route -> window id |
| `Services/BoardCanvasSession.cs` | Factory state, API, polling, plan JSON |
| `wwwroot/js/floating-window.js` | Pointer drag for windows |

## Window id -> View

| Id | View | Route |
|----|------|-------|
| `login` | `HomeView` | `/` |
| `exchange` | `ExchangeView` | `/exchange` |
| `market` | `MarketView` | `/market` |
| `pool` | `PoolView` | `/pool` |
| `transactions` | `TransactionsView` | `/transactions` |
| `wiki` | `WikiView` | `/wiki` |
| `cli` | `CliView` | `/cli` |
| `admin` | `AdminView` | `/admin` |
| `boards-picker` | `BoardPickerView` | `/boards` |
| `store` | `MachineStoreView` | -- |
| `machine-settings` | `MachineSettingsView` | -- |
| `board-info` | `BoardInfoView` | -- |
| `place-machine` | `PlaceMachineView` | -- |
| `pipe-form` | `PipeFormView` | -- |
| `plan-json` | `PlanJsonView` | -- |

Registration: `GameShellNavigation.EnsureRegistered()`.

## Views and mobile

| View | Mobile via |
|------|-----------|
| `HomeView` | `Pages/Home.razor` |
| `ExchangeView` | `Pages/Exchange.razor` |
| ... | `Pages/<Name>.razor` |
| `BoardsMobileView` | `Pages/Boards.razor` |

Pattern in Page:

```razor
@page "/pool"
@inject ViewportLayoutService Viewport
@if (!Viewport.UseGameShell) { <PoolView /> }
```

## FactoryCanvas in shell

Parameters for fullscreen:

- `FillViewport="true"` -- wrapper/SVG fills `game-shell-canvas-area`
- `HideHelpText="true"` -- hides help text; pipe toolbar on selected pipe remains

## CSS (desktop)

| Class | Purpose |
|-------|--------|
| `.game-shell` | `100vh`, flex column |
| `.fg-game-toolbar` | RCT-like top bar |
| `.fg-game-btn` | Toolbar button |
| `.game-shell-canvas-area` | Grid background, flex 1 |
| `.fg-floating-window` | Fixed, draggable |
| `.fg-floating-window-header` | Drag handle |
| `.fg-floating-window-resize` | Resize corner |
| `.fg-window-panel` | Content without `.panel` frame in window |
| `.game-shell-hidden-body` | Hides `@Body` on desktop |

Mobile: `@media (max-width: 899px)` hides `.game-shell`, `.fg-game-toolbar`, `.fg-floating-window`.

## DI (`Program.cs`)

```csharp
builder.Services.AddSingleton<ViewportLayoutService>();
builder.Services.AddSingleton<GameWindowService>();
builder.Services.AddSingleton<BoardCanvasSession>();
builder.Services.AddSingleton<GameShellNavigation>();
```

## Common mistakes

- Logic in `Pages/` that only runs on mobile -- do not forget `Views/` + window registration for desktop.
- New `@bind` to `BoardCanvasSession` property with `private set` -- requires public setter.
- Double `InitializeAsync` on session -- guard with `Session.IsInitialized`.
- Zoom/pan on canvas **not** implemented yet; needs new JS if added.
