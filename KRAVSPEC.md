# Requirements specification - Web-based game (.NET)

## 1. Purpose and goals
This document describes requirements for a web-based game built with .NET.

Overall goals:
- Deliver a playable MVP that works in modern web browsers.
- Build a stable foundation for further development (more features, balance, multiplayer).
- Have clear quality requirements for performance, security, and operations.

## 2. Vision
The game should be easy to get into, provide clear progression, and work well on desktop and mobile web browsers without installation.

## 3. Target audience
- Primary: casual players (13+), short sessions 5-20 minutes.
- Secondary: more engaged players who want to optimize and return daily.

## 4. Scope

### In scope (MVP)
- Web-based game (frontend in browser, backend in .NET), preferably as PWA with offline support for editing (see F26).
- Account via external IdP (e.g. Google / OpenID Connect); no stored passwords in own database (see F1). **Local/guest account** may be considered early in development to skip login during first iterations (see F1).
- Basic game loop (start, play, progression, save, exit).
- Server-side game data and persistence; continuous tick simulation on server (see F25).
- Exactly **20 base elements** in MVP content; architecture and `dna` model must scale to **thousands** without rewrite.
- Simple admin/operations surface for monitoring.

### Out of scope (future)
- Advanced real-time multiplayer.
- In-game payments.
- Social features (guilds, chat, friends).
- Advanced event/season systems.
- **Maintenance/wear** of machines, energy or resource consumption per tick, and related economy (planned; not required in first delivery but data model must not close the door).
## 5. Functional requirements

### F1 - Account management and IdP
- Primary login must be via external identity provider (recommended: **Google** or another OpenID Connect provider).
- Backend must **not** store user passwords; only IdP subject, token session, and necessary profile metadata.
- Players must be able to log in and log out.
- Password reset is handled by IdP, not by the game.
- **Development/MVP phase:** optional **local or guest-based account** (e.g. device-specific key stored locally) may be used to speed iteration; must be **migratable** to IdP account later without data loss.

### F2 - Player session
- Players must be able to start a new game.
- Players must be able to continue previously saved progression.
- Players must be able to pause/exit without losing data.

### F3 - Game loop
- The game must have clear goals and rewards.
- The player must be able to take actions that affect progression.
- The game must validate actions server-side.
- The game's core loop must be: buy -> refine -> sell or refine further.

### F4 - Progression and storage
- Player progression must be saved in the database.
- Critical data (resources, unlocks, statistics) must be versioned.
- The game must handle data migration between versions.
- History of transactions and refinements must be saved for game balance and troubleshooting.
### F7 - Marketplace
- The player starts with money only; material that goes into the factory or is sold on the exchange must sit in **one volume pool per player** (**seaport** model, see F28). Material may **remain** in the pool without being immediately fed into the factory.
- Trading units on the exchange are **abstract units** (shares/volumes in game terms) mapped to balance in the seaport pool; implementation must be consistent between orders, delivery, and factory.
- The marketplace must work as a `stock exchange` where base elements are traded; **spot only** (buy/sell against **existing balance** in the pool) -- no short selling, no derivatives in MVP.
- **All exchange trading happens only on the server.** The client creates and cancels orders; matching, fills, and balance happen server-side.
- **The exchange is not available offline.** UI must clearly show **offline/unavailable state** for the exchange (no live trading, no reliable live curve); see F26.
- When offline, **standing** buy/sell orders continue to **execute on the server**; the player sees outcome via **transaction log** when online again.
- The player must be able to place buy and sell orders on base elements.
- Order matching must happen via an order book per base element.
- Last trade price must update the market price.
- Buy/sell totals and available volumes must always be verified server-side before fill.
- **Full seaport pool:** if delivery from a buy would **exceed** available volume, the **buy must be blocked** (no queue, no spill).
- On buy/sell, delivered base element must enter or leave the player's **seaport** pool.

