# GitHub Actions #11 — concorrenza Campus / CourseEdition

Repository `xxdema10xx/ElectronicRegister`, branch `refactor-database-entities`, base
`b400def64f3bb8065492e9458f6c461cc84b7bdc`.

Stato: **fix verificato localmente**. Tutte le verifiche finali sotto elencate sono PASS;
non è stato eseguito un nuovo workflow GitHub Actions.

## Evidenza originale

Run: https://github.com/xxdema10xx/ElectronicRegister/actions/runs/41679562186

Job `126076367708`, `integration (tenants, 1, 6)`. Scaricati il log del job
e l'artefatto `mysql84-integration-tenants-1`, id `13254575621`, digest dichiarato
`sha256:2a8dac7101f0eaa4aa530aa6e92c73838cf71918788de72a7e1845677770fb62`.
Estrazione locale: `.diagnostics/run11-source/`.

Il TRX `tenants-1/5/current.trx` prova che il POST ha restituito 500 e che
l'asserzione originale è fallita alla riga 29. L'artefatto non contiene una
stack trace SQL del POST: l'host configurava il livello di log Default=Critical.
Non è quindi possibile attribuire retroattivamente un numero MySQL al solo TRX.

## Percorso applicativo e vincoli

POST: `InstitutionCourseEditionController.LinkCampus` →
`InstitutionCourseManager.LinkCampusAsync` → `InstitutionSchoolAuthorization.AdminAsync`
(servizio di autorizzazione già esistente) →
`ICourseStructureRepository.LinkCampusAuditedAsync` → `LinkCampusAsync` →
`ElectronicRegisterContext.SaveChangesAsync` → Pomelo EF Core → MySqlConnector → InnoDB.
Non esiste un ulteriore service di scrittura intermedio in questo percorso.
Il repository possiede la transazione che include link e `campus.link` nell'audit.

DELETE: controller Campus → `InstitutionCampusManager.DeleteAsync` →
`ITransactionManager.ExecuteAsync` → `InstitutionCampusRepository.DeleteAsync` →
`InstitutionAdministrationPersistence.WriteAsync`, `LockAsync`, `NoDependentsAsync` →
DELETE EF e audit nella stessa transazione. L'autorizzazione amministrativa e i lock
IAM vengono mantenuti come prima.

`fk_mt4_ec_campus` e `fk_mt4_ec_edition` sono FK composte con `institution_id`;
anche l'audit ha `fk_mt4c_campus`. Non sono CASCADE. Il modello EF configura
Restrict; il database mantiene i vincoli attivi. Il DELETE verifica tutte le FK
entranti con letture bloccanti correnti prima di eliminare. Le letture preliminari
`AnyAsync` del POST non costituiscono invece un lock sul genitore.

## Errore riprodotto e correzione

Il test originale, ripetuto 100 volte sul codice applicativo della base, ha prodotto
99 PASS e un POST 500 (ripetizione 61). Il log applicativo acquisito nel TRX
`baseline-repeated.trx` registra `DB_DEADLOCK`, `MySqlException`, **1213**, SQLSTATE
**40001**. La stack punta a `LinkCampusAuditedAsync:132`, cioè all'INSERT dell'audit
eseguito dopo `SaveChangesAsync` del link, e poi risale attraverso Manager e Controller.
Il filtro HTTP non include 1213, nemmeno nella forma diretta: l'eccezione arriva al
gestore globale e diventa 500. Non si tratta di una FK violation 1451/1452.

La transazione del POST prende lock sui figli (link, poi audit), mentre il DELETE
blocca il genitore e controlla i figli con letture `FOR UPDATE`. L'ordine differente
consente un ciclo tra i lock del link e quelli del range dell'audit. La soluzione
uniforma l'ordine sul genitore, senza eliminare i controlli sulle dipendenze.

La riproduzione controllata acquisisce anche `SHOW ENGINE INNODB STATUS`:

- il POST mantiene un record lock X su `course_edition_campus.PRIMARY` e aspetta
  un gap lock / insert intention su `course_administration_event.PRIMARY`;
- la transazione concorrente mantiene il lock sul range dell'audit e aspetta
  il record del link con `SELECT ... FOR UPDATE`;
- InnoDB sceglie il POST come vittima: `WE ROLL BACK TRANSACTION (1)`.

