# ElectronicRegister — MULTITENANCY MT-3 PARZIALE

**NON COMPLETATA. Gate multi-tenant scolastico chiuso. Nessun cutover autorizzato/eseguito.**
Sono consegnati inventario, piano di ownership e fondazioni SQL/EF/application per i
profili istituzionali. Non viene presentato come isolamento completo dell'applicazione.

| Checkpoint | Stato effettivo |
| --- | --- |
| A — ownership e schema | PARZIALE: due collegamenti profilo, FK composite e piano restante; ownership delle 21 tabelle non migrata |
| B — profili/autorizzazioni | PREPARATORIO e testato: risolutore scoped, non adottato dai manager didattici |
| C — repository/CRUD | NON MIGRATO; nessuna route scolastica nuova abilitata |
| D — esami | NON MIGRATO; regole/lock/grading originali preservati |
| E — worker/cutover | NON MIGRATO; worker ancora single-tenant, nessun principal globale introdotto |

## 1. Stato Git iniziale

Branch refactor-database-entities, HEAD `970d5d704283daf0c33e106b1ff17149360cfcf4`.
Conservate tutte le modifiche non committate audit/MT1/MT2, incluse le nuove entity,
sicurezza, test e report. Nessun reset/checkout/pull/commit, cancellazione di file
non tracciati o modifica frontend. Stato iniziale nelle evidenze `initial-git-status.txt`.
Letti i cinque documenti richiesti e ricostruiti DbSet, repository, route, DTO e percorsi
raw/background dal codice corrente. La baseline non e dedotta dal remoto.

## 2. Baseline locale

Build prima delle modifiche riuscita. **48 unit + 279 integration = 327/327 Passed**,
zero Failed/Skipped, MySQL reale electronic_register_test. TRX distinti in
[baseline](TestResults/mt3/baseline/). MySQL 5.7.24, REPEATABLE-READ, SQL mode strict;
preflight struttura/indici/FK in [schema-before.txt](TestResults/mt3/schema-before.txt).

## 3. Matrice delle tabelle e percorsi

[MULTITENANCY_ISOLATION_MATRIX.md](../../docs/architecture/MULTITENANCY_ISOLATION_MATRIX.md)
elenca le 25 tabelle iniziali e le due nuove: ownership attuale/proposta, repository,
famiglia endpoint, metodi, DbSet, DTO Guid, attributi route/ruoli, raw SQL e caller legacy.
User e identita globale intenzionale; le quattro strutture MT1/MT2 hanno scope proprio;
le 21 tabelle scolastiche legacy restano da migrare (User incluso nell'inventario legacy
ma non da trasformare in identita tenant). Il ponte submission_answer_option non ha
DbSet esplicito ma viene incluso, con percorso Answer/Question/Option.

La matrice e inventario statico e piano, non dimostrazione che ogni query sia gia
isolata. StudyArea/StudyPath non hanno controller pubblici ma sono repository interni
da migrare. Worker/processor sono processi tecnici privilegiati da confinare per item.

## 4. DDL implementato

`migrations/mt3/001_profile_expand.sql`:

- UNIQUE(institution_id,id) su institution_membership, giustificato dalla FK composita.
- institution_student_profile: PK(institution_id,membership_id), UNIQUE(student_id),
  FK composita membership e FK Student, tutte RESTRICT.
- institution_teacher_profile: stesso modello, UNIQUE(teacher_id).

Un profilo puo appartenere a una sola membership/fondazione; una membership puo avere
entrambi i profili senza duplicare User o credenziali. InstitutionId e quello del
collegamento membership, non del client. FK composita impedisce coppie istituto/
membership incoerenti; UNIQUE impedisce riuso dello stesso profilo in due fondazioni.
Nessuna nuova colonna sulle entity legacy, nessun cambio di FK didattica, nessuna modifica
dei UNIQUE globali Subject/StudyArea/StudyPath/Biennium: questi restano requisiti aperti.

Sorgente SQL versionabile e database-first. Due entity nuove scaffolded dal test DB;
mapping derivato dal candidato in ElectronicRegisterContext.Mt3Profiles.cs, relazioni
unidirezionali per non modificare Student/Teacher/Membership generati. Hook aggiunto
nel sidecar MT1, non nel context generato legacy. Script Scaffold-Mt3.ps1 ripetibile;
non copia i parent candidati. Rigenerare MT1 richiede riapplicare hook MT3 come documentato.

