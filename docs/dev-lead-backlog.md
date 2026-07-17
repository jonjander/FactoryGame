# Dev-lead backlog (larger changes)

Items that need separate review -- not blockers for the minimal local loop.

| Prio | Topic | Description |
|------|-------|-------------|
| ~~P2~~ | ~~SQLite lock on long-running API~~ | **Resolved (0.3.0):** Azure SQL / SQL Server with EF migrations; SQLite remains only for tests. |
| ~~P3~~ | ~~Admin list players~~ | **Resolved:** `CreatedAtUtcTicks` on `Players` with index; `GET /v1/admin/players` sorts in the database (`OrderByDescending` + `Take(200)`). |
| P2 | Balance -- time to first income | `BaseIncome` every 5 minutes (`BaseIncomeIntervalMinutes`); minimalLoop gave no cash delta in ~6 s. Consider shorter interval in dev, clearer UI for passive income, or factory->exchange income earlier in the tutorial. |
| P3 | Server `BoardSummary` vs muted warnings | The client adjusts hint/badge for the **selected** board; list API (`Health`/`StatusHint`) does not know browser mutes. Possible server-side mute or synced preview. |
| ~~P3~~ | ~~Opponent exchange~~ | **Verified locally (iter2:local):** two guests buy/sell element 7, P2P matching OK, pool updates. Fix: `ExchangeService` uses `CreateExecutionStrategy()` with SQL Server retry. |
