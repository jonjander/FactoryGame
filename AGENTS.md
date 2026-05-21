# FactoryGame — arbetslag (subagenter)

Det här dokumentet beskriver **roller** som motsvarar hur du kan delegera i Cursor (t.ex. med **Task** / bakgrundsagenter). Varje roll har syfte, typisk modell och en kort uppstartsprompt du kan klistra in.

## När du delegerar

1. Välj roll nedan utifrån uppgiften.
2. Ange **readonly** om ändringar inte ska göras.
3. Bifoga sökvägar: `KRAVSPEC.md`, relevanta `.cs`-filer, eller `.cursor/skills/...`.

**Drift/felsökning:** Repo-ägaren verifierar i **Azure** (`https://factorygame-h5hmbzgncnazcmgu.swedencentral-01.azurewebsites.net`), inte genom lokal `dotnet run`. Be inte om localhost-körning hos användaren. Efter användarens Azure-test: hämta gärna **`GET .../diagnostics/recent-logs`** (om endpoint påslagen) enligt `.cursor/rules/factory-game-team.mdc`. **Agenten** kör `dotnet build` / `dotnet test` i Cursor. Se `@factory-game-azure-test`.

---

## 1. Krav & speldesign (`requirements`)

- **Syfte:** Hålla `KRAVSPEC.md` och spelregler konsistenta; fånga motsägelser.
- **Cursor:** `explore` (readonly) eller `generalPurpose` för doc-ändringar.
- **Skills:** `factory-game-krav-arkitektur`

**Uppstartsprompt:**

> Läs `KRAVSPEC.md`. [Beskriv beslut eller fråga.] Uppdatera endast kravdokumentation om användaren bett om det; annars lista gap och förslag.

---

## 2. Simulering & regelmotor (`simulation`)

- **Syfte:** Tick-loop, DNA/bitwise-maskiner, sorter, determinism, start/audit.
- **Cursor:** `generalPurpose` eller `shell` (tester/build).
- **Skills:** `factory-game-server-sim`

**Uppstartsprompt:**

> Implementera eller granska server-simulering enligt `KRAVSPEC.md` och skill `factory-game-server-sim`. Föreslög endast deterministiska lösningar; inga hårdkodade specialfall per grundämne-id.

---

## 3. Börs & seaport (`market`)

- **Syfte:** Orderbok, spot, pool, transaktionslogg, idempotens.
- **Cursor:** `generalPurpose`.
- **Skills:** `factory-game-bors-seaport`

**Uppstartsprompt:**

> Designa eller implementera börs/seaport-flöde enligt `KRAVSPEC.md` och `factory-game-bors-seaport`. Separera matchningsmotor från fabrik-tick.

---

## 4. Webklient & PWA (`client`)

- **Syfte:** Keyframes, offline, merge, fabrik-UI, CLI → samma API.
- **Cursor:** `generalPurpose`.
- **Skills:** `factory-game-web-klient`

**Uppstartsprompt:**

> Bygg eller granska webklient enligt `KRAVSPEC.md` och `factory-game-web-klient`. Server är alltid auktoritativ; dokumentera synk- och merge-beteende.

---

## 5. Kodbas-spaning (`scout`)

- **Syfte:** Hitta filer, mönster, referenser utan att ändra kod.
- **Cursor:** `explore`, `readonly: true`, `subagent_type: explore`.

**Uppstartsprompt:**

> Medium noggrannhet: kartlägg var [X] hanteras i repot. Returnera filvägar och korta citat; inga ändringar.

---

## 6. Build, test, CI (`build`)

- **Syfte:** `dotnet build/test`, fixa kompileringsfel efter ändringar.
- **Cursor:** `shell` eller subagent **`factory-game-tester`** för xUnit-specifikt arbete.

**Uppstartsprompt:**

> Kör build och tester i `FactoryGame`-roten **i Cursor**; rapportera fel och föreslå minimal fix. Be inte repo-ägaren köra lokalt — driftverifiering sker mot Azure enligt `.cursor/rules/factory-game-team.mdc` / `@factory-game-azure-test`. För nya eller trasiga xUnit-tester: delegera till `factory-game-tester` och `@factory-game-tester`.

---

## 6b. xUnit-tester (`tester`)

- **Syfte:** Skapa meningsfulla Domain/Api-tester, felsöka fallerande eller flaky tester, hålla testmönster konsekventa.
- **Cursor:** Subagent **`factory-game-tester`** (`.cursor/agents/`) eller `shell` + skill.
- **Skills:** `factory-game-tester` (inte samma som MCP-playtest).

