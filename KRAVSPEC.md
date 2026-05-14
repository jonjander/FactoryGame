# Kravspecifikation - Webbaserat spel (.NET)

## 1. Syfte och mål
Detta dokument beskriver krav för ett webbaserat spel byggt med .NET.

Overgripande mål:
- Leverera en spelbar MVP som fungerar i moderna webblasare.
- Bygga en stabil grund for vidareutveckling (fler features, balans, multiplayer).
- Ha tydliga kvalitetskrav kring prestanda, sakerhet och drift.

## 2. Vision
Spelet ska vara latt att komma in i, ge tydlig progression och fungera bra pa desktop och mobil webblasare utan installation.

## 3. Malgrupp
- Primar: casual players (13+), korta sessioner 5-20 minuter.
- Sekundar: mer engagerade spelare som vill optimera och aterkomma dagligen.

## 4. Omfang

### Inom omfang (MVP)
- Webbaserat spel (frontend i webblasare, backend i .NET), gärna som PWA med offline-stod for redigering (se F26).
- Konto via extern IdP (t.ex. Google / OpenID Connect); inga sparlosenord i egen databas (se F1). **Lokalt/gastkonto** far overvagas tidigt i utveckling for att slippa inloggning under forsta iterationer (se F1).
- Grundlaggande spel-loop (start, spela, progression, spara, avsluta).
- Server-side speldata och persistence; kontinuerlig tick-simulering pa server (se F25).
- Exakt **20 grundamnen** i MVP-innehall; arkitektur och `dna`-modell ska skala till **tusentals** utan omskrivning.
- Enkel admin-/operationsyta for overvakning.

### Utanfor MVP (framtid)
- Avancerad multiplayer i realtid.
- In-game betalningar.
- Sociala features (guilds, chat, friends).
- Avancerade event/season-system.
- **Underhall/slitage** av maskiner, energi- eller resursforbrukning per tick, och relaterad ekonomi (planerat; inget krav i forsta leverans men datamodellen ska inte stanga dörren).

## 5. Funktionella krav

### F1 - Kontohantering och IdP
- Primar inloggning ska ske via extern identitetsleverantor (rekommenderat: **Google** eller annan OpenID Connect-leverantor).
- Backend ska **inte** lagra anvandarlosenord; endast IdP-subject, tokensession och nodvandig profilmetadata.
- Spelare ska kunna logga in och logga ut.
- Losenordsaterstallning hanteras av IdP, inte av spelet.
- **Utvecklings-/MVP-fas:** valfritt **lokalt eller gastbaserat konto** (t.ex. enhetsspecifik nyckel lagrad lokalt) far anvandas for att snabba upp iteration; ska kunna **migreras** till IdP-konto senare utan dataforlust.

### F2 - Spelarsession
- Spelare ska kunna starta nytt spel.
- Spelare ska kunna fortsatta pa tidigare sparad progression.
- Spelare ska kunna pausa/avsluta utan att data forloras.

### F3 - Spel-loop
- Spelet ska ha tydliga mal och beloningar.
- Spelaren ska kunna gora handlingar som paverkar progression.
- Spelet ska validera handlingar server-side.
- Spelets core-loop ska vara: kopa -> foradla -> salja eller foradla vidare.

### F4 - Progression och lagring
- Spelarens progression ska sparas i databas.
- Kritisk data (resurser, upplasningar, statistik) ska versionshanteras.
- Spelet ska kunna hantera migration av data mellan versioner.
- Historik over transaktioner och foradlingar ska sparas for spelbalans och felsokning.

