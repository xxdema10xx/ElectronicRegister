# ElectronicRegister — MULTITENANCY MT-5A

## Certificazione successiva — compatibilita test MAMP

**MT-5A COMPLETATA — 711/711 PASSED ON MAMP MYSQL 5.7.24**

Sulla sola istanza isolata 13306 con schema MT4C verificato: mirati corretti 4/4,
baseline storica **675/675**, nuovi **36/36**, due suite consecutive entrambe
**711/711 Passed** (51 unit + 660 integration), zero Failed/Skipped/NotExecuted.
Tutte le 675 identita storiche preservate; nomi/outcome delle due suite uguali,
hash sorgenti e sei SQL canonici invariati tra i run. Modificati soltanto i due test
autorizzati; nessuna modifica applicativa, JWT/gate/worker o migrazione.

98 FK senza orfani, 53 trigger invariati, ledger/revision e validatori SQL corretti,
nessuna fixture istituzionale residua. Originale 3306 invariato negli snapshot,
GLOBAL sql_mode invariato. Rollout default false, nessuna prontezza produzione dichiarata,
nessun MT5B/commit/push/cutover. Certificazione MySQL 5.7.24, non MariaDB 10.4.
[Report completo e limite diagnostico del rollback](MULTITENANCY_MT5A_TEST_COMPATIBILITY_FIX.md).

Tutti gli esiti precedenti sotto sono conservati integralmente come storico.

## Validazione finale MAMP isolato — 20 ottobre 2026

**MT-5A PARZIALE — VALIDATION BLOCKED.** Schema MT-4C verificato su MySQL 5.7.24
MAMP isolato, porta 13306: 32 tabelle, 53 trigger, 98 FK e 139 indici;
validatori MT4A/MT4C senza violazioni. Build superata, smoke 4/4 Passed.
Baseline: **51/51 unit e 622/624 integration**, totale **673/675 Passed,
2 Failed, zero Skipped/NotExecuted**. Tutte le 675 identita coincidono con MT4C final-2.

I blocchi riguardano un'asserzione che presume GLOBAL sql_mode non strict e
l'iniezione timeout SELECT SLEEP con una forma incompatibile con la materializzazione
attesa. Diagnosi, limiti dell'inferenza e proposte non applicate nel
[report finale](MULTITENANCY_MT5A_FINAL_VALIDATION.md). Nessuna modifica a codice o test.
I nuovi 36 e le due suite 711 non sono stati avviati dopo la baseline fallita.
Database originale 3306 invariato nei controlli prima/dopo; integrita finale della copia
verificata. Rollout chiuso, nessun MT5B o cutover.

Gli esiti precedenti seguono integralmente come evidenze storiche. Il blocco di schema
e stato risolto dalla recovery isolata; resta mancante la certificazione completa.

## Aggiornamento recovery

**Stato PARZIALE confermato.** La diagnosi successiva ha identificato il processo MAMP
MySQL5.7.24 su3306/datadir C:\MAMP\db\mysql, hostname DESKTOP-QUOAG32.
I TRX MT4C provengono invece da CES01622. MariaDB10.4.32 XAMPP e installato localmente
ma non attivo e senza electronic_register_test nel datadir configurato. Il backup storico
SHA256 corrispondente al report e precedente all'espansione MT4C, non lo schema finale.
Nessuna nuova suite, restore, migrazione o modifica applicativa nella recovery: schema
corretto non reperito, quindi arresto alla diagnosi come richiesto.
[Report recovery, evidenze e piano non eseguito](MULTITENANCY_MT5A_RECOVERY_REPORT.md).
Gli esiti e le limitazioni originarie sotto sono conservati integralmente.

**MT-5A PARZIALE.** Directory amministrativa read-only implementata e 36 nuovi test verdi
con HTTP/Program/JWT/SQL reali sul server disponibile. Rollout default false, nessun cutover.
Non e raggiunta la certificazione richiesta: DB disponibile MySQL 5.7.24 con schema precedente
a MT4, non MariaDB MT4C. I 624 integration preesistenti falliscono gia alla baseline nel
preflight schema. Due suite complete consecutive eseguite, entrambe non verdi.

## 1. Git iniziale e ambiente

