# Game shell — referens

## Kärnfiler

| Fil | Roll |
|-----|------|
| `Components/ResponsiveApp.razor` | Väljer layout efter viewport |
| `Services/ViewportLayoutService.cs` | `UseGameShell`, `Changed` event |
| `wwwroot/js/viewport-layout.js` | `matchMedia (min-width: 900px)` |
| `Layout/GameShellLayout.razor` | Canvas + toolbar + fönster + dolt `@Body` |
| `Layout/MainLayout.razor` | Mobil — oförändrad top-nav |
| `Components/GameToolbar.razor` | Desktop-knappar + sim-kontroller |
| `Components/FloatingWindow.razor` | Ett flytande fönster |
| `Components/FloatingWindowHost.razor` | Renderar öppna fönster |
| `Services/GameWindowService.cs` | Fönster-register, z-index, position/storlek |
| `Services/GameShellNavigation.cs` | Route → fönster-id |
| `Services/BoardCanvasSession.cs` | Fabrik-state, API, polling, plan-JSON |
| `wwwroot/js/floating-window.js` | Pointer-drag för fönster |

## Fönster-id → View

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
| `store` | `MachineStoreView` | — |
| `machine-settings` | `MachineSettingsView` | — |
| `board-info` | `BoardInfoView` | — |
| `place-machine` | `PlaceMachineView` | — |
| `pipe-form` | `PipeFormView` | — |
| `plan-json` | `PlanJsonView` | — |

Registrering: `GameShellNavigation.EnsureRegistered()`.

## Views och mobil

| View | Mobil via |
|------|-----------|
| `HomeView` | `Pages/Home.razor` |
| `ExchangeView` | `Pages/Exchange.razor` |
| … | `Pages/<Namn>.razor` |
| `BoardsMobileView` | `Pages/Boards.razor` |

Mönster i Page:

```razor
@page "/pool"
@inject ViewportLayoutService Viewport
@if (!Viewport.UseGameShell) { <PoolView /> }
```

## FactoryCanvas i shell

Parametrar för helskärm:

- `FillViewport="true"` — wrapper/SVG fyller `game-shell-canvas-area`
- `HideHelpText="true"` — döljer hjälptext; pipe-toolbar vid valt rör behålls

## CSS (desktop)

| Klass | Syfte |
|-------|--------|
| `.game-shell` | `100vh`, flex-kolumn |
| `.fg-game-toolbar` | RCT-liknande top-bar |
| `.fg-game-btn` | Toolbar-knapp |
| `.game-shell-canvas-area` | Rutnät-bakgrund, flex 1 |
| `.fg-floating-window` | Fixed, draggable |
| `.fg-floating-window-header` | Drag-handle |
| `.fg-floating-window-resize` | Resize-hörn |
| `.fg-window-panel` | Innehåll utan `.panel`-ram i fönster |
| `.game-shell-hidden-body` | Döljer `@Body` på desktop |

Mobil: `@media (max-width: 899px)` döljer `.game-shell`, `.fg-game-toolbar`, `.fg-floating-window`.

## DI (`Program.cs`)

```csharp
builder.Services.AddSingleton<ViewportLayoutService>();
builder.Services.AddSingleton<GameWindowService>();
builder.Services.AddSingleton<BoardCanvasSession>();
builder.Services.AddSingleton<GameShellNavigation>();
```

## Vanliga misstag

- Logik i `Pages/` som bara körs på mobil — glöm inte `Views/` + fönsterregistrering för desktop.
- Ny `@bind` mot `BoardCanvasSession`-property med `private set` — kräver public setter.
- Dubbel `InitializeAsync` på session — guard med `Session.IsInitialized`.
- Zoom/pan på canvas finns **inte** ännu; kräver ny JS om det läggs till.
