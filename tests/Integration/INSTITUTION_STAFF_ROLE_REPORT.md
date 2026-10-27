# Institution Staff — implementazione e verifica

**Stato: Fase 1 completata e verificata.** La certificazione combina le prove .NET/MySQL
eseguite direttamente dall'agente nella sandbox con i validatori Python eseguiti manualmente
dall'utente sulla PowerShell Windows host. La provenienza delle evidenze resta distinta.

## Baseline Git e perimetro

HEAD iniziale e finale: `d5bf774d57eb143252a3659a29e653e4a0ce19a9`.
Branch: `refactor-database-entities`. Stato iniziale pulito, nessuna modifica locale preesistente.
Nessun commit, push, reset, clean o checkout distruttivo. Nessuna modifica frontend,
importazione, email istituzionale, UF/moduli, monte ore o calendario.

Analizzati i riferimenti InstitutionRole, attributi dei controller, policy e gate legacy,
IAM, inviti, profili, membership, generation/JWT, query scolastiche, factory/scope esami,
ExamPrincipal/ExamManager/ExamSubmissionManager/GradeManager e test multi-tenant esistenti.
In particolare IsAdmin governava sia query amministrative sia accesso a DELETE/cancel;
UpdateGrade poteva riassegnare ClassSubjectId e l'update Draft sostituiva fisicamente la struttura.

## Codice e autorizzazioni

* InstitutionRole conserva Admin=0, Teacher=1, Student=2; aggiunge Staff=3.
  UserRole non cambia. SQL conserva i precedenti ordinali aggiungendo Staff in coda.
* InstitutionPermissions centralizza capability per risorsa/operazione. Controller,
  manager, scope e repository usano controlli distinti per CRU e operazioni distruttive.
* Staff non diventa Admin/Teacher e non richiede profili per operazioni amministrative.
  ExamPrincipal distingue il suo scope di lettura da IsAdmin e IsTeacher; non genera
  un ClaimsContext legacy Staff mascherato.
* Staff + Teacher, Staff + Student e Staff + Teacher + Student non recuperano DELETE/cancel.
  InstitutionAdmin + Staff conserva i privilegi amministrativi. Teacher-only e Student-only
  conservano ownership e percorsi esistenti. I percorsi espliciti docente/studente richiedono profili reali.
* Il gate legacy rifiuta anche identità Teacher in allowlist con Staff senza InstitutionAdmin,
  chiudendo il possibile recupero del DELETE docente tramite il sistema legacy.

Matrice completa, endpoint, DTO ed errori HTTP:
[INSTITUTION_STAFF_PERMISSIONS.md](../../docs/api/INSTITUTION_STAFF_PERMISSIONS.md).

Componenti interessati: controller Student/Teacher/Grade/Exam/Submission; filtri capability e
body Staff; InstitutionSchoolAuthorization, Student/Teacher/Exam manager, GradeManager,
ExamManager, ExamSubmissionManager, SubmissionVisibility; query/repository anagrafiche,
factory e scope esami, GradeRepository, LegacyAccess, InstitutionWriteAuthorization;
InstitutionRole, mapping EF, onboarding e SQL/contract/manifest.

## Anagrafiche, voti, esami

Staff crea/legge/modifica Student e Teacher locali, soltanto nome/cognome tramite PUT.
Anche le letture filtrate per classe sono verificate per tutte le combinazioni Staff.
ClassId iniziale può essere indicato al POST; trasferimenti restano Admin-only.
L'associazione membership/profilo resta Admin-only. Nessun User, ruolo globale,
password, mailbox o membership viene creato dall'anagrafica. Le letture Staff includono
membership attiva, ruoli esatti e generation nello stesso filtro SQL tenant-scoped.

Staff legge/crea/modifica voti attraverso i servizi esistenti, con riferimenti immutabili,
validazioni e protezioni delle submission. Non può cambiare StudentId/ClassSubjectId,
eliminare voti o modificare voti di esame fuori dal workflow docente.

Staff legge/crea/modifica esami, assegnazioni, pubblicazione e chiusura. Update conserva
ClassSubjectId e ciclo di vita. Sostituzione struttura/assegnazioni ammessa solo su Draft
senza submission. Repository e scope negano cancellazione fisica/amministrativa e scritture
storiche, submission e risposte tramite il solo ruolo Staff. Review/finalize richiedono
Teacher reale o InstitutionAdmin; i percorsi studente restano personali.

Il motore esami, randomizzazione, autosave/revision, worker, retake, grading e trasferimenti
sono inclusi nella regressione generale. Nessun autore docente viene falsificato.

## JWT, IAM, audit e concorrenza

Emissione/parsing del catalogo istituzionale e rivalidazione JWT riusano i flussi esistenti.
Staff può essere assegnato/revocato da IAM Admin-only con EventId/ExpectedRevision,
last Admin, generation e audit. Inviti e snapshot SQL accettano quattro ruoli distinti.
Non viene introdotto Last Staff né alcun token legacy Staff o cache di autorizzazioni.

