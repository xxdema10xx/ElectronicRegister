# Fase 2 — policy email istituzionali

**Stato Fase 2: implementata e verificata con evidenze combinate sandbox e host Windows.** L'utente ha comunicato l'esecuzione manuale dei quattro comandi Python sui file della Fase 2: 11/11 test, 159 SHA256, validatore Staff e validatore Email superati, tutti exit code 0. Questa esecuzione appartiene al computer host, non alla sandbox Codex. Il JSON conserva separatamente la precedente restrizione ambientale e la nuova evidenza manuale. I risultati non certificano i futuri artefatti della Fase 3. Nessuna creazione di mailbox o integrazione provider viene dichiarata.

## Baseline e preservazione

HEAD `d5bf774d57eb143252a3659a29e653e4a0ce19a9`, branch `refactor-database-entities`. Le modifiche della Fase 1 Staff erano già locali e non committate. Inventario iniziale e SHA256 dei file sono stati conservati in `.staff-private/email-phase2-*/source-baseline.json`. Non sono stati eseguiti reset, clean, checkout/restore distruttivi, commit o push. Nessun database produttivo, porta 3306 o istanza precedente sulla 13306 è stato utilizzato.

La baseline Staff documentata è stata controllata sui TRX e relativi digest: 231 unit + 1.137 integration, 1.368 complessivi. I report finali Staff, InstitutionRole, i controlli legacy e le migrazioni storiche sono preservati. Le sole aspettative numeriche preesistenti cambiate riguardano l'inventario reale dei tredici nuovi endpoint. Nessun test precedente viene rinominato, ignorato o indebolito. Il backfill distingue ora esclusivamente la nuova chiave tecnica intera del mutex dai GUID; tutti gli altri controlli restano applicati.

## Implementazione

- Dominio: categoria email separata dai ruoli IAM; configurazione di template, normalizzazione e collisioni; DTO JSON rigorosi e interfacce riutilizzabili.
- Business: parser lineare limitato, normalizzatore LatinFoldV1/ASCII, generatore puro deterministico, validatore dominio/indirizzo e suffisso numerico configurabile.
- Application/API: manager istituzionale, rollout disabilitato per default e tredici route protette, preview senza scritture/lookup di disponibilità, paginazione, policy versionate, riserve, override/ritiro Admin e storico.
- Infrastructure: repository SQL parametrizzato e transazionale, rivalidazione della membership/generation, lock IAM-first, EventId e ExpectedRevision, audit append-only e namespace globale UNIQUE condiviso con tutti i writer User.Email tramite trigger.
- Anagrafiche: creazione atomica Student/Teacher più riserva tramite `email-assignments/create-person`, riutilizzando i repository anagrafici esistenti; replay prima della creazione. Assegnazioni successive supportano profili legacy, anagrafiche senza User e membership senza profili.
- Database First: nuove entità e sidecar EF per le cinque tabelle; FK Student/Teacher composite restano SQL-only come nel modello MT3A, per non alterare la generazione del tenant shadow nei flussi legacy.

`User.Email`, password, membership, ruoli e login non sono modificati dall'assegnazione. La categoria primaria è selezionata esplicitamente; ruoli multipli non generano indirizzi aggiuntivi. Una persona già assegnata richiede un'operazione Admin dedicata per un nuovo indirizzo; le vecchie riserve istituzionali sono mantenute. Unificazione successiva di destinatari già assegnati è rifiutata e non riconciliata automaticamente.

## Sicurezza

Admin gestisce policy, eccezioni e ritiro del proprio tenant. Staff legge e applica policy Student/Teacher, crea le relative anagrafiche e consulta le assegnazioni consentite; non modifica template/domini/strategie, non forza indirizzi, non verifica mailbox e non elimina dati. Teacher/Student leggono soltanto i propri indirizzi attraverso membership/profili reali. Gli operatori piattaforma non ottengono privilegi tenant.

Le scritture ricontrollano membership attiva, insieme esatto dei ruoli e security generation sotto lock mantenuto fino al commit. Concorrenza con revoca è coperta da HTTP reale. EventId conflittuale o revision obsoleta produce 409; cross-tenant non disponibile produce 404; ruoli/rollout negati producono 403. Campi proibiti o template non validi producono 400. Gli errori non divulgano indirizzi o identificativi di altri tenant.

L'unicità non dipende da SELECT fra tabelle: il registro globale ha PK con la stessa collation di User.Email, alimentata da trigger di entrambi i writer. Collisioni concorrenti, incluso un writer User diretto, non producono doppie riserve. `Reject` e `NumericSuffix` sono configurabili e limitati. Un ritiro o override non libera il vecchio indirizzo istituzionale. La preview non interroga questo registro.

