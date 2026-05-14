# Versionshistorik

Kortfattad lista över vad som ingår i varje levererad version. Git-commit för en release har **endast** semver som meddelande (samma värde som `Version` i `Directory.Build.props`); git-tag är `v{Version}`.

## 0.1.3

- API: `GET /diagnostics/recent-logs` — oformaterad text med loggrad sedan processstart (ingen autentisering). Aktiveras i **Development** alltid; i övriga miljöer sätt `Diagnostics:ExposeRecentLogEndpoint` till `true` om du behöver den i t.ex. Azure-felsökning (lämna av i produktion).
- Webbklient: tydligare API-bas vid lokal Blazor dev-server (`WasmApplicationEnvironmentName`, fallback till `https://localhost:7145` för kända dev-portar) samt bättre feltext vid anslutningsfel.
- Infrastruktur: EF Core-migrationsfiler borttagna; SQLite-schema skapas med `EnsureCreatedAsync()` (befintlig databasstrategi i kodbasen).
- Övrigt: uppdateringar i Docker/README/KRAVSPEC och API-tester (SQLite-fixture i stället för Postgres-fixture).

## 0.1.2

- API: Swagger/Swashbuckle körs endast för sökvägar under `/swagger`, så rot-URL (`/`) lämnas till Blazor-PWA och fallback till `index.html` utan att Swagger-middleware kan lägga sig i vägen.

## 0.1.1

- Versionsflöde: commit-meddelande vid release är enbart semver; release-noteringar flyttas till denna fil (`releases.md`).

## 0.1.0

- Central `Version` i `Directory.Build.props` för alla projekt; versionsrad i webbklientens sidfot och i Swagger.
- Cursor-regler för commit/push, version/taggar och leveransmeddelande i chatten.