Questo grafo è della riproduzione locale controllata, non un log recuperato dal
server CI originale. È conservato in `.diagnostics/run11-local/fixed-deadlock-graph.txt`
ed è presente nello stdout del TRX mirato. La regressione costruisce proprio questo
ciclo su dati sintetici e verifica il numero 1213 originale sotto l'eccezione di dominio.
Con la stessa sessione si verificano versione `8.4.13`, isolamento `REPEATABLE-READ`
e `@@foreign_key_checks=1`.

Su MySQL 8.4.13 reale, il timeout dell'INSERT produce:

```text
InvalidOperationException: An exception has been raised that is likely due to a transient failure
  DbUpdateException: An error occurred while saving the entity changes
    MySqlException (1205): Lock wait timeout exceeded; try restarting transaction
```

La stack attraversa `MySqlExecutionStrategy.ExecuteAsync`, `SaveChangesAsync`,
`CourseStructureRepository.LinkCampusAsync:39` e `LinkCampusAuditedAsync`.
In questo secondo caso, il filtro `CourseApiErrorsAttribute` riconosce le violazioni 1062/1451/1452/1644
dirette o sotto `DbUpdateException`, ma non questo wrapper del provider.
L'eccezione raggiunge quindi il gestore globale come errore inatteso (500).
Questa evidenza non è una violazione FK presunta.

La correzione acquisisce sul Campus un `SELECT ... FOR UPDATE` tenant-scoped,
nella transazione del link, prima delle letture e delle scritture figlie. Il lock
è mantenuto fino al commit di link e audit; una lettura corrente dopo un DELETE
committed rileva invece l'assenza e restituisce 404. Gli errori 1205/1213 vengono
riconosciuti nel solo comando di associazione, attraversando i wrapper EF/Pomelo,
e tradotti in `CourseConflictException` dopo la disposizione/rollback della
transazione. L'eccezione originale viene conservata come inner exception.
Il filtro HTTP esistente converte questa eccezione di dominio in 409.

Non sono introdotti retry. Il gestore globale, le FK, le migration storiche,
`er-quality-1`, i container preesistenti, EC2 e produzione non vengono modificati.

## Invarianti e prove

- POST 201 seguito da DELETE 409: Campus, link e CourseEdition conservati.
- DELETE 204 seguito da POST 404: nessun Campus né link, CourseEdition conservata.
- Operazioni sovrapposte: solo i due esiti di dominio ammessi, mai 500.
- Lock osservati su connessioni effettive con `performance_schema.data_lock_waits`,
  non dedotti da ritardi o dal semplice `Task.WhenAll`.
- Una pausa dopo il vero INSERT mantiene i lock mentre parte il vero DELETE;
  il test non sostituisce repository, provider, transazioni o risultati HTTP.
- Test 1205/1213 con contesa reale: rollback di link e audit, genitori conservati.
  La tabella usata per rendere deterministica la scelta della vittima del deadlock
  è temporanea e locale alla sessione di test.

## File modificati

- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Repositories/CourseStructureRepository.Lifecycle.cs`: lock del genitore e traduzione locale
  dei soli errori di concorrenza 1205/1213.
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/Exceptions/CourseConflictException.cs`: conservazione opzionale dell'eccezione originale.
- `tests/Integration/InstitutionAdministrationDependencyRaceTests.cs`: ordini sequenziali, 100 ripetizioni,
  contesa verificata sul genitore, errori reali e integrità dopo rollback.
- `tests/Integration/InstitutionCampusLinkInterleavingTests.cs` (nuovo): INSERT reale sospeso con interceptor
  di osservazione e DELETE sovrapposto, con verifica di link e audit committed.
- `tests/Integration/InstitutionClassCrudFixture.cs`: diagnostica test per acquisire le eccezioni
  originali senza modificare le risposte applicative.
- `migrations/mysql84-remediation/manifest.json`: hash aggiornati e nuovo test incluso.

## Esecuzioni e limitazioni

Tutti i test di concorrenza usano container temporanei della fixture, con immagine
MySQL 8.4.13 fissata al digest del candidato. Nessun mock certifica la race.
Le evidenze locali sono in `.diagnostics/run11-local/`.