Branch effettivo `refactor-database-entities`, HEAD
`a002cb66cebecfdcdf892c099cf0518b796b7e1a`, workspace inizialmente pulito.
Non e il vecchio HEAD dei report MT4C. [HEAD](TestResults/mt5a/initial-head.txt),
[status iniziale](TestResults/mt5a/initial-git-status.txt).
Nessun reset/checkout/pull/commit/push, modifica operativa o GLOBAL sql_mode.
Workspace locale utilizzato senza assumere equivalenza con il remoto.

SDK .NET10.0.401. Il primo tentativo di restore e fallito per lettura negata del
NuGet.Config del profilo nella sandbox; build/test successivi con `--no-restore` e asset
gia disponibili. Nessun pacchetto/configurazione globale modificati.

## 2. Baseline675 e database reale

[Preflight](TestResults/mt5a/preflight.txt): solo `127.0.0.1:3306/electronic_register_test`,
**MySQL5.7.24**, 28 tabelle, 40 trigger; sola holding MT3A_LEGACY_TEST Provisioning.
Questo ambiente non coincide con il MariaDB10.4.32/32tabelle/53trigger del report MT4C.
Membership/role/User hanno PK/FK e indici leggibili e idonei alle query nuove.

Mancano course_edition, course_edition_campus, student_enrollment,
course_administration_event, class.course_edition_id e il relativo indice/FK.
TestDatabaseSchema rifiuta lo schema prima del seed/esecuzione dei624 casi integration.
Non e stato eseguito EnsureCreated su schema vuoto, tooling preparatorio o migrazione per
aggirare il problema. Nessun DDL MT5A. ER_TEST_STRICT_SESSION=1 sulle sole sessioni test.
GLOBAL sql_mode e schema finale identici al preflight: hash dei due file
`47C0A5832450B571BC929AE3C4FFE0C0A87D2531610BDE35348E668A8611AF1B`.

Baseline reale: **51 Passed, 624 Failed, 0 Skipped, totale675**. Il confronto dei nomi TRX
dimostra esattamente le stesse675 identita di MT4C final-2, non una baseline sostitutiva.
[TRX baseline](TestResults/mt5a/baseline/), [verifica](TestResults/mt5a/verification.json).

## 3. Inventario identity/membership

Inventario completo per componente, identita, ruoli, owner, stato, lettori/writer,
vincoli e rischio nella [governance](../../docs/architecture/INSTITUTION_MEMBERSHIP_GOVERNANCE.md).
Esaminati User, Institution, membership/ruoli/profili, controller identita e legacy,
InstitutionAuthManager, InstitutionAuthentication, resolver, CurrentTenant/ICurrentTenant,
JWT, regole MT2 e LegacyEndpointGate. Nessuna seconda tabella User o autenticazione duplicata.

I repository MT1 mantengono metodi tecnici Add/Suspend/AddRole, senza last-admin/audit:
non vengono aperti come API in MT5A. Il gate conserva le73 route legacy; la route storica
globale /api/Users esiste ancora nel ramo legacy, ma non diventa una directory istituzionale
e rimane inaccessibile ai JWT istituzionali. Non si dichiara inesistente una route storica.

## 4. Ruoli e autorizzazioni

InstitutionAdmin puo leggere directory/dettaglio del tenant corrente. Teacher/Student,
anche combinati o con User.Role=admin, non possono. Admin non implica Teacher/Student
ne i rispettivi profili; questi sono richiesti solo dai percorsi scolastici pertinenti.
Membership puo avere piu ruoli, serializzati come lista, senza conversione a UserRole legacy.
Stato membership Active e Institution Active richiesti per operazioni interattive.

## 5. User globale condiviso

Fixture identity-only temporanea A/B: sei User globali, undici membership distinte.
Stesso User admin in A e Teacher in B; altro User Teacher A/Student B e viceversa;
multiruolo Teacher+Student, membership sospese e admin B dedicato.
Nessuna duplicazione per tenant, somma ruoli A/B o modifica credenziali.
Non servono profili scolastici per amministrare la directory; questo e provato anche
con Admin senza profili. I test storici dei resolver/profili restano presenti ma bloccati
dal preflight generale: la nuova fixture non sostituisce le loro garanzie.

## 6. Repository e letture SQL

Nuovo IInstitutionMembershipDirectoryRepository, implementazione interna scoped con DbContext
esistente. Tutte le query iniziano da InstitutionId non vuoto. Dettaglio con AND MembershipId;
nessun metodo globale, IgnoreQueryFilters o lettura di tutti gli User da filtrare in memoria.
AsNoTracking/AsSingleQuery e proiezione SQL di soli campi necessari, ruoli con JOIN e filtro
ruolo EXISTS. Ricerca letterale Email solo di User associati tramite membership del tenant.
Pagine default20/max100; offset Int32 verificato, ordine JoinedAt/Id. Nessun count necessario.

