---
name: factory-game-dev-lead
description: >-
  Orchestrates local FactoryGame development loop: build, API on localhost:5176,
  MCP factorygame-local, playtest iterations, bugfix, balance, backlog for larger
  changes. Use when you need to play, evaluate, and mature the game iteratively
  without Azure.
disable-model-invocation: true
---

# FactoryGame -- dev lead (local loop)

## Quick start

```powershell
# Terminal 1 -- API (http profile, port 5176)
dotnet run --project src/FactoryGame.Api --launch-profile http

# Terminal 2 -- MCP scripts
cd tools/factorygame-mcp
npm install
npm run smoke:local
npm run playtest:local
npm run iter2:local
npm run iter3:local
```

Cursor: MCP server **`factorygame-local`** in `.cursor/mcp.json`.

## Subagent

Delegate the full loop to **`factory-game-dev-lead`** (`.cursor/agents/factory-game-dev-lead.md`).

## Iterations

1. **Minimal factory** -- `fixtures/plans.json` -> `minimalLoop`
2. **Economy** -- wallet, pool, transactions
3. **Money** -- exchange orders, sell from pool
4. **Advanced** -- separator, machine inventory
5. **Opponent** -- `factory-game-playtester` with another `deviceKey`

## After each round

- Findings table (bug / balance / MCP / requirements)
- Small fix -> test -> restart loop
- Large -> `docs/dev-lead-backlog.md`

## Delivery

Follow `factory-game-version-and-tags` and `factory-game-git-commit-push`. Local loop does **not** replace Azure testing -- owner syncs deploy manually.

## Related

| Resource | Purpose |
|--------|--------|
| `@factory-game-mcp-server` | 32 tools |
| `@factory-game-mcp-playtest` | Flows |
| `factory-game-playtester` | MCP against requirements |
| `factory-game-tester` | xUnit |