### F7 - Marknadsplats
- Spelaren startar med enbart pengar; material som ska in i fabrik eller saljas pa bors ska ligga i **en enda volym-pool per spelare** (**seaport**-modellen, se F28). Material far **ligga kvar** i poolen utan att omedelbart matas ut i fabriken.
- Handelsenheter pa borsen ar **abstrakta enheter** (andelar/volymer i spelterm) som mappas till saldo i seaport-poolen; implementationen ska vara konsekvent mellan order, leverans och fabrik.
- Marknadsplatsen ska fungera som en `stock exchange` dar grundamnen handlas; **endast spot** (kop/salj mot **befintligt saldo** i poolen) — ingen blankning, inga derivat i MVP.
- **All borshandel sker endast pa servern.** Klienten skapar och annullerar ordrar; matchning, avslut och saldo sker server-side.
- **Borsen ar inte tillganglig offline.** UI ska visa tydligt **offline-/otillgangligt lage** for borsen (ingen live-handel, ingen palitlig livekurva); se F26.
- Vid offline fortsatter **staende** kop-/saljordrar att **exekveras pa servern**; spelaren ser utfallet via **transaktionslogg** nar hen ar online igen.
- Spelaren ska kunna lagga kop- och saljordrar pa grundamnen.
- Matchning av ordrar ska ske via en orderbok per grundamne.
- Senast betalt pris (last trade) ska uppdatera marknadspriset.
- Kopsumma/saljsumma och tillgangliga volymer ska alltid verifieras server-side innan avslut.
- **Full seaport-pool:** om leverans fran kop skulle **overstiga** tillganglig volym ska **kopet blockeras** (ingen ko, ingen spill).
- Vid kop/salj ska levererat grundamne hamna i eller dras fran spelarens **seaport**-pool.

### F12 - Handelsfunktioner (bors)
- Varje grundamne ska ha ticker/symbol, aktuellt pris, omsattning och historik.
- Marknaden ska visa orderdjup (basta kop/salj) for respektive grundamne.
- Spelaren ska kunna se innehav per grundamne (abstrakta enheter / saldo i poolen) och genomsnittligt anskaffningsvarde dar det ar relevant.
- En **transaktionslogg** (kop, salj, avslut, leverans till/fran pool) ska finnas for spelaren och anvandas som primar feedback nar klienten varit offline.
- Handelsavgift/courtage ska kunna konfigureras globalt eller per grundamne.
- Prisgraf (OHLC eller motsvarande) ska kunna visas for vald period.
- Spelaren ska kunna skapa **staende** kop- och saljordrar (t.ex. good-until-cancelled) som ligger kvar tills de fylls eller avbryts; exekvering sker pa server oberoende av om klienten ar online.
- Pris pa **lagvardiga** eller oonskade produkter (t.ex. `aska`/`goo`) styrs av **efterfragan** i orderboken; om ingen efterfragan finns ska **effektivt pris** kunna bli **noll** — inte hardkodat, utan marknadsdrivet.

### F8 - Materialsystem (fake periodiskt system)
- Spelet ska ha ett materialbibliotek med unika material-id, **generatoriskt visningsnamn** (se F29) och kategori.
- Material ska delas in i exakt en kategori: `solid`, `liquid` eller `gas`.
- Varje material ska kunna ha metadata (t.ex. baspris, sallsynthet, upplasningsniva).
- Nya material ska kunna laggas till utan att bryta befintliga recipes eller spelarprogression.
- Alla material ska implementeras som samma domanklass (`Material`) med en enhetlig struktur.
- Materialets egenskaper ska beraknas fran ett verifierbart och berakningsbart `dna`.
- `dna` ska vara tillrackligt for att reproducera materialets egenskaper deterministiskt.
- Manuellt hardkodade specialfall per material ska undvikas i spelreglerna.
- Varje grundamne ska representeras av en `long`-kod dar bitfalt beskriver materialets `dna`.
- Manipulation i maskiner ska primart ske med bitvisa operationer och matematiska transformationer.
- Datamodellen ska vara designad for hog prestanda och skalning utan stora if/else-kedjor.

### F13 - Materialegenskaper
- Varje grundamne ska ha foljande obligatoriska egenskaper:
  - `explosivitet` (0-100)
  - `brandfarlighet` (0-100)
  - `toxicitet` (0-100)
  - `typ` (`solid` / `liquid` / `gas`)
  - `boilingPoint`
  - `freezePoint` (solid point)
- Egenskaperna ska beraknas fran `dna` och vara verifierbara i efterhand.
- Maskiner ska kunna hoja/sanka minst en av egenskaperna per transformation.
- Om en egenskap passerar definierade gransvarden ska spelet kunna utlosa riskevents (t.ex. driftstopp, kassation, bonusutfall).

- Foreslagna extraegenskaper for djupare gameplay:
  - `korrosivitet` (hur mycket materialet sliter pa maskiner/ledningar)
  - `reaktivitet` (hur starkt materialet reagerar med andra material)
  - `stabilitet` (hur kansligt materialet ar for temperatur- eller tryckandring)
  - `densitet` (paverkar lagringskostnad och transportkapacitet)
  - `renhet` (paverkar marknadsvarde och recipe-kompatibilitet)
  - `energiinnehall` (paverkar varmebehov/produktionshastighet)