### F12 - Trading features (exchange)
- Each base element must have ticker/symbol, current price, turnover, and history.
- The market must show order depth (best bid/ask) for each base element.
- The player must see holdings per base element (abstract units / pool balance) and average cost where relevant.
- A **transaction log** (buy, sell, fill, delivery to/from pool) must exist for the player and be used as primary feedback when the client was offline.
- Trading fee/commission must be configurable globally or per base element.
- Price chart (OHLC or equivalent) must be showable for selected period.
- The player must be able to create **standing** buy and sell orders (e.g. good-until-cancelled) that remain until filled or cancelled; execution happens on server regardless of client online status.
- Price on **low-value** or unwanted products (e.g. `ash`/`goo`) is driven by **demand** in the order book; if no demand exists, **effective price** may be **zero** -- not hardcoded, but market-driven.
### F8 - Material system (fake periodic system)
- The game must have a material library with unique material id, **generative display name** (see F29), and category.
- Material must belong to exactly one category: `solid`, `liquid`, or `gas`.
- Each material may have metadata (e.g. base price, rarity, unlock level).
- New materials must be addable without breaking existing recipes or player progression.
- All materials must be implemented as the same domain class (`Material`) with a uniform structure.
- Material properties must be computed from verifiable and computable `dna`.
- `dna` must be sufficient to reproduce material properties deterministically.
- Manually hardcoded special cases per material must be avoided in game rules.
- Each base element must be represented by a `long` code where bit fields describe the material's `dna`.
- Manipulation in machines must primarily happen with bitwise operations and mathematical transformations.
- The data model must be designed for high performance and scaling without large if/else chains.

### F13 - Material properties
- Each base element must have the following mandatory properties:
  - `explosivity` (0-100)
  - `flammability` (0-100)
  - `toxicity` (0-100)
  - `type` (`solid` / `liquid` / `gas`)
  - `boilingPoint`
  - `freezePoint` (solid point)
- Properties must be computed from `dna` and be verifiable afterward.
- Machines must be able to raise/lower at least one property per transformation.
- If a property crosses defined thresholds, the game must be able to trigger risk events (e.g. shutdown, scrap, bonus outcome).

- Suggested extra properties for deeper gameplay:
  - `corrosivity` (how much the material wears machines/pipes)
  - `reactivity` (how strongly the material reacts with other materials)
  - `stability` (how sensitive the material is to temperature or pressure change)
  - `density` (affects storage cost and transport capacity)
  - `purity` (affects market value and recipe compatibility)
  - `energyContent` (affects heat demand/production speed)

### F9 - Refinement system
- The player must be able to refine one or more input materials to output materials per recipes.
- A recipe may yield main product and at least one byproduct.
- Example: material `x` may refine to material `u` + byproduct `f`.
- The system must stop refinement if the player lacks inputs or money for process cost.
- Refinement results must be sellable directly or usable in new recipes.