Policy, eventi e assegnazioni sono protetti da cancellazioni fisiche; UPDATE di audit/versioni è vietato. I test inseriscono un trigger di fault temporaneo sull'istanza disposable per verificare rollback di policy, assegnazione, nuova anagrafica e riserva dopo errore audit; i guard business esistenti non sono disabilitati. Il trigger temporaneo viene rimosso nel finally. L'audit conserva UserId/MembershipId reali, tenant, operazione, target, revision, policy, prima/dopo, UTC e motivazione.

Tutti i domini restano `ConfiguredUnverified`; indirizzi `reserved` o `retired` non diventano credenziali, destinatari attendibili per inviti sensibili, identità Microsoft o prove di titolarità. Le future integrazioni provider richiederanno verifica e schema/workflow dedicati.

## Database e riproducibilità

Prima: 43 tabelle / 99 trigger / 131 FK / 180 indici. Dopo: **48 / 117 / 145 / 199**. MySQL reale 5.7.24; schema additivo utf8mb4_unicode_ci, JSON nativo, chiavi tenant-aware e trigger compatibili con MySQL 5.7; nessun affidamento su CHECK ignorati.

`migrations/institution-email/001_expand.sql` aggiunge cinque tabelle e diciotto trigger. Il backfill registra solo User.Email correnti nel namespace, senza inventare audit storici. `002_validate.sql` produce dieci contatori di violazioni. `predecessor-contract.json` conserva lo schema Staff precedente; bootstrap, schema contract, manifest e validatore backfill sono aggiornati. Il singleton del mutex è dato tecnico, non un account o tenant seed.

L'agente ha creato processi/datadir nuovi sulle porte **23321**, **23322**, **23323**. La prima istanza è esplorativa; la verifica finale della migrazione riguarda 23322. Preflight ha attestato versione, porta, datadir e UUID; dump con SHA256 prima del DDL. La migrazione è stata applicata una volta mediante client nativo; struttura finale acquisita da MySQL. Bootstrap da zero separato sulla 23323: tutte le 48 definizioni SHOW CREATE TABLE e i 117 corpi/metadata trigger corrispondono al contratto, dieci contatori SQL a zero, nessun User o tenant seed. DDL implicitamente committed: nessuna promessa di rollback SQL transazionale.

Dieci guard di teardown già esistenti sono stati vincolati alla sola istanza disposable 23322 dopo confronto esatto della definizione; le sorgenti bootstrap/produzione e i nuovi guard email restano invariati. Le modifiche di binding non fanno parte del contratto canonico; il bootstrap 23323 è verificato prima di qualsiasi binding.

Il runner controllato `tools/Invoke-InstitutionEmailMigration.ps1` include preflight dell'intero predecessore, quietness, backup SHA256 e postflight completo. Il parser PowerShell non rileva errori sintattici; il file runner non è stato eseguito come script. L'applicazione reale del medesimo SQL e le verifiche native sono evidenze separate. Un tentativo di eseguire un helper privato .ps1 ha restituito `PSSecurityException`, `UnauthorizedAccess`, per execution policy: la policy non è stata modificata e nessun flag di bypass è stato utilizzato.

## Test e copertura richiesta

I TRX finali e il confronto delle identità sono registrati nel JSON. Sono preservate tutte le 1.368 identità Staff preesistenti. I nuovi casi riguardano il generatore e HTTP reale; i fallimenti intermedi sono conservati nei log privati e non sostituiti da dichiarazioni di successo.

Esecuzione finale diretta nella sandbox: **317 unitari + 1.176 integrazione = 1.493 Passed, zero Failed/Skipped**. Rispetto alla baseline Staff: 86 casi unitari e 39 HTTP/integrazione aggiunti. Confronto delle identità: 231/231 unit e 1.137/1.137 integration preservate. Dopo l'intera suite è stata eseguita anche l'assertion aggiuntiva sul digest dei dati didattici dello stesso tenant: 1/1 Passed, senza modifiche al codice applicativo. I dieci controlli SQL sono stati rieseguiti dopo la regressione con zero violazioni.

Quindici artefatti storici rigenerati dalla suite sono stati ripristinati soltanto dopo confronto con i byte SHA256 iniziali; le nuove evidenze restano nella cartella privata. Tutti e tre i server creati per la Fase 2 sono stati spenti regolarmente dopo verifica PID/versione/porta/datadir/UUID, conservando i datadir. `git diff --check` è passato; la chiusura del manifest di 159 file è stata controllata anche con `git check-ignore` e SHA256 in PowerShell.