| Esecuzione realmente completata | Risultato | TRX |
| --- | --- | --- |
| Codice originale, race ripetuta 100 volte | 99 PASS, 1 FAIL con POST 500 / MySQL 1213 | `baseline-repeated.trx` |
| Fix: race, ordini sequenziali, attese osservate, errori 1205/1213, INSERT sovrapposto | **106/106 PASS**, nessun ignorato | `fixed-targeted.trx` |
| Intera classe + unione delle partizioni tenant storica e attuale | **506/506 PASS**, nessun ignorato | `full-class-and-tenant-partition.trx` |
| IAM: `BothRolloutFlagsAreRequiredAndGetRemainsReadOnly` | **6/6 PASS** | `iam-regression.trx` |
| Onboarding: `ProvisioningDoesNotAssignRolesUntilPersonallyAcceptedAndAuditsDistinctActors` | **1/1 PASS** | `onboarding-regression.trx` |
| Governance: `Governance_LifecycleRevokesOnlyTheSelectedInstitutionAndArchiveIsTerminal` | **2/2 PASS** | `governance-regression.trx` |
| Intera suite unitaria, incluse regole Campus/Class/Academic, membership e autorizzazioni | **598/598 PASS** | `unit.trx` |

I 506 casi comprendono **497 casi dell'intera `InstitutionClassCrudTests`** e 9
casi `Mt2IdentityTests`. Il confronto dei metodi effettivamente presenti nel TRX
con `partition-coverage-plan.json` dà **23/23** metodi della partizione del run #11
e **24/24** della partizione attuale `(tenants, 1, 6)`, senza mancanti. L'aggiunta
di quattro metodi modifica infatti la distribuzione alfabetica degli shard.
Un solo filtro comprende classe e partizioni, evitando di rieseguire gli stessi casi.

La race originale passa **200/200 volte sul fix**: 100 nella verifica mirata e altre
100 nella classe completa. Restano immutate le asserzioni sugli esiti ammessi e
sull'integrità. Sono passate anche `AcademicPathDeletionAndClassCreationNeverCascadeACommittedClass`,
`AssignmentDeletionAndExamInsertKeepEveryCommittedExam`, le eliminazioni con dipendenze
school/edition/history/exams/submissions/grades, i test least privilege e quelli di
rollback dell'audit Campus. Non risultano regressioni nei controlli eseguiti su
onboarding, IAM, autorizzazioni tenant e governance.

| Scenario HTTP | Prima / riproduzione | Dopo |
| --- | --- | --- |
| Race libera POST–DELETE | POST 500 per deadlock nell'audit | POST 201 / DELETE 409, oppure POST 404/409 / DELETE 204 |
| POST committed prima del DELETE | Vincolo da preservare | 201 / 409, Campus e link conservati |
| DELETE committed prima del POST | Vincolo da preservare | 204 / 404, nessun Campus/link, CourseEdition conservata |

La gestione specifica 1205/1213 è verificata al confine del repository reale:
`CourseConflictException`, numero e catena originali conservati, transazione
completamente annullata. La traduzione di questa eccezione in HTTP 409 resta nel
filtro `CourseApiErrorsAttribute` preesistente. Gli altri errori SQL non sono
convertiti genericamente in conflitti dal nuovo codice.

Controlli finali: `git diff --check` PASS; verifica della chiusura con
`node tools/mysql84-release.cjs` PASS, predecessore immutabile verificato.
Il nuovo `tests/Integration/InstitutionCampusLinkInterleavingTests.cs` è presente
nel manifest candidato insieme agli hash aggiornati di tutti i sorgenti modificati.

Limiti: API e testhost sono stati eseguiti su Windows, MySQL nel container Linux.
La classe e l'unione delle partizioni sono state eseguite nello stesso processo;
non è stato rilanciato il runner CI che isola i singoli metodi in processi distinti.
Le regressioni IAM/onboarding/governance aggiuntive sono selezionate, non una nuova
certificazione completa dei rispettivi domini. L'artefatto originale CI non consente
di ricostruire retroattivamente la sua eccezione SQL; il deadlock è stato riprodotto
localmente sul codice della stessa base e sullo stesso test.

Nessuna certificazione SQL completa già PASS nel run #11 è stata ripetuta;
l'installazione dello schema nelle fixture temporanee è il prerequisito dei test
applicativi. I container temporanei vengono rimossi dalla fixture; i container
preesistenti non sono stati modificati.

Non sono stati eseguiti commit, push o deployment.