Le scritture Staff prendono lock condivisi institution → membership → roles → security state
prima delle risorse e rileggono status, identità, ruoli e generation nella transazione.
Il test concorrente mantiene una scrittura autorizzata aperta, avvia revoca IAM HTTP,
rilascia il writer, verifica i due esiti e il successivo rifiuto del vecchio token.
Non è presentato come una verifica tramite INNODB_LOCK_WAITS.

Nuovo audit mirato `staff.*` su voti/esami nella stessa transazione, con ActorUserId,
ActorMembershipId nel JSON e snapshot before/after. Trigger negano UPDATE/DELETE degli
eventi Staff; test SQL verificano entrambe le negazioni. Fault injection dopo persistenza
audit copre create/update voti ed esami e verifica digest invariato dell'intero tenant.
L'audit storico dei voti non è ricostruito e la copertura Teacher-only preesistente non viene ampliata.

## Database e riproducibilità

MySQL effettivo: **5.7.24**. Mai usate le istanze esistenti su 3306/13306.
Solo processi/datadir nuovi sotto `.staff-private`, con porte 23316–23319, password di test
private e PID registrati. I primi tentativi e i relativi log sono conservati separatamente.

| Schema | Tabelle | Trigger | FK | Indici |
|---|---:|---:|---:|---:|
| Prima | 43 | 98 | 131 | 180 |
| Dopo | 43 | 99 | 131 | 180 |

Migrazione `migrations/institution-staff/001_expand.sql`, validazione read-only `002_validate.sql`.
Il runner verifica versione/porta/datadir/schema/trigger, writer fermi, backup e SHA256,
ordinali/ruoli preesistenti e count audit dopo DDL. ALTER ENUM e sostituzioni trigger
implicano commit: nessuna promessa di rollback transazionale DDL.

Target additivo definitivo: 23319, con fixture preesistente Admin/Teacher/Student,
membership multi-ruolo, evento IAM e invito pending. Preflight, backup, postflight e
confronto completo delle 43 tabelle/99 definizioni e contesti trigger superati.
Target bootstrap: 23318, creato vuoto e confrontato integralmente con schema-contract.json.
Le definizioni delle tabelle comprendono tutti i vincoli e indici attesi.

I guard root-only di teardown dei test precedenti erano vincolati a 13306 e datadir storico.
Solo DOPO verifica del contract canonico, sul target di regressione sono stati vincolati
alla porta/datadir nuovi esatti. Bootstrap/migrazione di produzione non sono stati modificati
per ampliare tale bypass. Fixture e CLI supportano target di prova espliciti e verificati;
configurazioni precedenti restano valide.

Manifest SHA256 aggiornato e controllato nativamente con PowerShell.
Tutte le 152 entry, le regole EOL e la disponibilità dei file richiesti fuori dai pattern
ignore sono verificate. I sei controlli SQL finali restituiscono zero sia su 23318 sia su 23319;
il backup pre-DDL conserva il digest originario. Tutti i processi creati per i quattro datadir sono stati
spenti dopo verifica di versione, porta, datadir, UUID e PID. Datadir e backup restano privati/ignorati.
Quindici artefatti storici prodotti dai test sono ripristinati dal baseline con i filtri EOL Git;
nessun artefatto storico generato resta modificato.
Validatore Python `tools/verify-institution-staff.py`: **SUPERATO sull'host Windows**,
eseguito manualmente dall'utente, exit code `0`, con output:

```text
152 SHA256 files and static Staff contract verified; no SQL executed
```

Non è stato eseguito direttamente dall'agente nella sandbox.
Il successivo tentativo con il percorso assoluto fornito dall'utente è bloccato dal sandbox:
`Get-Item` su `C:\Users\Lorenzo\AppData\Local\Python\pythoncore-3.14-64\python.exe`
restituisce `System.UnauthorizedAccessException: Accesso negato`, categoria `PermissionDenied`,
ID `ItemExistsUnauthorizedAccessError,Microsoft.PowerShell.Commands.GetItemCommand`.
L'invocazione diretta restituisce `CommandNotFoundException`; questo non indica che Python
sia assente dal computer. Nessuna installazione, modifica degli script o aggiramento effettuato.
Il confronto completo SQL/contract eseguito con PowerShell non viene dichiarato un'esecuzione Python.

## Test e identità

Baseline ricostruita dal commit tramite `git archive` in una copia isolata, senza cambiare branch.
Discovery offline con asset NuGet già presenti: 133 unitari e 1.096 integrazione.
Confronto nominale delle identità con i TRX: nessuna identità precedente mancante.
Nessuna assertion precedente indebolita o test rinominato/ignorato. Sono state adattate le
sole aspettative del target isolato (porta/datadir) e aggiunto il caso SQL Staff al catalogo.

Nuovi test: matrice capability di tutte le combinazioni Staff, ordinali e principal;
HTTP CRU e isolamento, DTO proibiti, negazioni DELETE/cancel, profili e ruoli misti,
IAM/generation/revoca/JWT falsificato, legacy, rollout/Selection, audit, fault e concorrenza.
I test precedenti restano nella regressione completa, non soltanto nei filtri Staff.