### F9 - Foradlingssystem
- Spelaren ska kunna foradla ett eller flera input-material till output-material enligt recipes.
- Recipe ska kunna ge huvudprodukt och minst en biprodukt.
- Exempel: material `x` kan foradlas till material `u` + biprodukt `f`.
- Systemet ska stoppa foradling om spelaren saknar indata eller pengar for processkostnad.
- Foradlingsresultat ska kunna saljas direkt eller anvandas i nya recipes.

### F10 - Maskiner och floden
- Spelet ska innehalla olika maskintyper med definierade in- och utgangar (ports).
- Spelaren ska kunna koppla en maskins utgang till en annan maskins ingang.
- Varje port ska ha typregler for vilka materialkategorier den accepterar: `solid`, `liquid`, `gas`.
- Floden mellan maskiner ska valideras server-side sa att ogiltiga kopplingar blockeras.
- En maskin ska endast kunna bearbeta material som finns i anslutet inflode.
- Spelaren ska kunna salja output fran en maskin direkt till marknaden eller dirigera vidare i kedjan.
- Varje maskin ska kunna paverka ett eller flera egenskapsfalt pa materialet (via materialets `dna`-regler).

### F11 - Standardmaskiner (MVP)
- `Boiler` (1:1): en ingang, en utgang. Hojer temperaturrelaterade egenskaper.
- `Liquid Separator` (1:2): en ingang, tva utgangar. Delar en liquid i tva fraktioner.
- `Destilator` (1:2): en ingang, tva utgangar. Separering baserat pa kokpunktslogik.
- `Mixer` (2:1): tva ingangar, en utgang. Kombinerar tva material till en ny sammansatt output.
- `Heater` (1:1): en ingang, en utgang. Hojer energi/temperaturprofil.
- `Cooler` (1:1): en ingang, en utgang. Sanker energi/temperaturprofil.
- `Sorter` (1:4): en ingang, fyra utgangar. Spelaren konfigurerar port **1–3** via **multi-dropdown** (per port: noll eller flera grundamnen). **Port 4** ar alltid **rest**.
- **Routningsregel:** varje inkommande grundamne skickas till **den utgang dar det grundamnet ar listat** i konfigurationen. Ar grundamnet **inte** konfigurerat pa nagon av port 1–3 gar det till **port 4**.
- Server ska **validera** att samma grundamne inte ar valt pa mer an en av port 1–3 (annars otydlig konfiguration); ogiltig konfiguration ska blockeras vid sparning/start.
- Tom port (inga valda grundamnen) tar **inte** emot nagot via "match" — endast explicit listade grundamnen gar dit; ovrigt till port 4.
- Samtliga maskiner ska ha tydlig throughput och process-tid som kan balanseras via konfig.

### F23 - Maskininstallningar och tuning
- Spelaren ska **inte** kunna andra maskininstallningar medan spelplanen ar `Running`. Andringar sker endast i `Edit`-lage.
- Vid overgang till `Running` (**start**) ska aktuellt plan-state (inkl. installningar) sparas pa server och en **ekonomisk granskning** genomforas (rad, kostnader, tillatna kopplingar).
- Varje maskin ska ha konfigurerbara installningar som paverkar produktion, kvalitet, risk och energikostnad.
- Exempel pa installningar:
  - `Heater`/`Boiler`: varmeeffekt, ramp-hastighet, maltemperatur
  - `Mixer`: ratio mellan input 1 och input 2, mix-intensitet, mix-tid
  - `Separator`/`Destilator`: cut-points, reflux/returgrad, processhastighet
  - `Cooler`: kylkapacitet, maltemperatur, kyltid
- Installningarna ska valideras server-side mot tillatna intervall och maskinniva/upplasningar.
- Installningar ska ingå i simuleringen och i spelplanens versionssnapshot.
- Små andringar i installningar ska kunna ge stora skillnader i output (hog skill ceiling).
- Ogynnsamma installningar ska kunna ge ineffektiv output, fallback-material (`aska`/`goo`) eller driftproblem.