## 7. Manager

IInstitutionMembershipAdminManager risolto dalla DI reale. Riusa InstitutionSchoolAuthorization
AdminAsync, che acquisisce il solo ICurrentTenant inizializzato dall'handler MT2, rivalida
membership/Institution/set ruoli attraverso InstitutionAuthManager e richiede InstitutionAdmin.
Non riceve InstitutionId client, non ricostruisce claim, non chiama repository User globale.
Uso diretto da scope senza TenantContext negato, verificato nei test.

## 8. Endpoint effettivi

| Metodo | Route | Accesso |
| --- | --- | --- |
| GET | /api/institution/admin/members | lista locale paginata; filtri status/role/search |
| GET | /api/institution/admin/members/{membershipId:guid} | dettaglio locale |

200 incluso [];400 filtri/binding non validi;401 JWT assente/non valido;403 ruolo/stato/
revoca/rollout;404 GUID valido inesistente o fuori tenant con identico corpo.
Segmento non GUID non corrisponde alla route. Nessun POST/PUT/PATCH/DELETE nuovo, nessun
invito/Institution/register-admin o ricerca globale utenti. Metodi di scrittura sulle due
route rispondono405; controllata anche la metadata MVC.
[Contratti frontend](../../docs/api/MEMBERSHIP_ADMIN_FRONTEND_CONTRACTS.md).

## 9. JWT, policy e gate

Policy InstitutionMembershipAdminAccess: solo schema InstitutionAccess, autenticazione,
InstitutionRequirement MT2, ruolo InstitutionAdmin e flag. Selection/legacy/scaduto negati401.
Marker InstitutionMembershipAdminEndpoint verificato dal gate esistente insieme a policy
esatta, autenticazione e TenantContext; marker combinati/duplicati, policy/schema alternativi,
AllowAnonymous e assenza contesto negati. Nessuna autorizzazione per prefisso URL.
JWT/parser/rivalidazione MT2 non sostituiti o allentati.

## 10. Rollout

`Multitenancy:MembershipAdminApi:Enabled` default false (anche assente/non valido).
Indipendente da SchoolApi/CourseApi/ExamApi/InstitutionWorker perche la directory gestisce
appartenenza istituzionale, non accesso didattico. Il flag non assegna ruoli.
Test positivi con solo MembershipAdminApi true e flag didattici false; test chiusi su entrambe
le action. Solo appsettings.example.json esteso con false, nessuna configurazione operativa.

## 11. Isolamento A/B

Program.cs reale, Kestrel, JWT da login/selezione veri, database reale senza mock repository.
Admin A vede5 membership A, Admin B vede6 membership B; membership condivise distinte,
dettaglio esterno uguale a mancante; paginazione locale deterministica a dati invariati.
Filtri stato/ruolo/ricerca scoped, email presente solo in B non enumerabile in A, percentuale
di ricerca trattata letteralmente; InstitutionId query non cambia il tenant.

La fixture indipendente non chiama TestDatabaseSchema/SeedData e non finge che il DB abbia MT4.
Serve a verificare le nuove sole letture sulle tabelle identita gia esistenti; non e una
certificazione MariaDB o dei moduli scolastici/worker.

## 12. Revoca

Membership sospesa e Institution sospesa negano entrambe le action403; rimozione admin e
aggiunta Teacher dopo emissione invalidano il vecchio token per uguaglianza esatta dei ruoli.
Il token B rimane valido. Dopo aggiunta, nuova selezione produce il set corrente e consente
lettura se admin e ancora presente. Nessuna cache autorizzazioni.
Mutazioni di fixture avvengono solo su record temporanei, prima del digest read-only.

La policy MT2 non e una revoca monotona permanente: ripristinare stato e identico set ruoli
puo rendere nuovamente valido un JWT non scaduto. Nessun nuovo meccanismo inventato; decisione
esplicita prima delle mutazioni MT5B. Una richiesta gia autorizzata non viene annullata
retroattivamente: i futuri writer richiedono rivalidazione nello stesso confine transazionale.

## 13. Privacy e assenza scritture

