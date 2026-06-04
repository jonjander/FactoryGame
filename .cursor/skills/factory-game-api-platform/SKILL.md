---
name: factory-game-api-platform
description: >-
  Implements and reviews FactoryGame API layer — ASP.NET Core endpoints, auth,
  EF Core persistence, Infrastructure services, Contracts DTOs, migrations, SQL
  retry/idempotency. Use for HTTP routes, DI wiring, database schema, or when
  Domain logic must be exposed without leaking persistence into Domain.
disable-model-invocation: true
---

# FactoryGame — API och plattform

## Ägarskap

| Lager | Sökväg |
|-------|--------|
| HTTP / minimal APIs | `src/FactoryGame.Api/Endpoints/` |
| Kontrakt (DTO) | `src/FactoryGame.Contracts/` |
| Persistens, tjänster | `src/FactoryGame.Infrastructure/` |
| Ren spelregel | `src/FactoryGame.Domain/` — **delegera** till `@factory-game-server-sim` |

## Principer

- Server-auktoritativ validering; inga spelkritiska beslut enbart i klienten.
- Domänlogik anropas från Infrastructure/Api — inte SQL eller HTTP i `Domain`.
- Ekonomiska kommandon: idempotens och transaktioner där krav säger det (`KRAVSPEC.md`).
- Versionerat API under `/v1`; håll DTO i sync med klient (`FactoryGame.Web`).

## Checklista

- [ ] Endpoint returnerar tydlig valideringsorsak (inte generisk 500).
- [ ] EF-migration vid schemaändring; Azure SQL retry där `ExchangeService` mönster gäller.
- [ ] Inga hemligheter i `appsettings`; använd User Secrets lokalt.
- [ ] Ändring som påverkar sim/börs/klient → pinga `@factory-game-integration-lead`.

## Delegering

- Tick, maskiner, DNA → `@factory-game-server-sim` / `factory-game-simulation`
- Orderbok, pool → `@factory-game-bors-seaport` / `factory-game-market`
- Blazor, canvas → `@factory-game-web-klient` / `factory-game-web-client`