## 5. Backup, backfill e preservazione dati

Prima del DDL: verificati DATABASE(), versione, SQL mode, tipi/collation Guid e FK.
Mysqldump completo con snapshot InnoDB esportato nella cartella privata ignorata
`Backend/ElectronicRegisterAPI/mt3-evidence/private/before-profile-expand.sql`.
Controllate dimensione, leggibilita, CREATE TABLE e marker di completamento; SHA-256 in
[backup-sha256.txt](TestResults/mt3/backup-sha256.txt). Nessun dump/hash password nei report.

Mt3ProfileBackfill e utility esplicita non registrata in DI/Program. Rifiuta database
diversi da electronic_register_test prima della connessione, ricontrolla DATABASE(),
richiede Guid esplicito del record LEGACY_MT1 e membership/ruoli compatibili. SQL
`002_profile_backfill.sql` incorporato come risorsa; nessun provisioning automatico.
Inserisce solo collegamenti ai profili gia esistenti conservando i Guid. Preflight
ambiguo/incompatibile abortisce; collisione UNIQUE/FK annulla tutte le DML. Rerun idempotente.

Prova controllata: backfill membership MT1 seguito dal collegamento MT3, confronto
conteggi e SHA-256 di **ogni cella** delle 21 tabelle legacy, incluse PK, password hash,
esami/recuperi, risposte, Grade e Audit. Verifiche da nuovi scope/connessioni; nessun
confronto limitato a Count o entity tracked. Collisione dopo inserimenti preliminari
dimostra rollback di entrambi i tipi di collegamento. Ruolo incompatibile non normalizzato.

Nessun backfill permanente eseguito. Studenti/docenti senza User non vengono assegnati
a membership inventate; ownership dell'intero grafo e ambiguita storiche restano aperte.
Teardown rimuove soltanto dati delle prove, conserva lo schema nuovo. Il backfill dei
profili NON e backfill di Exam/Grade/Class/Audit.

DDL MySQL 5.7 non atomico: lo script non e un down/retry automatico. Backup export
verificato ma **restore completo non provato**; non effettuato DROP/ripristino distruttivo
delle tabelle legacy. Recovery/cutover richiedono prova controllata e finestra dedicata.

## 6. Profili Student/Teacher

InstitutionProfileRepository ricerca in SQL con InstitutionId, MembershipId e UserId
tramite membership. IInstitutionProfileResolver risolve separatamente i due ruoli dopo
verifica di membership/istituto/insieme ruoli tramite l'autorizzatore MT2. Stesso User,
profili differenti A/B, ruoli simultanei, profilo mancante, admin senza profili e revoca
sono provati. User.TeacherId resta A ma il contesto B risolve B, mai un fallback.
Mappatura storica persiste anche dopo revoca del ruolo; il profilo non conferisce diritti.

## 7. Contesto

Riutilizzato TenantContext MT2 immutabile; nessun tenant resolver alternativo, Guid.Empty
o contesto singleton. Il risolutore prende il contesto verificabile e rilegge autorizzazione.
Non e ancora consumato dai manager didattici, che restano su ClaimsContext dietro gate.
Le nuove letture sono scoped, AsNoTracking e SQL filtrate. Richieste concorrenti A/B
del servizio hanno scope distinti; non sono prove di concorrenza didattica multi-tenant.

## 8. Autorizzazioni

MT2 conserva DB revoca, Institution Active, membership Active e ruoli esatti. Il nuovo
risolutore richiede lo specifico ruolo, non sceglie arbitrariamente un UserRole singolo.
Identificativi incoerenti, revoca, ruolo perso o profilo assente -> UnauthorizedAccessException
controllata. Nessun endpoint nuovo usa questo errore per aprire dati. Il controllo avviene
prima del lookup del profilo; non promette revoca retroattiva delle operazioni in corso.

## 9. Repository

NUOVO InstitutionProfileRepository read-only, filtrato per tenant/membership/User.
Nessun repository scolastico preesistente migrato. UserRepository CRUD globale non
diventa sicuro tramite questo collegamento. Tutti i metodi da migrare sono nella matrice;
non aggiunti Global Query Filters come sostituto della verifica SQL.

## 10. API