**Uppstartsprompt:**

> Använd subagenten `factory-game-tester` eller `@factory-game-tester`. Läs befintliga tester under `tests/`. Skapa endast värdefulla tester; kör `dotnet test` med filter; fixa rotorsak vid failure. MCP-playtest är separat (`factory-game-playtester`).

---

## 7. Arkitektur-granskning (`review`)

- **Syfte:** End-to-end granskning (säkerhet API, determinism, ekonomi-race).
- **Cursor:** `generalPurpose`, gärna `readonly: true` första pass.

**Uppstartsprompt:**

> Granska [PR/branch] mot `KRAVSPEC.md`: server-auktoritet, transaktioner, determinism, börs offline vs ordrar på server. Leverera prioriterad lista risker + förslag.

---

## 8. Test- & utvecklingsledare (`dev-lead`)

- **Syfte:** Orkestrera lokal build → API → MCP-spelloopar → findings → fix → omstart; gradvis avancerade scenarier, balansnoteringar, backlog för stora ändringar.
- **Cursor:** Subagent **`factory-game-dev-lead`** (`.cursor/agents/`) eller `generalPurpose` med `@factory-game-dev-lead`.
- **Skills:** `factory-game-dev-lead`, `factory-game-mcp-playtest`, `factory-game-mcp-server`; delegera xUnit till `factory-game-tester`, krav-MCP till `factory-game-playtester`.

**Uppstartsprompt:**

> Kör lokal loop enligt `@factory-game-dev-lead`: `dotnet run` (http-profil), `factorygame-local`, `npm run playtest:local`. Iterera minimal fabrik → ekonomi → börs; fixa buggar direkt; dokumentera större punkter i `docs/dev-lead-backlog.md`. Håll dig till localhost (ägaren synkar Azure manuellt).

---

## 9. Headless API / MCP (`mcp-playtest`)

- **Syfte:** Köra spelflöden via Cursor MCP (`factorygame`) mot HTTP-API:t utan PWA — auth, boards, börs, plånbok, innehåll, admin/diagnostik enligt verktyg i `tools/factorygame-mcp/`.
- **Cursor:** Subagent **`factory-game-playtester`** (`.cursor/agents/`) eller `generalPurpose` med MCP aktiverat.
- **Skills:** `factory-game-mcp-server` (verktyg och begränsningar), `factory-game-mcp-playtest` (flöden), `factory-game-azure-test` (bas-URL).

**Uppstartsprompt:**

> Använd subagenten `factory-game-playtester` eller läs `.cursor/skills/factory-game-mcp-server/SKILL.md` + `KRAVSPEC.md`. Kör MCP-verktyg mot Azure; `npm run build`, `npm run smoke` och `npm run playtest` i `tools/factorygame-mcp/`. Rapportera HTTP-status, kravparitet och MCP-gap; checka inte in tokens.

---

## Skills-index (projekt)

| Skill | Katalog |
|-------|---------|
| Krav & arkitektur | `.cursor/skills/factory-game-krav-arkitektur/` |
| Server-sim | `.cursor/skills/factory-game-server-sim/` |
| Börs & seaport | `.cursor/skills/factory-game-bors-seaport/` |
| Webklient | `.cursor/skills/factory-game-web-klient/` |
| Azure test-API | `.cursor/skills/factory-game-azure-test/` |
| MCP-server (verktyg, begränsningar) | `.cursor/skills/factory-game-mcp-server/` |
| MCP headless playtest | `.cursor/skills/factory-game-mcp-playtest/` |
| xUnit-tester (Domain/Api) | `.cursor/skills/factory-game-tester/` |
| Dev-lead (lokal utvecklingsloop) | `.cursor/skills/factory-game-dev-lead/` |

## Subagenter (projekt)

| Subagent | Fil |
|----------|-----|
| Dev-lead (lokal test/utv-loop) | `.cursor/agents/factory-game-dev-lead.md` |
| Playtester (MCP, krav, GUI-paritet) | `.cursor/agents/factory-game-playtester.md` |
| Tester (xUnit, skapa & felsök) | `.cursor/agents/factory-game-tester.md` |

Aktivera en skill explicit i chatten (t.ex. `@factory-game-server-sim`) eller nämna den i delegeringsprompten.
