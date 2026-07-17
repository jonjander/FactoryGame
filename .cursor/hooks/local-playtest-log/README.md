# Local playtest log hooks

Cursor project hooks that collect logs during **local playtesting** so the agent can correlate timestamps with reported bugs.

## What is logged

| File | Source |
|-----|--------|
| `api-recent.log` | API + backend (`GET /diagnostics/recent-logs`) |
| `ui-client.log` | Blazor/browser (`GET /diagnostics/client-logs`) |
| `shell.log` | `dotnet run`, curl, etc. via `afterShellExecution` |
| `agent-activity.ndjson` | Tool calls + session events |
| `snapshots.ndjson` | Metadata for each snapshot |

Everything goes under **`.local-logs/sessions/<id>/`** (gitignored). The active session is pointed to by `.local-logs/current-session.txt`.

## Hooks (`.cursor/hooks.json`)

- **sessionStart** -- creates session folder
- **afterShellExecution** -- saves shell output; snapshot after `dotnet run`/`watch`/`test`
- **postToolUse** -- timeline for agent tools; snapshot after Shell/MCP
- **stop** -- final snapshot + short follow-up to the agent

## Manual snapshot

When the app runs locally (e.g. `dotnet run --project src/FactoryGame.Api`):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .cursor/hooks/local-playtest-log/snapshot-local-logs.ps1
```

## Configuration

Edit `.cursor/hooks/local-playtest-log/config.json` if you use other ports:

- API (default): `https://localhost:7145`, `http://localhost:5176`
- UI separate: `https://localhost:7048` posts client logs to local API per `index.html`

## Troubleshooting

- Hooks appear under **Cursor -> Settings -> Hooks** and in the **Hooks** output channel.
- If nothing happens: re-save `hooks.json` or restart Cursor.
- Hooks **fail open** -- they never block the agent on error.

## For the agent

When the user reports a bug after local playtesting:

1. Read `.local-logs/current-session.txt` -> session folder
2. Compare the user's time/event against `agent-activity.ndjson` and `ui-client.log` / `api-recent.log`
3. Run the snapshot script if the API is still running