**Zero API scolastiche migrate/aperti nuovi endpoint.** Il gate MT2 resta invariato
su tutte le 73 route legacy e rifiuta i nuovi token. Le cinque route auth MT2 restano
distinte e membership-aware. Account management/membership amministrative tenant-scoped
non implementati; non viene inventato un SuperAdmin per colmare questa lacuna.

## 11. Test A/B

Due fondazioni Active con nomi uguali, User globale comune, due Student e due Teacher
distinti e membership multiruolo. Provata la sola risoluzione profili A/B e i vincoli
dei collegamenti. **Non** e la matrice didattica completa richiesta per MT3: mancano
tenant-owned Class/Subject/Exam/Submission/Grade e test HTTP positivi di tali flussi.
I test HTTP MT2 continuano a provare che A/B e ticket non accedono alle route legacy.

## 12. Vincoli SQL negativi

Provati sei casi reali MySQL: FK composita Institution/Membership incoerente 1452,
profilo inesistente 1452, riuso profilo di A in B 1062, per Student e Teacher.
Controllo successivo da nuovo scope della persistenza di A. Le FK non autorizzano
il ruolo e non isolano Student.ClassId o ClassSubject.TeacherId: tali vincoli restano
progettati, insieme a Grade/Exam/Submission/Retake/Answer/Option cross-tenant.

## 13. Esami, recuperi, Grade

Codice non modificato: ConvertToGrade, SuggestedGrade, Passed, review/finalize/override,
recuperi affiancati, tentativo unico, Close senza InProgress e lock rimangono invariati.
Baseline rieseguita; **nessuna garanzia di isolamento multi-tenant di questi flussi ancora
implementata**. Non vengono esposti ai token dei tenant nuovi.

## 14. Worker

Non modificato. Il batch globale 50 e scope per elemento sono garanzie single-tenant
preesistenti, non contesto interno tenant-scoped. Restano da implementare selezione
tecnica limitata, risoluzione istituto dalla submission, scope vincolato a tenant/item,
Grade/Audit coerenti e test misti A/B. Nessun falso admin o JWT per il worker.

## 15. Audit

AuditEvent non ha ancora InstitutionId. Append atomici esistenti non modificati e
regressioni preservate. Classificare/attribuire tutti gli eventi EntityId polimorfici
e trattare eventuali orfani/azioni non riconducibili prima di backfill, senza inventare
ownership o perdere JSON. Nessuna API audit globale nuova.

## 16. Legacy e gate

Configurazione MT2 Enabled/holding/allowlist/pre-backfill resta invariata, default
chiuso. Nessuna conversione token scoped -> ClaimsContext globale, nessun privilegio
derivato da User.Role sui nuovi tenant. Non utilizzare il collegamento dei profili
per aprire gradualmente route ancora senza ownership/filtro/FK.
Prima di backfill permanente disabilitare AllowUnbackfilledAccounts e adattare CRUD
legacy ai profili referenziati. Eliminazione del bridge solo dopo C/D/E e cutover
autorizzato, mai semplicemente disabilitando LegacyEndpointGate.

## 17. Concorrenza

Rieseguiti test storici Start/Close, Submit/autosave, Review/Finalize, worker multipli
e rollback. Risoluzione profili A/B contemporanea provata con scope separati.
Mancano operazioni didattiche A/B contemporanee e rollback contaminazione A/B: non
si presentano le gare single-tenant come prova equivalente. Nessun lock nuovo globale.

## 18. Performance

Risoluzione: una lettura autorizzazione MT2 + lookup profilo con PK
(InstitutionId,MembershipId) e controllo UserId via membership. Nessun N+1 di esami o
materializzazione globale. UNIQUE(profile_id) e composite membership servono integrita,
non indici speculativi. Dataset piccolo; nessun EXPLAIN su carico adeguato o benchmark
staging. Grade/worker/assigned performance multi-tenant rimane da verificare dopo migrazione.

## 19. File produttivi modificati

Sotto Backend/ElectronicRegisterAPI/src:

- Domain: Interfaces/Repositories/IInstitutionProfileRepository.cs e
  Interfaces/Services/IInstitutionProfileResolver.cs.