**Esito finale .NET: 231 unitari + 1.137 integrazione = 1.368 passed, zero failed/skipped.**
Le 1.229 identità iniziali sono tutte presenti; 139 casi aggiunti (98 unitari, 41 integrazione,
di cui 40 HTTP Staff e un caso SQL sul nuovo ruolo). Numeri, identità e hash degli artefatti effettivi
sono riportati in [INSTITUTION_STAFF_ROLE_VERIFICATION.json](INSTITUTION_STAFF_ROLE_VERIFICATION.json).
I tentativi intermedi hanno evidenziato vincoli del test harness, contratti DTO legacy,
classificazione audit e isolamento delle fixture; non vengono presentati come esecuzioni verdi.

## Chiusura della verifica Python e provenienza

L'utente ha verificato dalla PowerShell Windows Python 3.14.2, `tools/local_test_db.py --help`,
11/11 test con `python -m unittest discover -s tools -p "test_*.py"` e le 152 entry SHA256
con `python tools/verify-project-artifacts.py`, senza operazioni SQL. Questi risultati sono
registrati come verifica manuale dell'utente, non come esecuzioni dell'agente.

Nel seguito l'agente ha ricontrollato gli hash dei due TRX (231 e 1.137 passed), i 152 hash
del manifest e gli artefatti distinti di bootstrap, migrazione e sei controlli SQL a zero.
Non sono state rieseguite operazioni SQL in questo seguito; restano valide le esecuzioni
MySQL isolate documentate sopra. I controlli Python offline non le sostituiscono.

L'utente ha poi comunicato l'esecuzione riuscita di `python .\tools\verify-institution-staff.py`
sulla PowerShell Windows host, con il risultato riportato sopra ed exit code `0`.
Questo chiude il precedente blocco del validatore Staff offline. Il controllo finale dell'agente
ha riconfermato gli hash dei TRX, tutte le 152 entry del manifest e il relativo digest.
In questa chiusura documentale non sono stati rieseguiti test .NET o comandi SQL.

La modalità Python con parametri MySQL non è stata eseguita: il confronto SQL completo
è stato eseguito separatamente con PowerShell/MySQL nativi sui target isolati documentati.
La limitazione di accesso all'interprete dalla sandbox rimane un fatto storico, non un blocco
residuo della Fase 1, dato l'esito host comunicato dall'utente. Nessuna evidenza host viene
attribuita a un'esecuzione diretta dell'agente e nessun controllo offline viene attribuito a SQL.

## Controllo finale dei criteri di accettazione

| # | Criterio | Esito ed evidenza |
|---|---|---|
| 1 | Staff nel dominio e nel database | Superato: ordinali .NET preservati, ENUM SQL esteso in coda, migrazione e contract verificati |
| 2 | JWT istituzionali | Superato: InstitutionAccess Staff, claim falsificato rifiutato, nessun token legacy Staff |
| 3 | Assegnazione/revoca IAM | Superato: workflow Admin-only, generation, audit e rifiuto del token precedente |
| 4 | CRU delle risorse consentite | Superato: Student/Teacher locali, voti, esami e letture filtrate per classe |
| 5 | Nessun DELETE tramite Staff | Superato: negazioni API, capability, manager e repository; cancel distinto da Update |
| 6 | Sicurezza delle combinazioni multi-ruolo | Superato: Staff con Teacher/Student non recupera DELETE; Admin + Staff conserva Admin |
| 7 | Nessuna escalation verso InstitutionAdmin | Superato: IAM negato a Staff, identità globale separata, bypass legacy chiuso |
| 8 | Isolamento fra Fondazioni | Superato: query SQL tenant-scoped, membership corrente e prove HTTP cross-tenant |
| 9 | Nessuna falsa identità Teacher | Superato: principal Staff senza profili; percorsi docente/studente con profili reali |
| 10 | Regole voti/esami preservate | Superato: riferimenti immutabili, ciclo di vita, submission e audit protetti |
| 11 | Motore esami | Superato: regressione comprendente worker, randomizzazione, autosave, review/finalize e retake |
| 12 | Storico trasferimenti studenti | Superato: suite precedente preservata, trasferimenti restano Admin-only |
| 13 | Migrazione MySQL isolata | Superato: MySQL 5.7.24, target 23319, preflight, backup, ordinali e postflight |
| 14 | Bootstrap e manifest | Superato: target 23318, contract 43 tabelle/99 trigger/131 FK/180 indici; 152 hash; Python offline host |
| 15 | Test precedenti e nuovi | Superato: 231 unitari + 1.137 integrazione, zero failed/skipped; tutte le 1.229 identità precedenti presenti |
| 16 | Documentazione aderente al codice | Superato: matrice, endpoint, limiti anagrafiche/account, errori HTTP e provenienza delle verifiche espliciti |

**Blocchi residui della Fase 1: nessuno rilevato nel controllo finale delle evidenze.**
Non è implementata creazione globale di account autenticabili da parte dello Staff:
anagrafica e identità rimangono separate, secondo il perimetro richiesto e la dipendenza
dalla futura fase email/provisioning verificato.

Nessun commit o push eseguito.

INSTITUTION STAFF ROLE AND PERMISSIONS IMPLEMENTED AND VERIFIED
