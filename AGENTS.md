# FactoryGame — arbetslag (subagenter)

Det här dokumentet beskriver **roller** som motsvarar hur du kan delegera i Cursor (t.ex. med **Task** / bakgrundsagenter). Varje roll har syfte, typisk modell och en kort uppstartsprompt du kan klistra in.

## När du delegerar

1. Välj roll nedan utifrån uppgiften.
2. Ange **readonly** om ändringar inte ska göras.
3. Bifoga sökvägar: `KRAVSPEC.md`, relevanta `.cs`-filer, eller `.cursor/skills/...`.

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
- **Cursor:** `shell`.

**Uppstartsprompt:**

> Kör build och tester i `FactoryGame`-roten; rapportera fel och föreslå minimal fix.

---

## 7. Arkitektur-granskning (`review`)

- **Syfte:** End-to-end granskning (säkerhet API, determinism, ekonomi-race).
- **Cursor:** `generalPurpose`, gärna `readonly: true` första pass.

**Uppstartsprompt:**

> Granska [PR/branch] mot `KRAVSPEC.md`: server-auktoritet, transaktioner, determinism, börs offline vs ordrar på server. Leverera prioriterad lista risker + förslag.

---

## Skills-index (projekt)

| Skill | Katalog |
|-------|---------|
| Krav & arkitektur | `.cursor/skills/factory-game-krav-arkitektur/` |
| Server-sim | `.cursor/skills/factory-game-server-sim/` |
| Börs & seaport | `.cursor/skills/factory-game-bors-seaport/` |
| Webklient | `.cursor/skills/factory-game-web-klient/` |
| Azure test-API | `.cursor/skills/factory-game-azure-test/` |

Aktivera en skill explicit i chatten (t.ex. `@factory-game-server-sim`) eller nämna den i delegeringsprompten.
