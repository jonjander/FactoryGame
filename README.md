# FactoryGame (MVP)

Server-authoritativ fabriksimulator med börs, seaport-pool, DNA-baserade grundämnen och Blazor WebAssembly-klient (PWA).

## Krav

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Docker (för PostgreSQL och integrationstester; **inte** krav för `dotnet run` på API med standardinställningar)

## Databas (EF Core)

- **Standard:** `ConnectionStrings:DefaultConnection` är **tom** i `appsettings.json` → API startar med **SQLite in-memory** (`Data Source=:memory:;Cache=Shared`), **utan Docker** och **utan Postgres**. Schema skapas med `EnsureCreatedAsync()` (befintliga migrationer är Npgsql-specifika).
- **Postgres:** sätt `ConnectionStrings__DefaultConnection` till en Npgsql-sträng (t.ex. via `docker compose` eller miljövariabel). Vid start körs då `MigrateAsync()` som tidigare.
- **SQLite fil:** anslutningssträng som börjar med `Data Source=` eller `Filename=` → `UseSqlite` + `EnsureCreatedAsync()`.
- **Kravbild** (snapshot från webb m.m.): se `KRAVSPEC.md`.

## Snabbstart (API utan databas-server)

```bash
dotnet run --project src/FactoryGame.Api
```

Swagger: `https://localhost:7145/swagger` (profiler i `launchSettings.json`).

Hälsa: `GET /health`

## Snabbstart (API + PostgreSQL)

```bash
docker compose up -d
dotnet run --project src/FactoryGame.Api
```

Med Postgres kör API `Database.MigrateAsync()` vid start. För manuella migrationer:  
`dotnet ef database update --project src/FactoryGame.Infrastructure --startup-project src/FactoryGame.Api`  
(design-time factory förväntar Postgres om `ConnectionStrings__DefaultConnection` inte pekar på SQLite.)

## Webbklient (Blazor WASM)

```bash
dotnet run --project src/FactoryGame.Web
```

Standard-API mot `http://localhost:5176`. Ändra `src/FactoryGame.Web/wwwroot/factory-config.json` om API körs på annan bas-URL.

CORS: Development använder `Cors:Origins` i `appsettings.Development.json` på API-projektet.

## Autentisering

- **Gäst:** `POST /v1/auth/guest` med JSON `{ "deviceKey": "valfri-sträng" }`, sedan `Authorization: Bearer <token>`.
- **API-nyckel:** `X-Api-Key: <plaintext>` (hash lagras i databasen). Skapa nyckel med admin-bootstrap nedan.

## Admin (bootstrap)

Sätt `Admin:BootstrapToken` (t.ex. i `appsettings.Development.json`). Anropa:

- `GET /v1/admin/players` med header `X-Admin-Token: <token>`
- `POST /v1/admin/api-keys` med samma header och body `{ "playerId": "...", "name": "bot", "scopes": "market,boards" }`  
  Svaret innehåller `key` en gång.

## Miljövariabler (API)

| Variabel | Beskrivning |
|----------|-------------|
| `ConnectionStrings__DefaultConnection` | **Tom** → SQLite in-memory. Annars Npgsql-sträng, eller SQLite (`Data Source=...`) |
| `ASPNETCORE_ENVIRONMENT` | `Development` / `Production` |
| `Cors__Origins__0` | (valfritt) tillåten klient-URL |

## Tester

```bash
dotnet test FactoryGame.sln
```

Integrationstester (`FactoryGame.Api.Tests`) startar Postgres via Testcontainers och kräver Docker.

## Azure Web App (API)

Repot innehåller workflow [`.github/workflows/azure-webapp-api.yml`](.github/workflows/azure-webapp-api.yml) som bygger och publicerar **endast** `FactoryGame.Api` till en Linux Web App (.NET 8).

**GitHub (Actions → secrets/variables):**

| Typ | Namn | Innehåll |
|-----|------|----------|
| Secret | `AZURE_WEBAPP_PUBLISH_PROFILE` | Innehållet i nedladdad *Publish profile* från Azure Portal (Web App → **Get publish profile**) |
| Variable | `AZURE_WEBAPP_NAME` | Web App-resursens namn (t.ex. `factorygame-api`) |

Om variabeln `AZURE_WEBAPP_NAME` saknas hoppar workflow över deploy-steget (bygget körs ändå).

