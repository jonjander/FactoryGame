---
name: factory-game-dev-lead
description: >-
  Orkestrerar lokal FactoryGame utvecklingsloop: build, API på localhost:5176,
  MCP factorygame-local, playtest-iterationer, buggfix, balans, backlog för stora
  ändringar. Använd när du ska spela, utvärdera och mogenhetsutveckla spelet
  iterativt utan Azure.
disable-model-invocation: true
---

# FactoryGame — dev lead (lokal loop)

## Snabbstart

```powershell
# Terminal 1 — API (http-profil, port 5176)
dotnet run --project src/FactoryGame.Api --launch-profile http

# Terminal 2 — MCP-skript
cd tools/factorygame-mcp
npm install
npm run smoke:local
npm run playtest:local
npm run iter2:local
npm run iter3:local
```

Cursor: MCP-server **`factorygame-local`** i `.cursor/mcp.json`.

## Subagent

Delegera hela loopen till **`factory-game-dev-lead`** (`.cursor/agents/factory-game-dev-lead.md`).

## Iterationer

1. **Minimal fabrik** — `fixtures/plans.json` → `minimalLoop`
2. **Ekonomi** — wallet, pool, transactions
3. **Pengar** — börsordrar, sälj från pool
4. **Avancerat** — separator, maskinlager
5. **Motspelare** — `factory-game-playtester` med annan `deviceKey`

## Efter varje runda

- Findings-tabell (bug / balans / MCP / krav)
- Små fix → test → omstart loop
- Stora → `docs/dev-lead-backlog.md`

## Leverans

Följ `factory-game-version-and-tags` och `factory-game-git-commit-push`. Lokal loop innebär **inte** att Azure-test ersätts — ägaren synkar deploy manuellt.

## Relaterat

| Resurs | Syfte |
|--------|--------|
| `@factory-game-mcp-server` | 32 verktyg |
| `@factory-game-mcp-playtest` | Flöden |
| `factory-game-playtester` | MCP mot krav |
| `factory-game-tester` | xUnit |
