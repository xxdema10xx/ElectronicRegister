# MT-5B-C — certificazione persistence e JWT

21 ottobre 2026, conclusione circa 13:03 Europe/Rome. **Implementazione backend e
verifica completate: 772/772 Passed, tutte le 738 identità storiche preservate.** Nessuna migrazione MT5B ripetuta,
restore, reset o modifica di trigger/FK. Nessun intervento su XAMPP o sulla 3306.

## Ambiente e protezione dei dati

Target esclusivo delle fixture: 127.0.0.1:13306, MySQL 5.7.24, hostname CES01622,
processo MAMP C:\MAMP\bin\mysql\bin\mysqld.exe, datadir
`<repo>/migrations/mt4/private/mt5a-mamp-13306/data/`, electronic_register_test.
Baseline protetta: 127.0.0.1:3306, stesso binario/versione/hostname, datadir
C:\MAMP\db\mysql. PID iniziali 2156 e 9408 rispettivamente, correlati ai listener.

Prima dei test acquisiti snapshot SQL completi di entrambe le istanze, tutti i
SHOW CREATE e metadati trigger, e un backup logico completo privato della sola
13306 con inventario, footer, exit code e SHA256 verificati. Conservato anche il
contesto/overlay SQL_MODE: il solo dump 5.7 non riproduce tutti i metadati trigger.
Il fingerprint isolato iniziale coincide con R3C; inventario 33/64/99/141.

Evidenze e credenziali rimangono nell'area privata ignorata:
`migrations/mt4/private/mt5a-mamp-13306/mt5bc-20261021/`.
Il driver verifica processo, listener e identità SQL, imposta solo la connessione
13306 nel processo figlio e non stampa password. Nessun fallback/container,
--force, SET GLOBAL o modifica di account MySQL. Fixture applicative sintetiche
identificate da GUID e codici consentiti; pulizia attraverso i trigger attivi e
le normali guardie root/owner/ambiente/codice, senza disabilitarle.

## A — certificazione funzionale della persistenza

Eseguiti sul database reale tutti i 27 casi storici Mt5bSecurityStateTests:
**27/27 Passed**. Coprono stato iniziale, ruolo INSERT/DELETE e ripristino,
status/no-op, rollback, overflow, missing-state, INSERT SELECT multirow e duplicati,
DELETE JOIN/1442, identità immutabile, retention/ricreazione, FK composite,
lettore scoped, isolamento A/B, due connessioni concorrenti senza incrementi persi,
mutex Institution e writer tecnici limitati a Provisioning.

Il backfill è stato provato con membership realmente esistenti: stato sintetico A
rimosso con capability autorizzata, B con generation nonzero; esecuzioni della
primitive INSERT SELECT inizializzano solo A e preservano B anche alla ripetizione.
Si tratta della prova DML sulle fixture, **non** della riesecuzione del file 002
o di una riparazione operativa automatica dopo l'attivazione JWT.

Aggiunti **11 casi SQL** nel medesimo file, senza rinominare i precedenti:

| Metodo | Casi | Garanzia osservata |
| --- | ---: | --- |
| Sql_SidecarIdentityAndNegativeGenerationAreRejected | 3 | Owner/id sidecar immutabili, generation negativa negata |
| Sql_MissingStateRejectsNoopsAndRoleDelete | 3 | Missing-state nega no-op status/ruolo e DELETE ruolo |
| Sql_NewStateRejectsAnyNonzeroInitialGeneration | 2 | INSERT generation -1/1 negati; rollback conserva stato originale |
| Sql_SuspendedTimestampAloneDoesNotAdvanceGeneration | 1 | Timestamp senza cambio status non incrementa |
| Sql_OverflowRejectsRoleDeleteWithoutLosingRole | 1 | Overflow nega DELETE e conserva il ruolo; rollback conserva generation |
| Sql_FixtureGuardRequiresExplicitOwnerAndSyntheticCodeAndExcludesHolding | 1 | Owner errato/codice non autorizzato negati; holding osservata solo in lettura |