**”No deployments found” i Azure (Deployment Center → Loggar):** det är **normalt** om du deployar med en **egen** GitHub Actions-workflow i repot (som denna). Den vyn visar ofta bara deployment-historik som Azure **själv** skapat via Deployment Center-bygge (Oryx m.m.), inte zip-deploy från GitHub-runners. **Sanning:** **GitHub → Actions** → workflow *Deploy API to Azure Web App* (grön körning + steget *Deploy* kört). Zip-deploy brukar ändå registreras hos Kudu: öppna i webbläsare `https://<appnamn>.scm.azurewebsites.net/api/deployments` (byt `<appnamn>` mot Web App-namnet) — där syns poster om paketet verkligen nått appen.

**Tom Log stream i Azure efter Sync:** (1) Öppna **GitHub → Actions** och senaste körningen av *Deploy API to Azure Web App* — bygget/deploy loggas där. Om steget **Deploy** är grått/hoppat saknas `AZURE_WEBAPP_NAME` eller så matchar inte publish profile. (2) Web App-namnet i variabeln ska vara **exakt** resursnamnet (hostname utan `.azurewebsites.net`, t.ex. `factorygame-h5hmbzgncnazcmgu`). (3) **Log stream** visar främst **körningsloggar** från din app — slå på **Monitoring → App Service logs → Application logging (Filesystem)** och sätt t.ex. `Logging__LogLevel__Default` = `Information` i Configuration, annars kan stream vara tom även när appen levererar trafik.

**Azure Portal → Web App → Configuration → Application settings** (minsta för riktig drift):

- `ConnectionStrings__DefaultConnection` – Npgsql-sträng till Azure Database for PostgreSQL (eller annan Postgres). Utan denna kör appen SQLite in-memory (data försvinner vid omstart).
- `ASPNETCORE_ENVIRONMENT` = `Production`
- `Cors__Origins__0` = bas-URL till din Blazor WASM / frontend (tom lista ger `AllowAnyOrigin` i kodläget, men sätt explicit i prod om du kan).

**Deployment Center:** om Azure skapar en *egen* GitHub-workflow vid koppling, antingen ta bort den duplicerade eller använd bara en av workflows så samma push inte deployar dubbelt.

**Deploy från Git utan GitHub Actions (Oryx/Kudu):** sätt appinställningen **`PROJECT`** till `src/FactoryGame.Api/FactoryGame.Api.csproj` så rätt projekt byggs i en monorepo-lösning.

### External Git + App Service Build Service (det du har nu)

Om **Source = External Git** och **Build provider = App Service Build Service** bygger **Azure (Oryx)** direkt från repot vid varje **Sync** — då syns deployment oftare i **Deployment Center → Loggar** än vid ren GitHub Actions zip-deploy.

Gör så här så det matchar denna kodbas (**NET 8**, API-projekt i undermapp):

1. **Configuration → General settings → Stack settings** (eller motsvarande i din portal): **.NET** version ska vara **8** (LTS), **inte 10**. Alla `TargetFramework` i lösningen är `net8.0`; stack 10 ger onödig risk och fel diagnos.
2. **Configuration → Application settings** → lägg till **`PROJECT`** = `src/FactoryGame.Api/FactoryGame.Api.csproj`  
   Annars försöker Oryx ofta bygga hela `.sln` eller fel projekt och bygget misslyckas eller deployar inte API:t.
3. Repot har **`global.json`** i roten så Oryx väljer **.NET 8 SDK** i linje med projektet.
4. **Branch** ska peka på den gren du pushar till (t.ex. `master`). GitHub använder normalt **gemener** `master`; om Sync aldrig hämtar ny kod, kontrollera branchnamn exakt mot GitHub.

**Två deployvägar:** du kan antingen köra **External Git + Oryx** *eller* enbart **GitHub Actions** (`azure-webapp-api.yml`). Båda samtidigt kan skapa förvirring (dubbel deploy, olika “deployment”-listor). För enkelhet: **Disconnect** External Git om du vill låta GitHub Actions äga deploy helt.

## Säkerhet (MVP)

- Byt `Admin:BootstrapToken` i produktion eller stäng av admin-routes bakom nätverk.
- API-nycklar lagras som SHA-256-hash.
- Rate limiting: global fast fönster per spelare eller IP (se `Program.cs`).

## Struktur

- `src/FactoryGame.Api` – HTTP, OpenAPI, middleware
- `src/FactoryGame.Domain` – DNA, simuleringsstubbar, innehåll
- `src/FactoryGame.Infrastructure` – EF Core, tjänster, bakgrundstjänster
- `src/FactoryGame.Contracts` – delade DTO:er
- `src/FactoryGame.Web` – Blazor WASM PWA
- `tests/*` – enhets- och integrationstester