### F24 - Sallsynthet och svårighetsgrad
- Unika/hogvardiga grundamnen ska vara avsiktligt **svara** att framstalla; spelet ska i ovrigt vara **ganska oförlåtande** (fel tuning eller kedja ska straffa tydligt).
- Utfall ska vara **100% deterministiska**: samma `dna`, samma maskininstallningar, samma regelversion och samma kedjeordning ger **exakt samma** resultat. **Ingen** `Random` som paverkar utfall; om slump nagonsin infors maste den vara **seedad** och sparas sa att simulering gar att **aterspela** bit-for-bit.
- Misslyckad eller degraderad produktion (t.ex. `aska`/`goo`) ska bero pa **regler och toleranser** (inkl. tuning utanfor giltigt fonster), inte pa osynlig chans.
- Misslyckad process ska ge tydlig feedback (regelbrott, toleransmiss, instabilitet) och korrekt fallback-output.
- Balans ska styra att marknaden periodvis har verklig brist pa vissa eftertraktade grundamnen.

### F14 - Spelplaner och fabrikslinjer
- En spelare ska kunna ha en eller flera spelplaner.
- Varje spelplan ska ha en **seaport** som nav for koppling till borsen och delat lager mellan planer (se F28).
- Varje spelplan ska innehalla 0-x produktionslinjer med maskiner, kopplingar och floden.
- Spelplanen ska kunna vara i exakt ett av lagen: `Edit` eller `Running`.
- I `Edit`-lage far spelplanen inte generera produktion eller pengar.
- I `Running`-lage ska spelplanen vara read-only for klienten.
- Spelaren ska kunna byta lage mellan `Edit` och `Running` via server-validerade kommandon.

### F15 - Byggande och sparning av spelplan
- Klienten ska skicka byggandringar som transaktionsbaserade kommandon (t.ex. placera maskin, ta bort koppling).
- Servern ska vara auktoritativ och avgora om andringen ar giltig.
- Vid sparning av spelplan ska servern verifiera att spelaren har rad med valda maskiner och andringar.
- Om verifiering misslyckas ska sparning avvisas utan delvis applicerade andringar.
- Godkand sparning ska ge en ny versionssnapshot av spelplanen.
- **Offline:** sparning som kraver serververifiering ska **koas** och skickas nar klienten ar online igen. Vid **konflikt** mellan lokal och server-sanning ska klienten erbjuda **merge**-flode (t.ex. valj vilken version som ska galla, eller jamfor skillnader) i stallet for tyst att kasta bort lokal andring utan anvandarbeslut.

### F16 - Serverauktoritativ simulering
- Berakning av produktion, materialfloden och ekonomi ska ske pa serversidan.
- Klienten far endast presentera state och foresla handlingar; klienten far inte satta slutligt spelstate.
- Samtliga spelkritiska operationer (kop/salj, byggande, lagebyte) ska vara transaktionsbaserade och atomiska.

### F25 - Tick, synk och klientpresentation
- Serversidan ska kora simuleringen **kontinuerligt** i **tick** med **maximal ticklangd 5 sekunder** (konfigurerbart nedat, aldrig langre an 5s i kravbilden).
- Vid **belastning eller efterslapning** far servern **halka** och sedan **catch-up** genom att kora flera tick i foljd tills simuleringen ar i fas; tick-storleken per steg ska fortfarande respektera max 5s per logisk tick (eller dokumenterad catch-up-policy sa CPU-spikar begransas).
- **Synkronisering mellan spelare** behover **inte** vara global: tick-schemat ska **skala** (t.ex. per spelare, per shard eller per varld) — kravet ar forutsagbar prestanda, inte att alla delar samma globala klocka.
- Klienten ska periodiskt hamta **keyframes** (eller motsvarande snapshot + tick-index) fran servern som sanningskalla.
- Klienten far **interpolera** och lokalt **approximera** flode/animation for att upplevas mer realtid; vid ny keyframe ska klienten **jamka** mot serverns tillstand.
- Officiell klient ska vara mojlig att leverera som **PWA** (se F26).

### F26 - PWA och offline-beteende
- Webklienten ska kunna byggas som **PWA** med cache av shell och senast kanda regler/wiki-generering.
- **Offline** far spelaren **stoppa** fabrik (om lokalt tillstand) och **editera** plan; **start** av `Running` och bors krav normalt **online**.
- **Bors:** nar anslutning saknas ska borsen visas som **offline/ej tillganglig** (ingen handel, ingen palitlig live-data); eventuellt cacheat pris far visas med tydlig **varning** att det ar inaktuellt — eller borspanelen dold tills online (produktbeslut).
- Offline-andringar ska synkas via koade kommandon nar anslutning aterkommer; server validerar; konflikter hanteras med **merge** dar det ar lampligt (se F15).