I **38 casi persistence**, incluse le verifiche di mapping e superficie HTTP,
sono passati. La holding reale non è stata usata come fixture
di mutazione dal nuovo test; verificati il suo codice e l'esclusione dal predicato
sintetico. L'eccezione DBA rimane limitata alle fixture; non si dichiara protezione
contro root che alteri schema o identità della holding.

### Regressione A e correzioni del computer Windows

Primo run completo: 695 integrazione (692 Passed, 3 Failed, zero Skipped), più
51/51 unitari. Tutte le **738 identità storiche** erano presenti; gli 8 nuovi casi
già compilati portavano il totale a 746. Questo run fallito è conservato.

Correzioni ordinarie, senza cambiare le assertion di sicurezza:

- AuditBootstrapTests: EventLog Windows non scrivibile dall'account di test
  trasformava la gestione dell'errore in 500. Configurato EventLog=None nel solo
  host di test, come nelle altre fixture Program; attesa 404 mantenuta. Aggiunta
  diagnostica delle eccezioni. Riesecuzione: 2/2 Passed.
- Backup del test di ambiguità MT3A: il riferimento predefinito al client MySQL 8
  non esiste qui. Configurato il client MAMP e aggiunta l'opzione esplicita
  MYSQLDUMP_FLAVOR=MySQL57 nel helper BackupBeforeDdlAsync, evitando i flag 8-only.
  Target, credenziale privata, inventario, footer ed hash restano verificati.
  Nessuna DDL eseguita da questa correzione. Test interessato: Passed.

Dopo le riesecuzioni mirate, campagna A consolidata per ultima esecuzione di ogni
identità: **749/749 Passed**, comprendenti tutte le 738 storiche e 11 nuove.
Non viene presentato il primo run come verde né eliminato il suo esito.
Prima di iniziare B sono stati ricontrollati validator zero, metadati dei 64
trigger invariati e snapshot 3306 identico all'iniziale. Evidenza:
`phase-a-certification.json`.

## B — implementazione effettiva JWT

| File / metodo | Modifica |
| --- | --- |
| Domain/Models/TenantContext.cs | Generation immutabile; null rappresenta assenza, non zero. Record MembershipAuthorization con generation persistita nullable |
| Infrastructure/Repositories/MembershipAuthorizationRepository.ReadAsync | Un solo statement AsNoTracking/AsSingleQuery per User/membership/Institution/ruoli/generation; sidecar scoped per owner e membership, nessuna cache |
| Business/Services/MembershipAuthorizationRules.IsSelectable / Matches | Stato presente e non negativo obbligatorio; confronto generation, identità, stati attivi e insieme ruoli |
| Application/Managers/InstitutionAuthManager.SelectAsync | Emette dal solo snapshot autorizzabile persistito; nessuna generation ricevuta dal client |
| InstitutionAuthManager.AuthorizeAsync | Rilegge il persistito ad ogni richiesta protetta e nega token obsoleti o stato mancante |
| Infrastructure/Security/InstitutionTokens.Access | Claim security_generation obbligatorio, JSON Int64 numerico canonico; nessun fallback in emissione |
| Api/Security/InstitutionAuthentication.Configure / ParseTenant | Una sola generation, range 0..Int64.MaxValue; rifiuta assenza, duplicati, array, stringhe, negativi, frazioni/esponenti e overflow; selection ticket non può contenerla |

I percorsi relativi sono sotto Backend/ElectronicRegisterAPI/src nei rispettivi
progetti ElectronicRegisterAPI.*. Nessuna dipendenza Domain verso Infrastructure.
Un TenantContext descrittivo senza generation non può autorizzare o emettere JWT.
Le richieste protette confrontano generation corrente oltre a owner/user/id/ruoli:
il ripristino dei ruoli o dello status della membership non riabilita vecchi token.
User.Role globale non fornisce privilegi; LegacyEndpointGate resta fail-closed.

