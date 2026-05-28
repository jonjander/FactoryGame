# Dev-lead backlog (större ändringar)



Punkter som kräver separat granskning — inte blocker för minimal lokal loop.



| Prio | Ämne | Beskrivning |

|------|------|-------------|

| ~~P2~~ | ~~SQLite-lås vid långkörande API~~ | **Löst (0.3.0):** Azure SQL / SQL Server med EF migrations; SQLite kvar enbart för tester. |

| ~~P3~~ | ~~Admin list players~~ | **Löst:** `CreatedAtUtcTicks` på `Players` med index; `GET /v1/admin/players` sorterar i databasen (`OrderByDescending` + `Take(200)`). |

| P2 | Balans — tid till första intäkt | `BaseIncome` var 5:e minut (`BaseIncomeIntervalMinutes`); minimalLoop gav ingen cash-delta på ~6 s. Överväg kortare intervall i dev, tydligare UI om passiv inkomst, eller fabrik→börs-intäkt tidigare i tutorial. |

| ~~P3~~ | ~~Motspelare-börs~~ | **Verifierad lokalt (iter2:local):** två gäster köper/säljer element 7, P2P-matchning OK, pool uppdateras. Fix: `ExchangeService` använder `CreateExecutionStrategy()` med SQL Server retry. |