### F28 - Seaport (en volym-pool per spelare)
- Det finns **exakt en volym-pool per spelare** for fysiska grundamnen som ska **in i fabriken** eller **saljas pa borsen**. Material far **lagras** i poolen utan att omedelbart anvandas.
- Varje spelplan har en **seaport-nod** som ar anslutningspunkt till **samma** pool (inte separata lager per plan). Overforing mellan planer sker implicit: material finns i poolen och kan matas in pa vald plans linjer via respektive seaport.
- Poolen ska ha **max volym** (hard gran). **Kop fran bors** som skulle **overskrida** volym ska **blockeras**.
- Seaport-noden ska ha **max antal in- och ut-portar** (konfigurerbart per upplasning eller globalt).
- Seaportens **utgangar** ska vara konfigurerbara sa att en eller flera utgangar kan **ge ifran sig** ett eller flera valda grundamnen (flodesregler server-validerade).
- Kop fran bors levererar till poolen; salj drar fran poolen enligt saldo, volym och spot-regler.

### F29 - Generatoriska namn och wiki (100% genererat)
- **Wiki** ska vara **100% genererad** fran samma data som servern (regler, maskiner, egenskapsintervall, fallback); inga manuella wikisidor som sanningskalla.
- **Visningsnamn** for grundamnen ska **inte** hardkodas per id; de ska **harledas** fran `dna`/egenskaper via en **namngenerator** (fiktiva kemiska morfemer per egenskapsdimension, t.ex. sammansattningar i stil med `TyBoSodioum`, `BiKarbonitSulfat`).
- For ett givet `dna` och fixerad namngenerator-version ska visningsnamnet vara **stabilt** (samma namn for alltid for den kombinationen); vid framtida andring av generator ska **migreringsregel** finnas (t.ex. spara `displayNameVersion` per material).
- **Internationalisering:** morfem-tabeller / ordlistor ska kunna **lokaliseras per sprak** (sv, en, …) sa att genererade namn och wiki-text följer anvandarens locale, utan att hardkoda en namnlista per grundamne.
- Vid skalning till tiotusentals grundamnen ska namn forbli **unika** inom sprak/region (eller vid kollision: deterministisk suffix/regel) utan manuell namnlista.
- Andring av namngenerator-version ska versionshanteras tillsammans med `dna`-tolkning for reproducerbarhet.

### F18 - Oppet klient-API
- Klient-API:et ska vara oppet och dokumenterat sa att tredjepartsutvecklare kan bygga egna klienter.
- API-kontrakt (request/response/event) ska vara stabila och versionshanterade.
- **Autentisering:** interaktiva klienter anvander **OAuth2/OIDC** (t.ex. Google). **API-nycklar** (eller motsvarande maskin-till-maskin tokens) med begransade **scopes** ska stodjas for skript, botar och integrationer — samma API som webben, separat behorighetsmodell.
- Rate limits ska kunna sarskilja interaktiva anvandare och nyckelbaserade klienter.
- Officiell webklient ska anvanda samma publika API som tredjepartsklienter.
- Ett sandbox/testlage ska finnas for klientutveckling utan att riskera produktionsdata.

### F17 - Basinkomst
- Alla spelare ska ha en base income som tilldelas periodiskt.
- Base income ska vara **minimal**: endast sa att spelaren **inte kan kora fast helt**; spelet ska i ovrigt vara **stramt** ekonomiskt.
- Base income ska kunna balanseras via konfiguration och kombineras med fabrikens intakter.

### F19 - Webklient (enkel factory builder)
- En officiell webklient ska finnas med fokus pa enkelhet.
- Spelaren ska kunna dra kopplingar visuellt mellan portar (t.ex. dra ror fran maskin `x` till `y`).
- Bygglage ska visa tydlig validering av tillatna/otillatna kopplingar i realtid.
- Klienten ska ha snabbkommandon for vanliga byggoperationer (placera, connect, disconnect, rotate, save).
- **Mobilanpassning:** layout och interaktion ska vara **responsiva** och anvandbara pa **mobil webblasare** (viewport-meta, rimliga touchmal, scroll och enkolumn dar utrymmet kraver det; dra-koppling far ha mobilalternativ, t.ex. valj-kalla-valj-mal, utan att ge bort servervalidering).
- **100% klient-paritet:** samma **funktionalitet** som pa stor skarm — ingen avkapad spel-logik i webben; allt som gar att gora i officiell klient ska ga att na via samma **publika API** och motsvarande UI-floden (olika **presentation** ar tillaten).
- **Visuell prioritering pa sma skarmar:** **mindre** dekorativ grafik och tunga visuella effekter; **tydligare** hierarki med **beskrivande** menyer och submenyer (rubriker, korta forklaringar dar det hjalper) sa att funktionerna ar **latta att hitta** utan att spelaren tappar orientering.
- **UX-mal:** enkelt grundupplagg, intuitiv navigation mellan huvudomraden (fabrik, ekonomi/bors, inventarie, wiki, CLI enligt MVP-scope), utan att duplicera eller dolja kritisk information.