Login globale e selection ticket rimangono identità senza accesso didattico.
I vecchi InstitutionAccess senza generation vengono rifiutati; occorre nuova
selezione. Non esiste compatibilità permissiva v1, cache di revoche o healing
automatico di uno stato mancante. Un deployment esterno richiederebbe schema e
validator coordinati su tutti i nodi; nessun deployment è stato effettuato qui.

## Nuovi test JWT e compatibilità

`Mt5bJwtTests.cs`: **21 casi reali Passed**. Program ASP.NET Core eseguito tramite
entry point effettivo, DI/worker/middleware conservati, Kestrel loopback e MySQL
13306. JWT autentici emessi attraverso login e selezione; token malformati
risignati con la chiave sintetica del solo host di test, per esercitare il parser
senza confondere errori claim con errori di firma.

Copertura: emissione dalla generation SQL, niente privilegio dal global admin,
13 forme invalide del claim, revoca dopo ruolo/status e dopo ripristino esatto,
fresh selection, B invariato, status no-op/rollback, tenant mismatch, generation
vecchia/futura/MAX, membership assente, missing-state su token e nuova selezione,
ticket con claim vietato e blocco del gate legacy. Fault injection/recupero stato
sono esclusivamente delle fixture possedute e usano trigger attivi.

`MembershipAuthorizationTests.cs`: **2 nuovi unitari Passed**,
MissingSecurityStateOrContextNeverDefaultsToZero e SameRolesNeverReviveAnOlderGeneration,
per assenza/negatività fail-closed e revoca monotona a ruoli identici; vecchie identità e assertion mantenute.
Unitari totali ricompilati: **53/53 Passed**.

Le fixture dei servizi diretti MT3/MT3B e il token sospeso MT5A ora leggono la
generation persistita tramite `PersistedTestTenant.ReadAsync`, solo nei test.
Il helper non aggiorna un contesto che possiede già una generation e non è usato
da HTTP o produzione. Il setup di emissione è separato dalla misura delle query
del servizio: preservato il limite originale 1–4, senza aumentarlo o rimuovere
assertion. I fallimenti intermedi di compatibilità restano nei TRX privati;
i due casi corretti sono stati rieseguiti con successo prima della regressione finale.

## Regressione finale e controlli SQL conclusivi

| Run finale sui sorgenti congelati | Passed | Failed / Skipped / NotExecuted |
| --- | ---: | --- |
| Unit | 53/53 | 0 / 0 / 0 |
| Integration, MySQL reale | 719/719 | 0 / 0 / 0 |
| Totale | **772/772** | **0 / 0 / 0** |

Confronto per identità esatta con il manifest storico regression-2:
**738 presenti, tutte Passed; zero mancanti; 34 nuove** (11 persistence,
21 JWT, 2 unitari). Non è una somma di tentativi o una sostituzione di fallimenti:
questo risultato proviene dai due run finali completi, successivi alle correzioni.
La suite Integration è stata eseguita interamente senza filtro, in circa 20 minuti.
Inventario e SHA256 di tutti i sorgenti C# verificati identici prima/dopo il run.

| Evidenza privata | SHA256 |
| --- | --- |
| unit-final/result.trx | A3D15066D2C597D42331933B2622286C4B8B23B6981B2E560BA9CA556A914F69 |
| integration-final/result.trx | F14211740CB162AD2EAA22DAD11DF1BDD185BDDC965D329E68C4153A27E86963 |

Confronti e conteggi in `final-regression-verification.json`; sorgenti congelati
in `final-source-freeze.json`. Restano conservati anche i run intermedi falliti.
Build riuscita con warning di nullabilità preesistenti; nessuna assertion rimossa,
test disabilitato o identità storica rinominata.

Controlli SQL reali dopo la suite:

| Verifica | Esito |
| --- | --- |
| 13306: tabelle / trigger / FK / indici | **33 / 64 / 99 / 141** |
| Tutte le FK, comprese composite | **99 controllate, zero orfani** |
| MT4A / MT4C / MT5B 003 / MT5B 005 | **7 / 5 / 6 / 2 regole zero** |
| Colonne, chiavi, indici, DDL e contesto schema | Invariati rispetto a R3C |
| 64 corpi/SHOW CREATE/metadati trigger, inclusi CREATED/SQL_MODE/ACTION_ORDER | Invariati |
| Membership / sidecar alla chiusura | 0 / 0; nessuno stato mancante o generation invalida |
| Istituzioni sintetiche residue dei prefissi autorizzati | **0** |
| Writer, transazioni ed eventi residui | **0** |
| GLOBAL sql_mode | Invariato su entrambe le istanze |
| 3306: dati, definizioni, metadati e identità | **Identici allo snapshot iniziale**, 32/53/98/139 |

Fingerprint 3306 iniziale/finale:
`FC6DF04E520A46B4084D9007A825D40DC354CE0C231CB80A0E20133886D92F22`.
Fingerprint finale 13306:
`7D6FFC3A080ADA02D08B87BBCDBA58FBC6006F2D0584D1055D543B33021A6B90`.

Sulla sola 13306 i test storici hanno aggiornato i propri scenari persistenti
in audit_event, exam, exam_student, exam_submission, grade, submission_answer e
submission_answer_option: conteggi prima/dopo rispettivamente 10, 12, 12, 12,
4, 22, 13, senza differenza di numerosità. Non si dichiara quindi identità dei
valori delle fixture prima/dopo la regressione. Gli altri dati risultano invariati;
la 3306 non è stata usata per preparare o pulire fixture. Nessun restore o reset
automatico per riportare artificialmente il target allo stato iniziale.

Nuovo backup finale completo `after-jwt.sql`: exit code 0, footer e inventario
33 tabelle/64 trigger verificati, DDL confrontate con il server, 179.574 byte.
SHA256 **B0B7D83EEC93E174E2D276CE2C7BF8E0CD7A3899E1058C7CCDC13FD5306650C0**.
Bundle `after-jwt.sql-bundle.json` con tutti i metadati e overlay SQL_MODE/ordine,
mai eseguito; SHA256 overlay
`ED040833A0EA61DB77DFFED83F3C3A1FBEE7D00DFA80E4B9BA1A4FDAC536CA47`.
Non utilizzare il solo dump come prova di equivalenza del contesto dei trigger.
Risultati SQL e differenze degli scenari in `final-sql-certification.json`.

## Stato Git

Branch refactor-database-entities, HEAD invariato
`92cb8141b9555a36c00e517cb4cddc50d0dd7485`. Modificati i sette file backend
descritti sopra, le fixture/test citati e il contratto architetturale; aggiunti
Mt5bJwtTests.cs, PersistedTestTenant.cs e il presente report. Restano intatte le
modifiche locali preesistenti a .gitignore, i pacchetti candidati e i report precedenti.

Le cinque osservazioni SQL/HTTP storiche riscritte automaticamente dai test sono
state archiviate nell'area privata e riportate al contenuto versionato, senza
perdere le nuove evidenze. Nessun manifest storico modificato. Backup, credenziali,
TRX/log e materiale sensibile rimangono esclusi da Git; verificati hash dei
candidati e git diff --check. Nessun commit o push.

## Perimetro mantenuto

Nessuna modifica frontend, endpoint IAM di scrittura, LastAdmin, manager amministrativo,
audit IAM, workflow EventId/ExpectedRevision o UI implementati. Nessuna equivalenza
binaria agli SQL originali perduti. Nessun commit/push, nuovo server, modifica
appsettings operativi, password MySQL, trigger installati o GLOBAL sql_mode.
Aggiornato il contratto architetturale per descrivere il nuovo comportamento JWT.

**MT-5B-C JWT IMPLEMENTED AND VERIFIED**