DTO dedicato MembershipId/UserId/Email/Status/Roles/JoinedAt/SuspendedAt reali. User non ha
nome, quindi nessuna anagrafica inventata o risolta da profili legacy/B. Solo Email di membri
gia collegati. PasswordHash, token, provider credentials, ruoli/link globali e profili esclusi.
Test confronta l'insieme esatto delle proprieta JSON, non solo assenza di una stringa nota.

Digest SHA256 prima/dopo le richieste su tutte le colonne identity di User, Membership e Role,
ordinamento canonico, ogni volta un nuovo DbContext/no tracker; valori hash/password restano
solo in memoria. Assertion di uguaglianza nei test positivi/negativi e nelle prove revoca dopo
la preparazione della mutazione. Nessuna scrittura prodotta dalle nuove HTTP o query repository.
[Stato finale](TestResults/mt5a/final-db-state.txt): zero membership/ruoli/profili/tenant/utenti
temporanei,15 User presenti, holding Provisioning; schema/GLOBAL invariati.

## 14. SQL generato

[Cattura ed EXPLAIN](TestResults/mt5a/directory-sql.json), sei operazioni: lista, stato, ruolo,
ricerca, pagina2 e dettaglio. Ogni HTTP autorizzata produce esattamente3 SELECT osservate:
autorizzazione handler MT2, rivalidazione manager, query risorsa. Non comprende SET SESSION,
handshake o login/selezione eseguiti prima della cattura. Nessun N+1 o SELECT *.

La lista EF limita le membership in una derived table con WHERE institution_id e LIMIT/OFFSET
prima del JOIN dei ruoli. Ricerca aggiunge il join User locale prima del paging quando necessario.
Il dettaglio ha institution_id AND id; ruoli della sola membership selezionata. La proiezione
risorsa non seleziona password_hash. AsNoTracking verificato con ChangeTracker vuoto.

## 15. Performance

EXPLAIN eseguito sul SQL realmente catturato con parametri in memoria, nuovi comandi read-only.
Sul campione5/6 membership, lista usa prefisso tenant dell'indice uq_membership_institution_id,
User per PRIMARY eq_ref e ruoli per PK. Compaiono filesort e temporary sulla pagina/ordinamento;
il filtro stato non ha indice dedicato e la ricerca sottostringa non promette indice email.
Dettaglio risolto con indici owner/Id e PK. Nessun nuovo indice speculativo/DDL/Redis/cache.
Pagine profonde e volumi reali richiedono misure prima del rollout; nessun benchmark p95 o
throughput produttivo rivendicato, ne equivalenza dei piani con MariaDB non disponibile.

## 16. Test aggiunti e adeguamenti inventari

Nuova Mt5aMembershipAdminTests: **36 casi** (12 filtri invalidi,4 negazioni ruolo,4 revoche,
2 bootstrap,10 gate, directory/dettaglio, filtri/pagine, token negativi e SQL).
Targeted-3:36/36 Passed, zero Failed/Skipped; [TRX](TestResults/mt5a/targeted-3/).
Targeted precedenti conservati: iniziale interrotto per timeout host, diagnostico errore EventLog,
diagnostic-2 ordinamento enum SQL, targeted-2 22/23 per whitespace normalizzato a null da MVC.
Corretti helper di avvio, ordinamento finale dei ruoli e distinzione filtri assenti/vuoti,
senza indebolire assertion. EventLog disabilitato solo nell'host test; Program operativo invariato.

Inventari MT2/AuditBootstrap/MT3D/MT4B estesi alla nuova categoria mantenendo73 legacy,
24 School,33 Exam,19 Course e154 action precedenti. Nuovo totale156 MVC,2 membership;
Development ha anche5 metadata. Nessun test rimosso, Skip introdotto o autorizzazione indebolita.
I vecchi inventari non sono dichiarati passati: le prove dipendenti dal preflight sono bloccate.
Le nuove prove bootstrap verificano policy/schema e le due GET a runtime in entrambi gli ambienti.
[Route Production](TestResults/mt5a/routes-Production.json),
[Development](TestResults/mt5a/routes-Development.json).

## 17. Due suite definitive e build

Build completa con `--no-restore` riuscita; build incrementale finale0 warning/0 errori.
Ricompilazioni precedenti mostrano11 nullable warning preesistenti, nessun nuovo warning.
[Build](TestResults/mt5a/final-build.txt). Sorgenti congelati prima delle suite e verificati
immutati fra/prima/dopo tramite SHA256, senza modifiche per mascherare i risultati.

