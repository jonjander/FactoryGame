---
name: factory-game-azure-deploy
description: >-
  Deploys FactoryGame.Api (+ embedded Blazor PWA) to Azure App Service via
  Zip Deploy using a local PublishSettings file. Use when releasing a new
  Version, pushing a delivery to Azure, or when the user asks to deploy /
  publish to Azure (not Git/Oryx sync).
disable-model-invocation: true
---

# FactoryGame -- Azure deploy (Zip Deploy)

## When

- After a **version release** (`Directory.Build.props` bump + tag) — mandatory step in `factory-game-version-and-tags`.
- When the user asks to **deploy / publish to Azure** without waiting for Git Deployment Center.

Prefer this over Azure **External Git / Oryx** for agent-driven releases.

## Credentials (never commit)

| Source | Path / env |
|--------|------------|
| Default | `.local/FactoryGame.PublishSettings` (gitignored) |
| Override | `-PublishSettingsPath` or env `FACTORYGAME_PUBLISH_SETTINGS` |

Download: Azure Portal → App Service **FactoryGame** → **Get publish profile**.

Do **not** commit `*.PublishSettings`, passwords, or publish profile XML. Do **not** paste credentials into chat, skills, or commit messages.

## Deploy command

From repo root (agent runs this in Cursor):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .cursor/skills/factory-game-azure-deploy/scripts/Deploy-Azure.ps1
```

Optional:

```powershell
# Custom profile path
.\.cursor\skills\factory-game-azure-deploy\scripts\Deploy-Azure.ps1 -PublishSettingsPath "C:\path\FactoryGame.PublishSettings"

# Skip rebuild (reuse _publish_azure)
.\.cursor\skills\factory-game-azure-deploy\scripts\Deploy-Azure.ps1 -SkipBuild

# Skip /health smoke
.\.cursor\skills\factory-game-azure-deploy\scripts\Deploy-Azure.ps1 -SkipSmoke
```

What the script does:

1. `dotnet publish` `src/FactoryGame.Api/FactoryGame.Api.csproj` `-c Release` → `_publish_azure/`
2. Zip + **Kudu Zip Deploy** (`/api/zipdeploy`) with ZipDeploy (or MSDeploy) credentials from PublishSettings
3. Poll async deploy status
4. Smoke `GET {destinationAppUrl}/health` until `Healthy`

## Target

- **App:** `https://factorygame-h5hmbzgncnazcmgu.swedencentral-01.azurewebsites.net`
- **Project:** `FactoryGame.Api` (hosts `/v1` + Blazor WASM)

App settings (Portal) are unchanged by Zip Deploy — DB, `ASPNETCORE_ENVIRONMENT`, etc. stay as configured.

**Required Portal config (once per Web App):**

1. **Application setting** `ConnectionStrings__DefaultConnection` = Azure SQL string with `Authentication=Active Directory Default` (preferred over file inject).
2. **Startup Command** = `dotnet FactoryGame.Api.dll` (prevents Linux/Oryx `hostingstart` fallback).
3. After changing settings: **Restart**.

Optional local inject (gitignored): `.local/azure-sql-connection.txt` or env `FACTORYGAME_SQL_CONNECTION` — written into published `appsettings.Production.json` by `Deploy-Azure.ps1`.

**Never publish `appsettings.Local.json`** (developer localhost SQL). It is gitignored but can still land in publish output if present on disk; `FactoryGame.Api.csproj` and `Deploy-Azure.ps1` strip it. If it reaches Azure it overrides Production and crashes migrate.

## Release workflow (with version bump)

After git push + tag (see `factory-game-version-and-tags`):

1. Ensure `.local/FactoryGame.PublishSettings` exists.
2. Run `Deploy-Azure.ps1` (above).
3. Report in chat: delivered version **and** that Azure deploy succeeded (or failed with error).
4. Optional: `@factory-game-azure-test` smoke / `Smoke-AzureApi.ps1`.

## Failure

| Symptom | Action |
|---------|--------|
| PublishSettings missing | Copy profile into `.local/` or set env / `-PublishSettingsPath` |
| 401 on zipdeploy | Re-download publish profile (password rotated) |
| Health never Healthy | Portal Log stream / `GET /diagnostics/recent-logs`; check ConnectionStrings |
| Publish fails | Fix `dotnet publish` locally first |
| Kudu rsync `Invalid argument (22)` on paths with `\` | Fixed in 0.3.7+ (`New-DeployZip` uses `/`). Clear wwwroot and redeploy. |
| SQL migrate to `127.0.0.1` / crash after deploy | `appsettings.Local.json` was published — delete from wwwroot and redeploy 0.3.8+ |

## Related

- Smoke / URL / diagnostics: `@factory-game-azure-test`
- Version + tag routine: `.cursor/rules/factory-game-version-and-tags.mdc`
