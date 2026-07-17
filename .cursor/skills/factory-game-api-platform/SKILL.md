---
name: factory-game-api-platform
description: >-
  Implements and reviews FactoryGame API layer — ASP.NET Core endpoints, auth,
  EF Core persistence, Infrastructure services, Contracts DTOs, migrations, SQL
  retry/idempotency. Use for HTTP routes, DI wiring, database schema, or when
  Domain logic must be exposed without leaking persistence into Domain.
disable-model-invocation: true
---

# FactoryGame -- API and platform

## Ownership

| Layer | Path |
|-------|--------|
| HTTP / minimal APIs | `src/FactoryGame.Api/Endpoints/` |
| Contracts (DTO) | `src/FactoryGame.Contracts/` |
| Persistence, services | `src/FactoryGame.Infrastructure/` |
| Pure game rules | `src/FactoryGame.Domain/` -- **delegate** to `@factory-game-server-sim` |

## Principles

- Server-authoritative validation; no game-critical decisions on client only.
- Domain logic called from Infrastructure/Api -- not SQL or HTTP in `Domain`.
- Economic commands: idempotency and transactions where requirements say so (`KRAVSPEC.md`).
- Versioned API under `/v1`; keep DTO in sync with client (`FactoryGame.Web`).

## Checklist

- [ ] Endpoint returns clear validation reason (not generic 500).
- [ ] EF migration on schema change; Azure SQL retry where `ExchangeService` pattern applies.
- [ ] No secrets in `appsettings`; use User Secrets locally.
- [ ] Change affecting sim/exchange/client -> ping `@factory-game-integration-lead`.

## Delegation

- Tick, machines, DNA -> `@factory-game-server-sim` / `factory-game-simulation`
- Order book, pool -> `@factory-game-bors-seaport` / `factory-game-market`
- Blazor, canvas -> `@factory-game-web-klient` / `factory-game-web-client`