### F20 - Inbyggt CLI i webklienten
- Webklienten ska erbjuda ett inbyggt kommandolage/terminal for avancerade spelare.
- CLI-kommandon ska mappas till samma serverkommandon som GUI anvander.
- Minimikrav pa kommandon i MVP:
  - `alias <maskin-id> <aliasnamn>`
  - `connect <source> <out-port> to <target> <in-port>`
  - `disconnect <source> <out-port> from <target> <in-port>`
  - `start`
  - `stop`
  - `save`
- CLI ska ge tydliga felmeddelanden med valideringsorsak nar kommando avvisas av servern.

### F21 - Regelmotor for maskiner
- Varje maskin ska ha en deklarativ uppsattning regler for tillatna inputs och transformationslogik.
- Regler ska kunna uttrycka krav pa egenskaper, temperaturintervall, fas (`solid`/`liquid`/`gas`) och kompatibilitet mellan flera inputmaterial.
- Regelutvardering ska vara deterministisk och kunna exekveras med bitmasker/matematiska operatorer.
- Regler ska vara data- och konfigdrivna sa att nya maskiner/material kan laggas till utan kodforandring i core-motorn.
- Om input inte uppfyller maskinens regler ska output bli ett vardelost fallback-material (t.ex. `aska` eller `goo`).
- Fallback-output ska vara explicit markerad i state, logg och UI sa spelaren forstar varfor produktionen misslyckades.

### F22 - Maskinwiki och transparens
- Spelet ska innehalla en inbyggd wiki som beskriver varje maskin och dess regler.
- Wikisida per maskin ska visa:
  - krav pa inputmaterial och egenskaper
  - temperatur- och fasvillkor
  - forvantad output vid giltig process
  - fallback-output (`aska`/`goo`) vid brutna regler
- Wikins innehall ska vara **100% genererat** fran regeldata (se F29); samma payload ska kunna anvandas av externa klienter.
- Wikisidor ska vara tillgangliga via webklientens UI och sokbara for spelaren.

### F5 - UI/UX
- Granssnittet ska vara responsivt for desktop och mobil.
- Spelet ska visa tydlig feedback pa spelarens handlingar.
- Spelet ska ha grundlaggande tillganglighet (kontrast, tangentbordsstod dar relevant).

### F6 - Drift och support
- Systemet ska logga viktiga handelser (errors, inloggning, spelkritiska actions).
- Admin ska kunna se grundlaggande status (uptime, errors, aktivitet).

## 6. Icke-funktionella krav

### NF1 - Prestanda
- Sida ska ladda initial vy inom 3 sekunder pa normal uppkoppling.
- Backend API-responser for vanliga actions ska normalt vara under 300 ms.

### NF2 - Skalbarhet
- Losningen ska kunna hantera minst 1 000 samtidiga spelare i MVP-miljo.
- Arkitekturen ska tillata horisontell skalning av backend.

### NF3 - Sakerhet
- All trafik ska ga over HTTPS.
- Autentisering ska primart ske via **OAuth2/OIDC** (t.ex. Google); tokens ska valideras server-side; **inga** primarlosenord lagras i apps databas.
- API ska skyddas med autentisering och auktorisering.
- Kanda OWASP-risker ska hanteras i design och implementation.
- Klientdata ska betraktas som opalitlig; servern ska alltid rekonstruera och verifiera konsekvenser av alla kommandon.

### NF4 - Tillforlitlighet
- Dagliga backups av speldata.
- RPO <= 24 timmar, RTO <= 4 timmar for MVP.
- Fel ska hanteras utan datakorruption.
- Transaktioner for handel och byggande ska ge ACID-liknande garantier (atomisk commit eller rollback).