### F10 - Machines and flows
- The game must include different machine types with defined inputs and outputs (ports).
- The player must be able to connect one machine's output to another machine's input.
- Each port must have type rules for which material categories it accepts: `solid`, `liquid`, `gas`.
- Flows between machines must be validated server-side so invalid connections are blocked.
- A machine must only process material present in connected inflow.
- The player must be able to sell output from a machine directly to the market or route further in the chain.
- Each machine must be able to affect one or more property fields on the material (via the material's `dna` rules).
### F11 - Standard machines (MVP)
- `Boiler` (1:1): one input, one output. Raises temperature-related properties.
- `Liquid Separator` (1:2): one input, two outputs. Splits a liquid into two fractions.
- `Destilator` (1:2): one input, two outputs. Separation based on boiling point logic.
- `Mixer` (2:1): two inputs, one output. Combines two materials into new composite output.
- `Heater` (1:1): one input, one output. Raises energy/temperature profile.
- `Cooler` (1:1): one input, one output. Lowers energy/temperature profile.
- `Sorter` (1:4): one input, four outputs. Player configures ports **1-3** via **multi-dropdown** (per port: zero or more base elements). **Port 4** is always **remainder**.
- **Routing rule:** each incoming base element is sent to **the output where that element is listed** in configuration. If the element is **not** configured on any of ports 1-3 it goes to **port 4**.
- Server must **validate** that the same base element is not selected on more than one of ports 1-3 (otherwise ambiguous configuration); invalid configuration must be blocked on save/start.
- Empty port (no selected base elements) accepts **nothing** via "match" -- only explicitly listed elements go there; everything else to port 4.
- All machines must have clear throughput and process time balanceable via config.
- `Tank` (1:1): buffer with configurable size (small/medium/large); decouples flows with different rate.
- `Junction` (1:2): one input, two outputs; distributes material alternately or weighted by downstream capacity.
- `RateLimiter` (1:1): limits flow to configured max rate.
- **Future:** `Valve` with cron schedule (not MVP; requirement TBD sim time vs UTC).

### F23 - Machine settings and tuning
- The player must **not** be able to change machine settings while the board is `Running`. Changes only in `Edit` mode.
- On transition to `Running` (**start**) current plan state (incl. settings) must be saved on server and an **economic audit** performed (affordability, costs, allowed connections).
- Each machine must have configurable settings affecting production, quality, risk, and energy cost.
- Example settings:
  - `Heater`/`Boiler`: heat power, ramp speed, target temperature
  - `Mixer`: ratio between input 1 and input 2, mix intensity, mix time
  - `Separator`/`Destilator`: cut-points, reflux/return grade, process speed
  - `Cooler`: cooling capacity, target temperature, cool time
  - `operationRatePermille`: operating speed 50/80/100 % on process machines where realistic
  - `tankSize`: small/medium/large on Tank
  - `maxRatePermille`: max flow on RateLimiter
- Heat/cool/condense happens over multiple ticks; fast operation may overshoot and reduce quality; slow operation gives finer DNA control.
- Settings must be validated server-side against allowed intervals and machine level/unlocks.
- Settings must be part of simulation and board version snapshot.
- Small setting changes must be able to produce large output differences (high skill ceiling).
- Unfavorable settings must be able to yield inefficient output, fallback material (`ash`/`goo`), or operational problems.

### F24 - Rarity and difficulty
- Unique/high-value base elements must be intentionally **hard** to produce; the game should otherwise be **fairly unforgiving** (wrong tuning or chain must punish clearly).
- Outcomes must be **100% deterministic**: same `dna`, same machine settings, same rule version, and same chain order gives **exactly the same** result. **No** `Random` affecting outcome; if randomness is ever introduced it must be **seeded** and stored so simulation can be **replayed** bit-for-bit.
- Failed or degraded production (e.g. `ash`/`goo`) must depend on **rules and tolerances** (incl. tuning outside valid window), not invisible chance.
- Failed process must give clear feedback (rule break, tolerance miss, instability) and correct fallback output.
- Balance must ensure the market periodically has real shortage of certain sought-after base elements.
### F14 - Board plans and factory lines
- A player must be able to have one or more board plans.
- Each board plan must have a **seaport** as hub for connection to the exchange and shared storage between plans (see F28).
- Each board plan must contain 0-x production lines with machines, connections, and flows.
- The board plan must be in exactly one of modes: `Edit` or `Running`.
- In `Edit` mode the board plan must not generate production or money.
- In `Running` mode the board plan must be read-only for the client.
- The player must be able to switch between `Edit` and `Running` via server-validated commands.

### F15 - Building and saving board plans
- The client must send build changes as transactional commands (e.g. place machine, remove connection).
- The server must be authoritative and decide if the change is valid.
- On saving a board plan the server must verify the player can afford selected machines and changes.
- If verification fails, save must be rejected without partially applied changes.
- Approved save must yield a new version snapshot of the board plan.
- **Offline:** saves requiring server verification must be **queued** and sent when the client is online again. On **conflict** between local and server truth the client must offer **merge** flow (e.g. choose which version wins, or compare differences) instead of silently discarding local change without user decision.

### F16 - Server-authoritative simulation
- Calculation of production, material flows, and economy must happen on the server.
- The client may only present state and propose actions; the client must not set final game state.
- All game-critical operations (buy/sell, building, mode change) must be transactional and atomic.

### F25 - Tick, sync, and client presentation
- The server must run simulation **continuously** in **ticks** with **maximum tick length 5 seconds** (configurable downward, never longer than 5s in requirements).
- Under **load or lag** the server may **slip** and then **catch up** by running multiple ticks in sequence until simulation is in sync; tick size per step must still respect max 5s per logical tick (or documented catch-up policy so CPU spikes are limited).
- **Synchronization between players** need **not** be global: tick schedule must **scale** (e.g. per player, per shard, or per world) -- requirement is predictable performance, not that all share the same global clock.
- The client must periodically fetch **keyframes** (or equivalent snapshot + tick index) from the server as source of truth.
- The client may **interpolate** and locally **approximate** flow/animation for more realtime feel; on new keyframe the client must **reconcile** to server state.
- Official client must be deliverable as **PWA** (see F26).

### F26 - PWA and offline behavior
- The web client must be buildable as **PWA** with cache of shell and latest known rules/wiki generation.
- **Offline** the player may **stop** factory (if local state) and **edit** plan; **start** of `Running` and exchange normally require **online**.
- **Exchange:** when connection is missing the exchange must show as **offline/unavailable** (no trading, no reliable live data); optional cached price may show with clear **warning** that it is stale -- or exchange panel hidden until online (product decision).
- Offline changes must sync via queued commands when connection returns; server validates; conflicts handled with **merge** where appropriate (see F15).

### F28 - Seaport (one volume pool per player)
- There is **exactly one volume pool per player** for physical base elements that go **into the factory** or are **sold on the exchange**. Material may **be stored** in the pool without immediate use.
- Each board plan has a **seaport node** as connection point to the **same** pool (not separate storage per plan). Transfer between plans is implicit: material exists in the pool and can be fed into chosen plan lines via respective seaport.
- The pool must have **max volume** (hard limit). **Buy from exchange** that would **exceed** volume must be **blocked**.
- The seaport node must have **max number of in and out ports** (configurable per unlock or globally).
- Seaport **outputs** must be configurable so one or more outputs can **emit** one or more selected base elements (flow rules server-validated).
- Buy from exchange delivers to pool; sell draws from pool per balance, volume, and spot rules.

### F29 - Generative names and wiki (100% generated)
- **Wiki** must be **100% generated** from the same data as the server (rules, machines, property ranges, fallback); no manual wiki pages as source of truth.
- **Display names** for base elements must **not** be hardcoded per id; they must be **derived** from `dna`/properties via a **name generator** (fictional chemical morphemes per property dimension, e.g. compositions like `TyBoSodioum`, `BiKarbonitSulfat`).
- For a given `dna` and fixed name generator version the display name must be **stable** (same name forever for that combination); on future generator change a **migration rule** must exist (e.g. store `displayNameVersion` per material).
- **Internationalization:** morpheme tables / word lists must be **localizable per language** (sv, en, ...) so generated names and wiki text follow user locale, without hardcoding a name list per base element.
- When scaling to tens of thousands of base elements names must remain **unique** within language/region (or on collision: deterministic suffix/rule) without manual name list.
- Name generator version changes must be versioned together with `dna` interpretation for reproducibility.
### F18 - Open client API
- The client API must be open and documented so third-party developers can build own clients.
- API contracts (request/response/event) must be stable and versioned.
- **Authentication:** interactive clients use **OAuth2/OIDC** (e.g. Google). **API keys** (or equivalent machine-to-machine tokens) with limited **scopes** must be supported for scripts, bots, and integrations -- same API as web, separate permission model.
- Rate limits must distinguish interactive users and key-based clients.
- Official web client must use the same public API as third-party clients.
- A sandbox/test mode must exist for client development without risking production data.

### F17 - Base income
- All players must have base income assigned periodically.
- Base income must be **minimal**: only enough that the player **cannot get completely stuck**; the game should otherwise be **tight** economically.
- Base income must be balanceable via configuration and combined with factory income.

### F19 - Web client (simple factory builder)
- An official web client must exist focused on simplicity.
- The player must be able to drag connections visually between ports (e.g. drag pipes from machine `x` to `y`).
- Build mode must show clear validation of allowed/disallowed connections in realtime.
- The client must have shortcuts for common build operations (place, connect, disconnect, rotate, save).
- **Mobile adaptation:** layout and interaction must be **responsive** and usable on **mobile web browser** (viewport meta, reasonable touch targets, scroll and single column where space requires; drag-connect may have mobile alternative, e.g. pick-source-pick-target, without giving up server validation).
- **100% client parity:** same **functionality** as on large screen -- no cut-down game logic in web; everything doable in official client must be reachable via same **public API** and corresponding UI flows (different **presentation** allowed).
- **Visual priority on small screens:** **less** decorative graphics and heavy visual effects; **clearer** hierarchy with **descriptive** menus and submenus (headings, short explanations where helpful) so functions are **easy to find** without the player losing orientation.
- **UX goal:** simple basic layout, intuitive navigation between main areas (factory, economy/exchange, inventory, wiki, CLI per MVP scope), without duplicating or hiding critical information.

### F20 - Built-in CLI in web client
- The web client must offer built-in command mode/terminal for advanced players.
- CLI commands must map to the same server commands the GUI uses.
- Minimum commands in MVP:
  - `alias <machine-id> <alias-name>`
  - `connect <source> <out-port> to <target> <in-port>`
  - `disconnect <source> <out-port> from <target> <in-port>`
  - `start`
  - `stop`
  - `save`
- CLI must give clear error messages with validation reason when command is rejected by server.

### F21 - Rule engine for machines
- Each machine must have a declarative set of rules for allowed inputs and transformation logic.
- Rules must express requirements on properties, temperature ranges, phase (`solid`/`liquid`/`gas`), and compatibility between multiple input materials.
- Rule evaluation must be deterministic and executable with bitmasks/mathematical operators.
- Rules must be data- and config-driven so new machines/materials can be added without core engine code change.
- If input does not satisfy machine rules, output must become worthless fallback material (e.g. `ash` or `goo`).
- Fallback output must be explicitly marked in state, log, and UI so the player understands why production failed.

### F22 - Machine wiki and transparency
- The game must include built-in wiki describing each machine and its rules.
- Wiki page per machine must show:
  - requirements on input materials and properties
  - temperature and phase conditions
  - expected output on valid process
  - fallback output (`ash`/`goo`) on broken rules
- Wiki content must be **100% generated** from rule data (see F29); same payload must be usable by external clients.
- Wiki pages must be accessible via web client UI and searchable for the player.

### F5 - UI/UX
- The interface must be responsive for desktop and mobile.
- The game must show clear feedback on player actions.
- The game must have basic accessibility (contrast, keyboard support where relevant).

### F6 - Operations and support
- The system must log important events (errors, login, game-critical actions).
- Admin must be able to see basic status (uptime, errors, activity).

## 6. Non-functional requirements

### NF1 - Performance
- Page must load initial view within 3 seconds on normal connection.
- Backend API responses for common actions must normally be under 300 ms.

### NF2 - Scalability
- The solution must handle at least 1,000 concurrent players in MVP environment.
- Architecture must allow horizontal scaling of backend.

### NF3 - Security
- All traffic must use HTTPS.
- Authentication must primarily be via **OAuth2/OIDC** (e.g. Google); tokens must be validated server-side; **no** primary passwords stored in app database.
- API must be protected with authentication and authorization.
- Known OWASP risks must be handled in design and implementation.
- Client data must be treated as untrusted; server must always reconstruct and verify consequences of all commands.

### NF4 - Reliability
- Daily backups of game data.
- RPO <= 24 hours, RTO <= 4 hours for MVP.
- Errors must be handled without data corruption.
- Transactions for trading and building must give ACID-like guarantees (atomic commit or rollback).

### NF5 - Maintainability
- Code standard and formatting must be consistent.
- CI with build + tests must be required before release.
- Game rule logic must be testable in isolated units.
- Public API must have machine-readable specification (e.g. OpenAPI) and version history.
- Machine rule definitions must be versionable separately from application code.

### NF6 - Balance and economy
- All price and recipe changes must be configurable without code deploy (e.g. via database or config file).
- Game economy must be simulatable offline with test data to detect inflation/exploits.
- All economic events must be idempotent at API level to avoid duplicate buy/sell.
- Matching engine must be deterministic and give same fills for same order sequence.
- Market manipulation via obvious exploit patterns (e.g. self-trading in loop) must be detectable.
- Rarity of unique base elements must be steerable via configurable balance parameters.

### NF7 - Simulation and determinism
- Same machine chain, same input, same machine settings, and same rule version must give same output (deterministic result) within same game version.
- Simulation must run in ticks/steps on server for consistent game state; tick length **<= 5s**.
- State transitions in machines (idle, processing, blocked) must be logged for troubleshooting.
- Computation of material properties from `dna` must be deterministic and version-controlled.
- Material properties must be verifiable afterward via `dna` + transformation log.
- Property thresholds and risk event rules must be configurable and versioned.
- Bitwise and mathematical execution must give same result regardless of client platform.
- **No** non-deterministic randomness in production engine; any future noise must be **seeded** and logged for replay.

### NF8 - Scaling base elements
- MVP must ship with **20** base elements in the game world.
- Data model and game rules must be designed so number of base elements can grow to **thousands** without rewrite of core logic.
- New base elements and machine rules must be introducible via content updates.

### NF9 - API compatibility
- Breaking changes in public client API must be avoided within same major version.
- Deprecation policy must exist with transition period before removal of endpoints/fields.
- Official client and CLI must be tested in CI against same versioned API contract.

### NF10 - Rule performance
- Rule evaluation per machine step must happen without linear scan of hardcoded if conditions.
- Bitmask-based rule engine must handle large number of materials and machines with predictable latency.

### NF11 - Tuning, observability, and fairness
- Effect of each machine setting must be mathematically transparent and testable.
- Simulation must log which settings contributed to outcome (successful process, failed process, fallback).
- Difficulty should feel high but fair; the player must be able to read in wiki and logs how outcome arose.
- Balance changes to machine settings must be versioned for reproducibility between patches.
## 7. Technical requirements (.NET)

### Recommended stack
- Backend: ASP.NET Core on **.NET 10** (team target framework; follow Microsoft support cycle).
- Frontend: Blazor Web App or separate SPA (e.g. React) with .NET API.
- Persistence: **Entity Framework Core** with **SQL Server** (Azure SQL) in production and local dev; **SQLite** in-memory for integration tests and Docker default.
- Operations: Azure SQL with managed identity (`Authentication=Active Directory Default`); schema via EF Core migrations at startup.
- **Development phase:** local SQL Server via `appsettings.Local.json`; SQLite file/import (snapshot) may remain for easier seed but is not primary operations anymore.
- Cache (optional in MVP): Redis.

### Architecture principles
- Clear separation between domain logic, application logic, and infrastructure.
- API-first design with versioning.
- Server-side validation of all game-critical logic.

## 8. Data requirements
- Player profile, progression, inventory/resources, history, and statistics must be stored.
- Audit log for critical events must exist.
- Personal data must be minimized and handled per GDPR.
- Data model must include material catalog, recipe catalog, market prices, and transaction log.
- Data model must include `material_dna`, property computation/version, and machine transformation log.
- Data model must include order book, trade fills, price history, and player holdings per base element.
- Data model must include board plan, plan versions, machine placements, connections, and line state.
- Data model must include alias per board plan (for CLI), plus command history for audit/debug.
- Data model must include rule definitions per machine, rule version, and fallback rules (`ash`/`goo`).
- Data model must include machine settings per instance plus tuning history for analysis and replay.
- Data model must include **seaport** configuration (volume, port limits, output mapping), shared storage id for player.
- Data model must include **simulation tick** metadata and **keyframes**/snapshot version for client sync.
- Data model must include **name generator** version and parameters for wiki/names.
- Data model must include **transaction log** for exchange (orders, fills, deliveries to/from volume pool) with timestamp and idempotency keys where appropriate.

## 9. Quality requirements and testing

### Test levels
- Unit tests for domain logic.
- Integration tests for API + database.
- Simple end-to-end test for main MVP flows.

### Acceptance criteria (MVP)
- New player can register account and play within 2 minutes.
- Game state is saved and can be resumed without data loss.
- At least 95% of critical API calls succeed under normal load.

## 10. Deliverables
- Requirements specification (this document).
- Architecture document (separate).
- Backlog with user stories and prioritization.
- MVP release with operations instructions.

## 11. Open questions (to decide)
- Should MVP be single-player or simple async multiplayer?
- Should frontend be built in Blazor or separate JS framework?
- Which cloud platform for hosting?
- Transport for keyframes: polling, SSE, or WebSockets/SignalR?
- Should market prices be pure order book prices or is there reference/control price from server?
- Should recipes/periodic events rotate or is everything static until new content patch?
- Which OIDC provider besides Google (if any) should be supported in MVP?

## 12. Game mechanics - detailed MVP loop

1. Start state:
- New player gets starting capital in money.
- Inventory is empty of material.

2. Purchase:
- Player buys shares in base elements on the exchange via orders.
- Material belongs to one of categories `solid`, `liquid`, `gas`.

3. Refinement:
- Player chooses a recipe and refines to new product.
- Recipe may yield main product + byproducts.
- Refinement happens in machines connected via inputs and outputs.
- Each machine modifies material properties per verifiable `dna` rules.

4. Decision point:
- Player chooses to sell output on market for money, or
- use output as input in next refinement step.

5. Progression:
- Profit is reinvested in new materials and recipes.
- Player gradually builds more profitable refinement chains.
- Player creativity in factory design must be the primary path to better output and higher value.
- Base income provides stable foundation, while efficient factory design determines the large upside.

## 13. Next steps
1. Decide game type and core gameplay loop.
2. Break down requirements into epics and user stories.
3. Define MVP scope (Must/Should/Could/Won't).
4. Produce technical architecture sketch.
5. Start implementation of base project (.NET + frontend + CI).