| Esecuzione | Unit Passed | Integration Passed | Failed | Skipped | Totale |
| --- | ---: | ---: | ---: | ---: | ---: |
| MT4C storico | 51 | 624 | 0 | 0 | 675 |
| Baseline MT5A | 51 | 0 | 624 | 0 | 675 |
| Finale1 | 51 | 36 | 624 | 0 | 711 |
| Finale2 | 51 | 36 | 624 | 0 | 711 |

Due suite consecutive **eseguite ma NON superate**. Tutti i624 Failed incontrano il preflight
schema mancante gia osservato alla baseline. Non e certificata la regressione MT3/MT4/worker.
I675 nomi test precedenti sono tutti preservati, stessi36 nuovi Passed nei due run.
[TRX finale1](TestResults/mt5a/final-1/), [finale2](TestResults/mt5a/final-2/),
[verifica contatori/identita/hash](TestResults/mt5a/verification.json),
[script riproducibile](TestResults/mt5a/Verify-Evidence.ps1).

## 18. Last Admin

Formalizzato in governance: almeno una membership Active con InstitutionAdmin e User valido
nel tenant; enumerati sospensione/left/delete/rimozione o sostituzione ruoli, trasferimenti,
blocco identita globale e lifecycle Institution. Nessun admin automatico o membership fittizia.
MT5B deve serializzare tutti i writer su SELECT Institution(owner) FOR UPDATE, poi membership
ordinate, ruoli ordinati e inviti; current reads dopo lock, calcolo effetto residuo locale e
mutazione/audit atomici. Due revoche simultanee non devono entrambe lasciare zero admin.
Non e implementata una finta protezione solo in memoria o nei writer tecnici MT1.

## 19. Audit membership

Progettato ledger distinto da course_administration_event. Eventi membership creata/sospesa/
riattivata, ruolo aggiunto/rimosso, inviti creati/accettati/revocati/scaduti e gestione last-admin.
Tenant, actor User+Membership, target membership, Action, Before/After minimo, UTC, RequestId,
motivo; riferimenti tipizzati per inviti prima della membership, niente token/password.
Atomicita con mutazione, append-only/idempotenza/retention e attore bootstrap da finalizzare
in MT5B. Nessun DDL/evento retroattivo o evento didattico improprio.

## 20. Inviti e onboarding

Workflow proposto: verifica legale/delega -> provisioning controllato -> primo admin verificato
e approvato -> challenge email e autenticazione globale -> accettazione esplicita per tenant ->
membership/ruoli/audit atomici. Account esistente in B rimane unico, senza trasferire privilegi.
Inviti monouso con hash/scadenza/revoca/riemissione, esiti pubblici anti-enumerazione e nessun
collegamento automatico per email. Ruoli mai scelti liberamente dal destinatario.
Autorita legale, bootstrap, recupero, concessione admin, durata/rate limit, privacy/retention
e revoche permanenti richiedono decisione; relative API chiuse, nessun SuperAdmin didattico.

## 21. Rischi aperti e limiti

Blocco principale: ambiente MariaDB/schema MT4C non presente. Nessuna migrazione autorizzata
automaticamente in MT5A per riconciliarlo. I test mirati MySQL non provano MariaDB ne i675
test verdi. CORS/TLS/restore/segreti/rate limit/monitoraggio restano gate preesistenti.
Last-admin e audit membership sono progetto, non protezione dei futuri writer gia attiva.
Ruoli ripristinati e JWT vecchio richiedono decisione esplicita per una revoca monotona.
Piani su fixture piccole e paginazione offset non certificano carico o snapshot cross-request.

## 22. Prerequisiti MT-5B e conclusione del checkpoint

Prima di dichiarare MT5A completata: predisporre separatamente il solo database test MariaDB
autorizzato con schema MT4C verificato, ripetere baseline675 e ottenere due suite711/711 verdi,
inclusi MT3E worker, MT4C audit/concorrenza e vecchi gate. Mantenere rollout chiuso.
Per MT5B servono inoltre decisioni governance, lock last-admin e prove concorrenti,
audit membership atomico distinto, idempotenza e workflow inviti/provisioning verificato.
Nessun MT5B iniziato, nessun cutover, scrittura amministrativa pubblica o modifica globale User.
`git diff --check` superato; modifiche locali lasciate revisionabili senza commit.