### NF5 - Underhallsbarhet
- Kodstandard och formattering ska vara konsekvent.
- CI med build + tester ska kravstallas innan release.
- Logik for spelregler ska vara testbar i isolerade enheter.
- Publikt API ska ha maskinlasbar specifikation (t.ex. OpenAPI) och versionshistorik.
- Regeldefinitioner for maskiner ska kunna versionshanteras separat fran applikationskod.

### NF6 - Balans och ekonomi
- Alla pris- och recipe-andringar ska kunna konfigureras utan koddeploy (t.ex. via databas eller konfigfil).
- Spelekonomin ska kunna simuleras offline med testdata for att upptacka inflation/exploits.
- Alla ekonomiska handelser ska vara idempotenta pa API-niva for att undvika dubbla kop/salj.
- Matching-motorn ska vara deterministisk och ge samma avslut for samma ordningssekvens.
- Marknadsmanipulation via uppenbara exploitmonster (t.ex. self-trading i loop) ska kunna detekteras.
- Sallsynthetsgrad for unika grundamnen ska kunna styras via konfigurerbara balansparametrar.

### NF7 - Simulering och determinism
- Samma maskinkedja, samma input, samma maskininstallningar och samma regelversion ska ge samma output (deterministiskt resultat) inom samma spelversion.
- Simuleringen ska kunna kores i ticks/steps pa serversidan for konsekvent spelstate; ticklangd **<= 5s**.
- State-overgangar i maskiner (idle, processing, blocked) ska loggas for felsokning.
- Berakning av materialegenskaper fran `dna` ska vara deterministisk och versionsstyrd.
- Det ska ga att verifiera ett materials egenskaper i efterhand via `dna` + transformationslogg.
- Egenskapsgransvarden och riskevent-regler ska vara konfigurerbara och versionshanterade.
- Bitwise- och matematisk exekvering ska ge samma resultat oavsett klientplattform.
- **Ingen** icke-deterministisk slump i produktionsmotor; eventuellt framtida brus maste vara **seedat** och loggat for replay.

### NF8 - Skalning av grundamnen
- MVP ska levereras med **20** grundamnen i spelvarlden.
- Datamodell och spelregler ska designas for att antal grundamnen kan vaxa till **tusentals** utan omskrivning av core-logik.
- Nya grundamnen och maskinregler ska kunna introduceras via innehallsuppdateringar.

### NF9 - API-kompatibilitet
- Breaking changes i publikt klient-API ska undvikas inom samma huvudversion.
- Deprecation-policy ska finnas med overgangsperiod innan borttag av endpoints/fields.
- Officiell klient och CLI ska testas i CI mot samma versionerade API-kontrakt.

### NF10 - Regelprestanda
- Regelutvardering per maskinsteg ska kunna ske utan linjar scanning av hardkodade if-villkor.
- Motor for bitmask-baserade regler ska kunna hantera stort antal material och maskiner med forutsagbar latens.

### NF11 - Tuning, observability och fairness
- Effekten av varje maskininstallning ska vara matematisk transparent och testbar.
- Simulering ska logga vilka installningar som bidrog till utfall (lyckad process, misslyckad process, fallback).
- Svårighetsgrad ska upplevas hog men rattvis; spelaren ska kunna lasa i wiki och loggar hur utfallet uppstod.
- Balansandringar av maskininstallningar ska versionshanteras for reproducerbarhet mellan patchar.

## 7. Teknisk kravbild (.NET)

### Rekommenderad stack
- Backend: ASP.NET Core på **.NET 10** (teamets målram; följ Microsofts supportcykel).
- Frontend: Blazor Web App eller separat SPA (t.ex. React) med .NET API.
- Persistens: **Entity Framework Core** mot **SQLite** i nuvarande repo (in-memory som standard i utveckling; fil for bestandig lokal/moln-data).
- Drift / CI / paritetstester: samma SQLite-modell om inget annat beslutas; lang sikt kan **SQL Server** eller annan relations-DB valjas utan att andra krav andras i onodan.
- **Utvecklingsfas:** **SQLite** (fil eller `:memory:`). Under utveckling ska det ga att **seedea eller aterstalla** tillstand genom att **ladda upp och ladda ner en fil** direkt fran webbappen (t.ex. SQLite-databasfil eller versionerad snapshot som API:t kan importera).
- Cache (valfritt i MVP): Redis.

### Arkitekturprinciper
- Tydlig separation mellan domanlogik, applikationslogik och infrastruktur.
- API-forst design med versionering.
- Server-side validering av all spelkritisk logik.

