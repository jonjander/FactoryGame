# FactoryGame — arbetslag (subagenter)

Det här dokumentet beskriver **roller** för delegering i Cursor (Task / bakgrundsagenter). Spelet är uppdelat i **komponenter** (specialister) och **mellanchefer** (integration och arkitektur).

## Organisation

```mermaid
flowchart TB
  subgraph leads [Mellanchefer]
    INT[factory-game-integration-lead]
    ARC[factory-game-architect]
    DEV[factory-game-dev-lead]
  end
  subgraph components [Komponenter]
    REQ[factory-game-requirements]
    SIM[factory-game-simulation]
    MKT[factory-game-market]
    API[factory-game-api-platform]
    WEB[factory-game-web-client]
  end
  subgraph quality [Kvalitet]
    TST[factory-game-tester]
    MCP[factory-game-playtester]
  end
  INT --> SIM
  INT --> MKT
  INT --> API
  INT --> WEB
  INT --> REQ
  ARC --> INT
  DEV --> INT
  DEV --> MCP
  DEV --> TST
```

| Typ | Agent | När |
|-----|-------|-----|
| **Mellanchef** | `factory-game-integration-lead` | Feature går över sim/API/web/börs; DTO-paritet; “API OK men UI fel” |
| **Mellanchef** | `factory-game-architect` | Stor refactor; säkerhet/determinism/ekonomi-granskning (readonly) |
| **Mellanchef** | `factory-game-dev-lead` | Lokal utvecklingsloop, balans, backlog |
| **Komponent** | `factory-game-requirements` | KRAVSPEC, scope, terminologi |
| **Komponent** | `factory-game-simulation` | Tick, maskiner, DNA, sorter |
| **Komponent** | `factory-game-market` | Börs, pool, wallet, transaktioner |
| **Komponent** | `factory-game-api-platform` | Endpoints, EF, Infrastructure, Contracts |
| **Komponent** | `factory-game-web-client` | Blazor, canvas, offline, wiki-UI |
| **Kvalitet** | `factory-game-tester` | xUnit Domain/Api |
| **Kvalitet** | `factory-game-playtester` | MCP mot API, kravparitet |

**Drift:** Repo-ägaren verifierar i **Azure** — se `.cursor/rules/factory-game-team.mdc`. Agenten kör `dotnet build` / `dotnet test` i Cursor. `@factory-game-azure-test`.

---

## När du delegerar

1. **En komponent** → motsvarande specialist-subagent + `@factory-game-*` skill.
2. **Två eller fler lager** → `factory-game-integration-lead` först; den delegerar vidare.
3. **Stor eller riskfylld ändring** → `factory-game-architect` (readonly), sedan integration eller specialist.
4. Ange **readonly** för spaning/granskning.
5. Bifoga `KRAVSPEC.md`, relevanta `.cs`-filer, eller `.cursor/skills/...`.

---

## Komponenter (specialister)

### Krav & speldesign — `factory-game-requirements`

- **Skill:** `factory-game-krav-arkitektur`
- **Äger:** `KRAVSPEC.md`, produktgränser, acceptanskriterier
- **Prompt:** Läs `KRAVSPEC.md`. [Fråga.] Uppdatera krav endast om användaren bett om det; annars gap + vilken specialist som ska implementera.

### Simulering — `factory-game-simulation`

- **Skill:** `factory-game-server-sim`
- **Äger:** `src/FactoryGame.Domain/Simulation/`
- **Prompt:** Implementera/granska sim enligt `KRAVSPEC.md` och `@factory-game-server-sim`. Deterministiskt; inga specialfall per grundämne-id.

### Börs & seaport — `factory-game-market`

- **Skill:** `factory-game-bors-seaport`
- **Äger:** matchning, pool, marknads- och wallet-tjänster
- **Prompt:** Börs/seaport enligt `KRAVSPEC.md` och `@factory-game-bors-seaport`. Separera matchning från fabrik-tick.

### API & plattform — `factory-game-api-platform`

- **Skill:** `factory-game-api-platform`
- **Äger:** `FactoryGame.Api`, `Infrastructure`, `Contracts`
- **Prompt:** Endpoints/persistens/DTO enligt `@factory-game-api-platform`. Domänregler i Domain, inte i SQL-lager.

### Webklient — `factory-game-web-client`