- Application: Services/InstitutionProfileResolver.cs; DI AddApplicationManagers.cs.
- Infrastructure: Repositories/InstitutionProfileRepository.cs;
  Persistence/Migrations/Mt3ProfileBackfill.cs; due entity InstitutionStudentProfile e
  InstitutionTeacherProfile generate; ElectronicRegisterContext.Mt3Profiles.cs derivato;
  hook nel sidecar Multitenancy.cs; DI e csproj per SQL resource.
- SQL/script migrations/mt3; tre documenti architettura aggiornati/creati.

Nessuna modifica a controller/Program/auth MT2, schema/entity legacy, worker, processor,
lock o formule. Nessuna nuova dipendenza NuGet o servizio esterno.

## 20. Test aggiunti

Mt3ProfileTests: **21 nuovi casi integration**, nessun mock di persistenza.
Risoluzione multiruolo/multifondazione, contesti contraffatti, quattro revoche/missing,
sei vincoli SQL, backfill e idempotenza con snapshot celle, collisione rollback, ruolo
incompatibile, admin reale senza profili e due database non autorizzati.
327 casi precedenti non cancellati/alterati/skip. Nessuna fixture precedente adattata.

## 21. Validazione definitiva

Build riuscita. Warning nullable preesistenti (11 al rebuild completo), nessuno nuovo
nei file MT3. Comandi dal workspace Backend/ElectronicRegisterAPI:

```powershell
dotnet build ElectronicRegisterAPI.slnx
dotnet test ElectronicRegisterAPI.slnx --no-build --logger:trx --results-directory ./mt3-evidence/final-1
dotnet test ElectronicRegisterAPI.slnx --no-build --logger:trx --results-directory ./mt3-evidence/final-2
git diff --check
```

| Esecuzione | Unit Passed/Total | Integration Passed/Total | Total | Failed | Skipped / NotExecuted |
| --- | ---: | ---: | ---: | ---: | ---: |
| Baseline | 48/48 | 279/279 | 327/327 | 0 | 0 |
| Definitiva 1 | 48/48 | 300/300 | 348/348 | 0 | 0 |
| Definitiva 2 consecutiva | 48/48 | 300/300 | 348/348 | 0 | 0 |

Contatori total/executed/passed/failed/notExecuted e SHA-256 verificati in tutti i TRX:
[verification.json](TestResults/mt3/verification.json). Tutti i 327 nomi baseline presenti
in entrambi i run; insiemi finali identici. Nessuna modifica a codice o test tra i due
run. Evidenze [final-1](TestResults/mt3/final-1/) e [final-2](TestResults/mt3/final-2/).
`git diff --check` senza errori; soltanto avviso LF/CRLF preesistente del checkout Windows.
Nessuna deduzione dal solo exit code. Bootstrap Dev/Production reale e worker inclusi nei test
preesistenti, senza disabilitarli. Queste suite validano **il checkpoint parziale**,
non le funzionalita multi-tenant didattiche ancora assenti.

Dopo teardown: zero righe nelle due tabelle profilo e zero Institution di prova;
schema aggiunto conservato. Nessun secondo tenant operativo lasciato attivo.

## 22. Rischi aperti

**BLOCCANTE per MT3/cutover:** ownership e vincoli del grafo scolastico assenti,
repository/manager/API non migrati, account administration globale, worker/audit non
tenant-aware. Mitigazione attuale: nuovi token ancora bloccati, nessun secondo tenant
operativo. Nessuna dichiarazione Production Ready.
Altri limiti: restore integrale non provato, record legacy operativo non riconciliato,
profili senza User/ambiguita storiche, carico e failure DB multi-tenant non testati.
Restano i rischi di deployment audit (HTTPS/proxy/CORS/rate limiting/MySQL supportato).

## 23. Condizioni per secondo tenant

Completare checkpoint A con manifest/backfill integrale e FK, adottare profili in B,
migrare C/D/E con controlli risorsa+tenant, test HTTP/SQL A/B su tutti i percorsi,
revoca e concorrenza, restore controllato e validazione zero skipped, gate esplicito
di rilascio e autorizzazione al cutover. Il presente stato NON soddisfa quei gate.

## 24. MT4

Non iniziata. CourseEdition/enrollment e ogni ulteriore evoluzione rimangono fuori
scope finche MT3 non e completata e verificata. Prossimo lavoro necessario e ancora
MT3, a partire dall'ownership delle tabelle legacy, non una nuova fase funzionale.
