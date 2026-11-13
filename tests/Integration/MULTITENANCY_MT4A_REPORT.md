# ElectronicRegister - MT-4A

**MT-4A completata nel perimetro Domain/Infrastructure/SQL: due suite consecutive 607/607 Passed,
zero Failed/Skipped, tutti i 577 test precedenti preservati. Nessun cutover; MT-4B non iniziata.**
[Modello funzionale](../../docs/architecture/ITS_ACADEMY_COURSE_MODEL.md). Le decisioni amministrative
sospese sotto non vengono risolte inventando storico o aprendo API.


## 1. Baseline e Git

Branch refactor-database-entities, HEAD iniziale nel file [initial-head](TestResults/mt4a/initial-head.txt).
Workspace gia modificato MT3C/D/E e frontend .vscode preservato. Nessun reset/checkout/commit/push.
Build iniziale passata, zero errori e warning. Baseline originaria documentata 51 unit + 526 integration.
Prima esecuzione interrotta dopo due errori bootstrap dovuti al provider Windows EventLog senza permessi.
Due bootstrap ripetuti con Logging__EventLog__LogLevel__Default=None: 2/2 Passed.
Seconda esecuzione completa: 51 unit Passed, 525/526 integration Passed, zero Skipped; unico errore:
client mysqldump predefinito assente. Configurazione supportata esistente MYSQLDUMP_EXE verso XAMPP
e MYSQLDUMP_FLAVOR=MariaDB usata per la nuova baseline completa: **577/577 Passed**, 51 unit e 526 integration, zero Failed/Skipped.
[Baseline corretta](TestResults/mt4a/baseline-corrected/), [identita 577 casi MT3E identiche](TestResults/mt4a/baseline-verification.json).
Nessuna assertion indebolita.
Teardown dei soli due tenant e tre User temporanei della mia esecuzione interrotta, tramite ID/email
verificati: [conteggi](TestResults/mt4a/interrupted-test-cleanup.txt). Nessun account operativo rimosso.

## 2. Inventario precedente

StudyArea/StudyPath: cataloghi scoped. Biennium: StartYear/EndYear DateOnly, non offerta concreta.
BienniumStudyArea associa intervallo/area; BienniumStudyPath associa quell'organizzazione e StudyPath.
Class richiede BienniumStudyPath. ClassSubject collega gruppo/materia/docente. Student.ClassId nullable
rappresenta assegnazione attuale. Institution e tenant; Campus e sede locale. Membership e ruoli danno
accesso all'identita globale User; InstitutionStudentProfile e collegamento opzionale al profilo locale.
Nessuna tabella documenta date di iscrizione o cronologia di gruppi/sedi. Verificati Domain e schema
reale MariaDB nel [preflight](TestResults/mt4a/preflight.txt); documenti obbligatori MT3E/design/migration/
isolation/release gate/contratti esami esaminati.

## 3. CourseEdition

Istanza concreta locale di StudyPath: ID/owner/percorso immutabili, code max64 e name max200;
UNIQUE(InstitutionId,Code), stessi codici consentiti A/B. Date opzionali, ordine valido se entrambe note;
nessun obbligo di due anni o codice ministeriale. Stati tecnici planned/active/completed/cancelled,
nessun workflow amministrativo inventato. CreatedAt/UpdatedAt DB non retrodatati.

## 4. StudyPath, Biennium e Class

Class mantiene il legame legacy obbligatorio e aggiunge CourseEditionId nullable shadow. Edizione con
piu gruppi, classe con zero/una attribuzione. Trigger verificano stesso tenant e percorso, proteggendo
anche successive modifiche del percorso del BienniumStudyPath gia attribuito. Nessuna conversione
Biennium -> edizione; classe attribuita non viene spostata implicitamente ad altra edizione.

## 5. Campus

Ponte CourseEditionCampus, PK tenant/edition/campus, due FK composite RESTRICT. Zero o piu sedi,
nessuna primaria. Una sede serve piu edizioni. Duplicati e trasferimento owner rifiutati; cancellare una
sede usata non cancella il ponte. Nessun metodo unlink/delete introdotto.

## 6. StudentEnrollment

Iscrizione individuale indipendente da ClassId. Owner/Student/edition/data iniziale immutabili,
CompletionDate opzionale >= EnrollmentDate. Stati enrolled/completed/withdrawn/cancelled.
Una relazione logica per tenant/Student/edition; iscrizioni a edizioni differenti conservate, anche
contemporanee. Nessun account o membership necessari per creare l'iscrizione.

## 7. Storicizzazione

Date conosciute e stato corrente per ciascuna iscrizione; nessuna cancellazione al cambio stato o
alla conclusione dell'edizione. Non e un log append-only di tutte le transizioni. Nessuna falsa
cronologia ricostruita da Student.ClassId. Episodi ripetuti nella stessa edizione, audit con actor e
rettifiche richiedono policy MT4B; alternative nel modello funzionale. Nessuna motivazione obbligatoria.

