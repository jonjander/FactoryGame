# Local playtest log hooks

Cursor-projekthooks som samlar loggar vid **lokal speltest** så agenten kan korrelera tidsstämplar med rapporterade buggar.

## Vad som loggas

| Fil | Källa |
|-----|--------|
| `api-recent.log` | API + backend (`GET /diagnostics/recent-logs`) |
| `ui-client.log` | Blazor/webbläsare (`GET /diagnostics/client-logs`) |
| `shell.log` | `dotnet run`, curl m.m. via `afterShellExecution` |
| `agent-activity.ndjson` | Verktygsanrop + sessionhändelser |
| `snapshots.ndjson` | Metadata för varje snapshot |

Allt hamnar under **`.local-logs/sessions/<id>/`** (gitignored). Aktiv session pekas ut av `.local-logs/current-session.txt`.

## Hooks (`.cursor/hooks.json`)

- **sessionStart** — skapar sessionsmapp
- **afterShellExecution** — sparar shell-output; snapshot efter `dotnet run`/`watch`/`test`
- **postToolUse** — tidslinje för agentverktyg; snapshot efter Shell/MCP
- **stop** — sista snapshot + kort follow-up till agenten

## Manuell snapshot

När appen kör lokalt (t.ex. `dotnet run --project src/FactoryGame.Api`):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .cursor/hooks/local-playtest-log/snapshot-local-logs.ps1
```

## Konfiguration

Redigera `.cursor/hooks/local-playtest-log/config.json` om du kör andra portar:

- API (standard): `https://localhost:7145`, `http://localhost:5176`
- UI separat: `https://localhost:7048` postar klientloggar till lokal API enligt `index.html`

## Felsökning

- Hooks syns under **Cursor → Settings → Hooks** och i **Hooks** output channel.
- Om inget händer: spara om `hooks.json` eller starta om Cursor.
- Hooks **fail open** — de blockerar aldrig agenten vid fel.

## För agenten

När användaren rapporterar en bugg efter lokalt speltest:

1. Läs `.local-logs/current-session.txt` → sessionsmapp
2. Jämför användarens tid/händelse mot `agent-activity.ndjson` och `ui-client.log` / `api-recent.log`
3. Kör snapshot-skriptet om API fortfarande kör
