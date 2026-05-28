# Dev-lead backlog (större ändringar)

Punkter som kräver separat granskning — inte blocker för minimal lokal loop.

| Prio | Ämne | Beskrivning |
|------|------|-------------|
| ~~P2~~ | ~~SQLite-lås vid långkörande API~~ | **Löst (0.3.0):** Azure SQL / SQL Server med EF migrations; SQLite kvar enbart för tester. |
| P3 | Admin list players | `GET /v1/admin/players` sorterar nu i minnet (samma SQLite DateTimeOffset-mönster). OK för dev; överväg indexerat `CreatedAtUtcTicks`-fält om listan växer. |
| P2 | Balans — tid till första intäkt | `BaseIncome` var 5:e minut (`BaseIncomeIntervalMinutes`); minimalLoop gav ingen cash-delta på ~6 s. Överväg kortare intervall i dev, tydligare UI om passiv inkomst, eller fabrik→börs-intäkt tidigare i tutorial. |
| P3 | Motspelare-börs | Två gäster med `factory-game-playtester`: köp/sälj samma element, verifiera matchning och pool. |