## 8. Vincoli SQL

Script versionati in migrations/mt4. Trigger SIGNAL, senza dipendenza da CHECK MySQL 5.7, per
stati/date/valori vuoti e identita immutabili. Indici scoped per codice, iscrizione, lookup di edizione,
percorso, sede e gruppi. Nessun indice status speculativo. Nessun GLOBAL sql_mode modificato.

## 9. FK composite

Edition -> StudyPath; ponte -> edition/Campus; enrollment -> Student/edition; Class -> edition.
FK diretta edition -> Institution. Tutte ON DELETE/UPDATE RESTRICT. UNIQUE campus tenant/id
aggiunta; chiavi MT3A parent preservate. information_schema verifica nomi, ordine colonne, parent e regole esatte delle sette FK, sei composite.
Test negativi reali superati.

## 10. Mapping EF

Scaffold selettivo delle tre nuove entity, nuovo sidecar ElectronicRegisterContext.Mt4.cs e hook nel
partial MT1, senza overwrite delle entity legacy. Reconcile-Mt4.py rende la riconciliazione ripetibile.
FK composite scaffoldate, owner shadow MT3A di StudyPath/Student configurati per nome nelle
principal key EF senza cambiare le entity legacy. TemporaryGuidValueGenerator evita Guid client
permanenti su questi owner chiave: i valori temporanei EF non vengono persistiti e il trigger deriva
l'owner effettivo. Nessuna Institution fittizia o scorciatoia di autorizzazione. Writer legacy verificati
con test dedicati e suite preesistente. RESTRICT anche nel mapping, senza duplicare le relazioni legacy.
Owner concurrency token e query AsNoTracking. Nessun global filter usato come autorizzazione.

## 11. Backfill

002 e intenzionalmente senza DML: manca manifest verificato. Legacy Class.CourseEditionId resta NULL;
zero edizioni/iscrizioni fittizie e zero date inventate. Rerun non scrive. Snapshot colonne originali e
SHA256 di tutte le righe prima/dopo migrazione verificati su **28 tabelle preesistenti**, identici:
[prima](TestResults/mt4a/legacy-before.json), [dopo](TestResults/mt4a/legacy-after.json).
Nessuna cella/hash password pubblicata. Backup completo leggibile con tutte le definizioni e SHA256
prima del DDL: [preflight e digest](TestResults/mt4a/expand.txt), dump privato ignorato da Git.
Il validatore offline MT3A classifica le tre nuove tabelle dopo verifica grafo; altre tabelle sconosciute
restano rifiutate. Test di due backfill MT3A con grafo MT4 attribuito passato. Anche 002 eseguito due volte senza
scritture, con stesso unico gruppo legacy irrisolto: [run 1](TestResults/mt4a/backfill-1.txt),
[run 2](TestResults/mt4a/backfill-2.txt). VALIDATE: sette controlli zero violazioni.

## 12. Compatibilita legacy

Nessun consumer HTTP, nessun cambiamento intenzionale a ExamManager/processor/GradeManager/worker,
formule/JWT/gate/API Student-Teacher-Subject/frontend/randomizzazione. Nuovo repository interno
ICourseStructureRepository registrato separatamente, senza overload globale. Tre flag default false.
Holding MT3A_LEGACY_TEST Provisioning. Nessun tenant reale o dato operativo creato.

## 13. Test A/B

Mt4aCourseStructureTests crea due fondazioni Active temporanee, percorsi/gruppi/studenti/sedi/edizioni
distinti. Verifica owner, codici scoped, duplicati, FK SQL negative, piu sedi e riuso sede, coerenza
percorso/gruppo, null legacy, enrollment a edizioni differenti e diniego dei riferimenti esterni.
DbContext nuovi nelle riletture; teardown dei soli grafi temporanei. **30 nuovi casi passati**, insieme
a 21 casi legacy selezionati: [targeted-5](TestResults/mt4a/targeted-5/).

## 14. Integrita storica

Prove superate su cambio stato/edizione conclusa, unicita relazione, identita immutabile, cancellazione
Student/Campus/edition protetta, Student.ClassId e link legacy immutati. Snapshot migrazione include
anche Exam/Grade/Submission e colonne originali di tutte le tabelle preesistenti.

## 15. Concurrency e SQL

Task.WhenAll con scope DI/DbContext A/B distinti per iscrizioni e riletture. Interceptor SQL controlla
WHERE InstitutionId e parametro owner prima della materializzazione per ogni nuova query di lettura.
Nessuno stato tenant statico. Non si certifica un workflow concorrente di transizioni sulla stessa
iscrizione: policy e gestione conflitti restano prerequisiti dei manager MT4B.