| Requisiti test del prompt | Evidenze |
|---|---|
| 1–23, template/nomi/limiti/determinismo | InstitutionEmailGeneratorTests: esempi Umbria, composizioni arbitrarie, trasformazioni, Unicode, input inutilizzabili, sintassi/domini/suffissi/limiti e surrogate distinti |
| 24–26, configurazioni per tenant/categoria | TenantsHaveIndependentConventionsAndForeignRecipientsAreNotFound; OneInstitutionCanPublishDifferentTemplatesForAllCategories; CategoriesAreIndependentAndDoNotChangeIamRoles |
| 27–30, revisioni/storico | NewVersionsLeaveOldAddressesAndSnapshotsUnchanged; ConcurrentPolicyVersionsHaveOneWinner; audit e FK per versione |
| 31–34, autorizzazione policy | StaffCannotConfigureOverrideOrForceDomain; TeacherAndStudentCannotWritePoliciesOrReserve; tenant derivato da contesto rivalidato, senza ID policy foreign in input |
| 35–44, collisioni/idempotenza | CollisionUsesGlobalUniqueReservationAndConfiguredSuffix; ConcurrentSameAddressHasExactlyOneWinner; EventReplayReturnsSameAssignmentAndConflictingReplayNeverWrites; ExistingGlobalEmailConflictsAndGlobalWritersCannotClaimInstitutionalReservations; CrossTenantNamespaceCollisionReturnsOnlyGenericConflict; RetiredAddressCannotBeReservedForAnotherPerson; ConcurrentGlobalWriterAndAssignmentCannotDuplicateAddress |
| 45–52, identità/Staff/tenant | AtomicCreateSupportsUnlinkedProfilesAndIdempotentReplay Student e Teacher; StaffPreviewAndReservationDoNotCreateMailboxOrChangeGlobalIdentity; ForbiddenFieldsCannotCreateCredentialsOrVerification; doppia membership e User.Email invariato verificati nel test fra tenant |
| 53–57, login/onboarding/JWT | Intera regressione preesistente login legacy/istituzionale/Microsoft, IAM e onboarding, conservando identità e assertion; il nuovo modulo non aggiunge alcun writer di credenziali |
| 58–60, revoca/override | DisabledRolloutAndRevokedStaffCannotMutate; RevocationWaitsForEmailWriteThenOldTokenFails; OverrideRetainsOldReservationAndAudit; StaffCannotConfigureOverrideOrForceDomain |
| 61–64, rollback/audit | AuditFaultRollsBackPolicyAssignmentAndNewPerson (tre operazioni); AtomicCreateRollsBackProfileWhenReservationCollides; AppendOnlyAuditAndAssignmentsRejectDirectDestruction |
| 65–66, FK/unicità | Schema contract reale di FK composite; preflight/backfill validati; tentativi API cross-tenant; collisioni concorrenti reali e namespace globale UNIQUE |
| 67–69, dati didattici | Digest before/after sulle risorse del tenant destinatario e non destinatario più intera regressione voti, esami, worker, submission, iscrizioni e storico trasferimenti |
| 70, nessuna credenziale implicita | Flag restituiti falsi, User.Email invariato, nessuna creazione di User nel flusso, nuovo namespace separato e regressione dei login |

## Manifest e Python: sandbox precedente ed evidenza host successiva

Manifest aggiornato: **159 artefatti SHA256**. Verifica indipendente PowerShell eseguita; non viene presentata come esecuzione Python. Nuovo validatore offline `tools/verify-institution-email.py`; validatore Staff aggiornato soltanto per il nuovo inventario trigger; driver bootstrap esteso con i dieci controlli SQL email.

L'interprete host indicato dall'utente era stato tentato nella sandbox dal percorso assoluto. Invocazione: `CommandNotFoundException`; ispezione del percorso: `UnauthorizedAccessException`, `PermissionDenied`, `ItemExistsUnauthorizedAccessError,Microsoft.PowerShell.Commands.GetItemCommand`. Non è stato installato Python, modificato l'interprete o aggirata la sandbox. I controlli Python e gli 11 test degli strumenti **non erano stati eseguiti dalla sandbox sugli input della Fase 2**; l'utente li ha poi eseguiti con successo sull'host Windows, come indicato all'inizio del report.

Le precedenti esecuzioni manuali host (11 test, 152 hash, validatore Staff exit 0) rimangono evidenza storica della Fase 1. Le nuove esecuzioni host comunicate per la Fase 2 verificano invece 11 test, 159 hash, Staff ed Email, tutti exit code 0. Di seguito sono conservati i comandi, ora completati sull'host per gli input della Fase 2:

```powershell
$py = 'C:\Users\Lorenzo\AppData\Local\Python\pythoncore-3.14-64\python.exe'
& $py -m unittest discover -s tools -p 'test_*.py'
& $py tools/verify-project-artifacts.py
& $py tools/verify-institution-staff.py
& $py tools/verify-institution-email.py
```

Il blocco Python descritto sopra riguardava la precedente esecuzione nella sandbox ed è stato risolto, ai fini della certificazione della Fase 2, dai risultati manuali host comunicati dall'utente. Le evidenze sandbox restano distinte. Non sono previste in questa fase verifica DNS/provider, provisioning mailbox, importazione Excel/CSV, frontend o modifica dei login.