- **Skill:** `factory-game-web-klient`
- **Äger:** `src/FactoryGame.Web/`
- **Prompt:** Klient enligt `KRAVSPEC.md` och `@factory-game-web-klient`. Server auktoritativ; dokumentera synk/merge.

---

## Mellanchefer

### Integration — `factory-game-integration-lead`

- **Skill:** `factory-game-integration-lead`
- **Äger:** tvärgående flöden (bräda start→keyframes, Contracts↔Web↔MCP, ekonomi helhet)
- **Prompt:** Koordinera [feature] över lager. Delegera implementation till rätt specialist; verifiera helheten med tester/MCP.

### Arkitektur — `factory-game-architect`

- **Skill:** `factory-game-architect`
- **Äger:** readonly granskning mot krav (säkerhet, determinism, ekonomi-race, offline)
- **Prompt:** Granska [ändring/PR] mot `KRAVSPEC.md`. Critical/Warning/Suggestion + rekommenderad specialist. Implementera inte utan explicit mandat.

### Utvecklingsledare (lokal loop) — `factory-game-dev-lead`

- **Skills:** `factory-game-dev-lead`, `factory-game-mcp-playtest`, `factory-game-mcp-server`
- **Prompt:** Kör lokal loop enligt `@factory-game-dev-lead`. Delegera xUnit → `factory-game-tester`, tvärgående fix → `factory-game-integration-lead`.

---

## Kvalitet & spaning

### Kodbas-spaning (`scout`)

- **Cursor:** `explore`, `readonly: true`
- **Prompt:** Kartlägg var [X] hanteras. Returnera filvägar och citat; inga ändringar.

### Build & CI (`build`)

- **Prompt:** `dotnet build/test` i Cursor. xUnit → `factory-game-tester`.

### xUnit — `factory-game-tester`

- **Skill:** `factory-game-tester`
- **Prompt:** `@factory-game-tester`. Meningsfulla tester; `dotnet test` med filter. Inte MCP-playtest.

### MCP / Azure — `factory-game-playtester`

- **Skills:** `factory-game-mcp-server`, `factory-game-mcp-playtest`, `factory-game-azure-test`
- **Prompt:** MCP mot Azure; `npm run build/smoke/playtest` i `tools/factorygame-mcp/`. Ingen token i repo.

---

## Skills-index

| Skill | Katalog |
|-------|---------|
| Krav & arkitektur | `.cursor/skills/factory-game-krav-arkitektur/` |
| Server-sim | `.cursor/skills/factory-game-server-sim/` |
| Börs & seaport | `.cursor/skills/factory-game-bors-seaport/` |
| Webklient | `.cursor/skills/factory-game-web-klient/` |
| API & plattform | `.cursor/skills/factory-game-api-platform/` |
| Integration (mellanchef) | `.cursor/skills/factory-game-integration-lead/` |
| Arkitektur (mellanchef) | `.cursor/skills/factory-game-architect/` |
| Azure test-API | `.cursor/skills/factory-game-azure-test/` |
| MCP-server | `.cursor/skills/factory-game-mcp-server/` |
| MCP playtest | `.cursor/skills/factory-game-mcp-playtest/` |
| xUnit | `.cursor/skills/factory-game-tester/` |
| Dev-lead (lokal loop) | `.cursor/skills/factory-game-dev-lead/` |

## Subagenter (projekt)

| Subagent | Fil | Typ |
|----------|-----|-----|
| Requirements | `.cursor/agents/factory-game-requirements.md` | Komponent |
| Simulation | `.cursor/agents/factory-game-simulation.md` | Komponent |
| Market | `.cursor/agents/factory-game-market.md` | Komponent |
| API platform | `.cursor/agents/factory-game-api-platform.md` | Komponent |
| Web client | `.cursor/agents/factory-game-web-client.md` | Komponent |
| Integration lead | `.cursor/agents/factory-game-integration-lead.md` | Mellanchef |
| Architect | `.cursor/agents/factory-game-architect.md` | Mellanchef |
| Dev-lead | `.cursor/agents/factory-game-dev-lead.md` | Mellanchef |
| Tester | `.cursor/agents/factory-game-tester.md` | Kvalitet |
| Playtester | `.cursor/agents/factory-game-playtester.md` | Kvalitet |

Aktivera med `@factory-game-server-sim` eller delegera med Task till `factory-game-simulation` (subagent-namn = filens `name` i frontmatter).