## 16. Risultati completi

Baseline: 577/577 Passed. Migrazione/VALIDATE/snapshot e targeted: superati. Build finale: zero
errori; warning nullable preesistenti nella ricompilazione dei test. Prima suite completa **607/607 Passed**; tutti i 577 nomi precedenti preservati, 30 nuovi.
[TRX final-1](TestResults/mt4a/final-1/), [verifica identita/hash](TestResults/mt4a/final-1-verification.json).
Seconda suite **607/607 Passed**, senza cambiamenti ai sorgenti tra le due esecuzioni.
[TRX final-2](TestResults/mt4a/final-2/), [verifica completa](TestResults/mt4a/verification.json):
contatori reali, tutti gli outcome Passed, zero Skipped, insiemi identici, 577 precedenti preservati,
SHA256 dei sei TRX baseline/finali. [Hash sorgenti](TestResults/mt4a/final-source-verification.json):
nessun cambiamento dei sorgenti congelati; tre entity nuove identiche byte per byte allo scaffold DB.

| Esecuzione | Unit Passed | Integration Passed | Failed | Skipped |
| --- | ---: | ---: | ---: | ---: |
| Baseline corretta | 51 | 526 | 0 | 0 |
| Targeted-5 | - | 51 (30 nuovi + 21 legacy) | 0 | 0 |
| final-1 | 51 | 556 | 0 | 0 |
| final-2 | 51 | 556 | 0 | 0 |

Conservati anche i tentativi falliti: targeted-1 (due indici convenzionali EF), targeted-2 (Description
obbligatoria omessa dalla nuova fixture), targeted-final (generazione Guid owner sui nuovi alternate
key EF), oltre ai log delle correzioni di compilazione shadow key. Corretti mapping/generazione/fixture,
senza cancellare test, saltare casi o indebolire assertion. Targeted-3 era 50/50 prima dell'aggiunta del
caso dedicato ai writer legacy; targeted-5 include anche tale caso ed e 51/51.
[Guard e riproducibilita](TestResults/mt4a/scaffold-guards.json): Apply parziali MT1/MT3 respinti prima
della copia, riconciliazione MT4 riproduce tutti gli hash persistence identici.
TRX distinti in TestResults/mt4a. verify_evidence.py ha confrontato contatori, singoli outcome e identita
con i 577 casi MT3E, senza dedurre successo dall'exit code. Bootstrap reale Development/Production passato in entrambe le suite, con DI/route/auth e arresto
ordinato del Program reale: [evidenza](TestResults/mt4a/bootstrap-rollout-verification.json).
Tutti i casi worker MT3E e gli altri test precedenti restano nella suite e sono Passed.

[Stato DB finale](TestResults/mt4a/final-db-state.txt): sola holding Provisioning, nessun tenant
provvisorio, zero righe MT4/Campus/membership/ruoli/profili, zero righe scolastiche/audit fuori holding,
GLOBAL sql_mode identico al preflight. Le 40 trigger MT3A restano presenti, piu otto MT4; sette nuove
FK, sei composite, con colonne/ordine/parent e RESTRICT effettivi. [Controlli dati](TestResults/mt4a/final-db-verification.json),
[VALIDATE finale](TestResults/mt4a/validate-final.txt): sette controlli zero violazioni.
Tre rollout default false verificati sull'esempio versionato; nessun appsettings operativo modificato.

Git diff --check e whitespace dei nuovi sorgenti/documenti/script superati:
[controlli finali](TestResults/mt4a/git-diff-check.txt), [Git finale](TestResults/mt4a/final-git-status.txt).
Nessun commit/push. Le modifiche MT3C/D/E e frontend preesistenti sono preservate.

## 17. Rischi aperti

DDL con commit implicito e possibile stato parziale: backup completo verificato, nessun DROP/down o
retry cieco. Restore/RPO/RTO non certificati. Solo electronic_register_test autorizzato. MariaDB
10.4.32 ambiente effettivo; compatibilita MySQL progettata, non attestata come esecuzione su altra
versione. Regole di accesso future, carico e deployment restano soggetti al release gate MT3E.

## 18. Decisioni funzionali sospese

Manifest classi/edizioni; episodi multipli nella stessa edizione; transizioni/riattivazione/rettifiche;
audit di stato con actor/motivazione; eventuali intervalli di assegnazione a gruppi e sedi. Nessuna
scelta viene presentata come obbligo normativo o usata per inventare storia retroattiva.

## 19. Prerequisiti MT-4B

Confermare policy sopra, autorizzazioni con membership/ruoli rivalidati, controllo conflitti di stato,
contratti API e test HTTP dedicati. MT4B non iniziata; tre flag e gate operativi restano chiusi.