## 8. Datakrav
- Spelarprofil, progression, inventarie/resurser, historik och statistik ska lagras.
- Audit-logg for kritiska handelser ska finnas.
- Personuppgifter ska minimeras och hanteras enligt GDPR.
- Datamodellen ska inkludera materialkatalog, recipe-katalog, marknadspriser och transaktionslogg.
- Datamodellen ska inkludera `material_dna`, egenskapsberakning/version och maskin-transformationslogg.
- Datamodellen ska inkludera orderbok, handelsavslut, kurshistorik och spelarens innehav per grundamne.
- Datamodellen ska inkludera spelplan, spellansversioner, maskinplaceringar, kopplingar och line-state.
- Datamodellen ska inkludera alias per spelplan (for CLI), samt kommandohistorik for audit/debug.
- Datamodellen ska inkludera regeldefinitioner per maskin, regelversion och fallback-regler (`aska`/`goo`).
- Datamodellen ska inkludera maskininstallningar per instans samt tuning-historik for analys och replay.
- Datamodellen ska inkludera **seaport**-konfiguration (volym, portgranser, utgangs-mappning), delat lager-id for spelaren.
- Datamodellen ska inkludera **simulerings-tick**-metadata och **keyframes**/snapshot-version for klientsynk.
- Datamodellen ska inkludera **namngenerator**-version och parametrar for wiki/namn.
- Datamodellen ska inkludera **transaktionslogg** for bors (ordrar, avslut, leveranser till/fran volym-pool) med tidsstampel och idempotens-nycklar dar lampligt.

## 9. Kvalitetskrav och test

### Testnivaer
- Enhetstester for domanlogik.
- Integrationstester for API + databas.
- Enkel end-to-end-test for huvudfloden i MVP.

### Acceptanskriterier (MVP)
- Ny spelare kan registrera konto och spela inom 2 minuter.
- Spelstatus sparas och kan aterupptas utan dataforlust.
- Minst 95% av kritiska API-anrop lyckas under normal belastning.

## 10. Leverabler
- Kravspecifikation (detta dokument).
- Arkitekturdokument (separat).
- Backlog med user stories och prioritering.
- MVP-release med driftinstruktion.

## 11. Oppna fragor (att besluta)
- Ska MVP vara single-player eller enkel asynkron multiplayer?
- Ska frontend byggas i Blazor eller separat JS-ramverk?
- Vilken **SQL-provider** ska vara primar i produktion nar SQLite inte racker (t.ex. SQL Server), givet att EF Core ar gemensamt lager?
- Vilken molnplattform ska anvandas for hosting?
- Transport for keyframes: polling, SSE eller WebSockets/SignalR?
- Ska marknadspriser vara rena orderbokspriser eller finns referens-/styrpris fran server?
- Ska recipes/periodiska event rotera eller ar allt statiskt tills ny content-patch?
- Vilken OIDC-leverantor utover Google (om nagon) ska stodjas i MVP?

## 12. Spelmekanik - detaljerad MVP-loop

1. Startlage:
- Ny spelare far ett startkapital i pengar.
- Lager ar tomt pa material.

2. Inkop:
- Spelaren koper andelar i grundamnen pa borsen via order.
- Material tillhor en av kategorierna `solid`, `liquid`, `gas`.

3. Foradling:
- Spelaren valjer en recipe och foradlar till ny produkt.
- Recipe kan ge huvudprodukt + biprodukter.
- Foradling sker i maskiner som kopplats ihop via in- och utgangar.
- Varje maskin modifierar materialets egenskaper enligt verifierbara `dna`-regler.

4. Beslutspunkt:
- Spelaren valjer att salja output pa marknaden for pengar, eller
- anvanda output som input i nasta foradlingssteg.

5. Progression:
- Vinst aterinvesteras i nya material och recipes.
- Spelaren bygger gradvis mer lonsamma foradlingskedjor.
- Spelarens kreativitet i fabriksdesign ska vara den primara vagen till battre output och hogre varde.
- Base income ger en stabil grund, medan effektiv fabriksdesign avgor den stora uppsidan.

## 13. Nasta steg
1. Besluta speltyp och core gameplay-loop.
2. Bryta ned krav till epics och user stories.
3. Definiera MVP scope (Must/Should/Could/Won't).
4. Ta fram teknisk arkitekturskiss.
5. Starta implementation av grundprojekt (.NET + frontend + CI).
