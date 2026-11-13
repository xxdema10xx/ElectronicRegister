# Fase 5 — calendario istituzionale

Stato: **IMPLEMENTED_AND_VERIFIED**. La continuazione completa i layer SQL, Infrastructure, Application e Api che mancavano alla consegna precedente. La certificazione è basata sui risultati elencati sotto; i run intermedi falliti non vengono sommati ai risultati finali.

## Stato iniziale e preservazione

HEAD iniziale/finale `b1140b12f82cfff08818c4e302efe3139185b26a`, branch `refactor-database-entities`. Working tree iniziale NON pulita: conteneva i componenti e i report della prima parte. Snapshot iniziale `.formative-private/calendar-continuation-e6ecccfba02945b9a7e69b5280133ec4`; report/JSON precedenti conservati prima delle modifiche. Parser e regole temporali/monte ore sono riusati. Nessuna sorgente di test precedente modificata, rinominata o disabilitata. Gli artefatti storici rigenerati dalla regressione sono ripristinati dai byte iniziali, con gli output nuovi conservati privatamente. Nessun commit, push, reset, clean o checkout distruttivo.

## Modello e architettura effettivi

Archivio canonico institution_lesson; periodo classe/mese; impostazioni tenant; staging privato; ledger append-only; indice SQL delle occupazioni. Domain conserva invarianti e contratti, Business il mapping, Application autorizzazione/coordinamento, Infrastructure unità di lavoro/query/lock, Api binding rigoroso e HTTP. Sidecar EF Database First scalari e indici senza cambiare gli owner shadow legacy. Nessuna migrazione automatica allo startup.

FK/UNIQUE/trigger verificano tenant, Class/CourseEdition/binding Published/versione/modulo/Subject/ClassSubject, docenti e lifecycle. OriginalTeacherId e ownership immutabili. Indice occupazioni Class/Teacher aggiornato dai trigger, mai usato come calendario alternativo o per sommare ore. Lock corrente su questo indice evita il limite MySQL 5.7 di locking read della propria tabella nel trigger.

Transazioni ReadCommitted con mutex Institution e IAM-first; profili/riferimenti e ruoli/generation rivalidati sotto lock. Revisioni optimistic per lezione e periodo, EventId globale con hash attore/tenant/payload, ricevute durevoli e replay prima dei controlli sulla revisione corrente. Ledger BeforeJson/StateJson/ReceiptJson con motivo, actor User/Membership e UTC. Snapshot ore RepeatableRead. Deadlock/timeout => 409 e retry dello stesso intento.

## Funzionalità HTTP e sicurezza

Admin/Staff: configurazione, liste istituzionali/classi/edizioni/docenti, Draft, pubblicazione classe/mese, modifiche Published, annullamento, spostamento anche fra mesi, sostituzioni, recuperi, minuti effettivi/rettifiche, storia e ore. Lezioni Completed protette dalle normali riprogrammazioni. Teacher vede tutte le proprie classi e sostituzioni del solo tenant selezionato, conferma solo la propria Published e legge il modulo autorizzato dalla lezione. Student deriva dal profilo reale e dalla classe corrente, segue i trasferimenti senza cambiare lezioni storiche. Nessuna inferenza dalla sola Enrollment o da User.TeacherId.

Viste personali senza Draft, attori o motivi amministrativi. Capabilities calendario separate da IAM/email/piani Published/Exam/Grade; nessuna escalation Staff o PlatformOperator. JWT legacy/Selection non autorizzano il percorso InstitutionAccess. Ruoli combinati e due Fondazioni sono verificati con JWT reali. Route nuove assenti a rollout chiuso; flag InstitutionCalendar default false e rivalidato al commit. Gli altri rollout non sono accesi dal modulo.

## Importazione e tempo

Upload CSV/XLSX reale, parser/preflight esistenti, mapping automatico/manuale, GUID e riferimenti non ambigui, risoluzione SQL, preview privata con TTL 30 minuti e limiti, conflitti e budget. AppendDraft e ReplaceDraftScope esatto, senza sostituzione Published o altre classi. Conferma actor-bound ImportId/EventId/Revision/Digest con nuova verifica di IAM/riferimenti/prenotazioni e AllOrNothing. Errori per riga, colonne e mapping effettivo, riferimenti originali normalizzati, preview ID stabili e impatto ore.

Minuti liberi, intervalli semichiusi, stesso giorno locale, fuso tenant esplicito e UTC. DST ambiguo/inesistente rifiutato senza offset arbitrari. Date Excel 1900/1904 rigorose, seriale 60 fittizio rifiutato, stili numerici limitati e testo ISO; formule/macro/DTD/link esterni/ZIP ostili restano esclusi. Aula solo testo, nessuna prenotazione fisica garantita. Import Student/Teacher conservato.

Pubblicazione esplicita per classe/mese, nessun timer automatico. ApprovalLeadDays configurabile, deadline locale inclusiva, ritardi e publisher UTC. Correzioni Published atomiche e auditate, ClassSubject non riscritto dalle sostituzioni. Recuperi collegati alle Cancelled con nuova identità.

## Ore

Planned = modulo Published. Scheduled = durata UTC di Published Scheduled/Completed non annullate. Delivered = minuti effettivi esplicitamente confermati di Published Completed. Draft e Cancelled zero. Recupero attivo una sola volta, origine annullata zero. Budget separato Class/Module anche per stessa edizione. Residui/eccedenze/warning, UF, totali classe e progressione mensile SQL; nessuna modifica al piano per nascondere eccedenze, nessun completamento automatico Enrollment e nessuna certificazione basata sul tempo trascorso.

## Esecuzioni effettive

| Prova | Risultato |
|---|---:|
| Unitari, esecuzione nuova | 552 Passed |
| Integration, regressione completa finale | 1418 Passed |
| HTTP calendario su schema migrato, stessi casi già inclusi nel totale | 50 Passed |
| Identità .NET distinte correnti | 1970 |
| Identità della consegna precedente preservate | 1920 |
| Nuove identità rispetto alla consegna precedente | 50 |
| Python, esecuzione nuova | 18 Passed |

Zero Failed/Skipped nei run certificati. I conteggi provengono dai TRX con ResultSummary Completed e confronto delle identità comprensive dei parametri, non dalla somma dei run. Le 1920 identità precedenti e le identità Fase 4 sono effettivamente presenti. I test dei lettori file e i run ripetuti su istanze differenti non vengono contati nuovamente.

Percorso reale verificato: upload/risoluzione/preview => conferma SQL Draft => pubblicazione esplicita => Teacher su tre classi e Student sulla propria => modifiche/cancellazione/recupero/sostituzione => conferma Teacher e rettifica Staff => report ore e audit. Prove concorrenti: import/import, import/manuale, pubblicazione/pubblicazione, pubblicazione/sostituzione, cancellazione/completamento, conferma duplicata, revoca IAM, flag al commit, deadlock nativo, rollback dopo audit/lezione/pubblicazione e perdita risposta dopo commit con replay su altro host. SQL diretto auditato non supera i conflitti Published; audit e occupazioni non sono cancellabili arbitrariamente.

## MySQL, migrazione e bootstrap

Predecessore congelato 60 tabelle/153 trigger/171 FK/257 indici. Finale: 66/174/193/293. Migrazione finale su porta 23323, bootstrap indipendente su 23325; nuovi UUID/PID/datadir e binary path attestati, porte 3306/13306 mai usate. Backup predecessore SHA256 `7F290165DE644439FA2A24951480E9562BE4EB37BD0D2FE857D23EDBB9AC4BA6`. Definizioni precedenti preservate e schema migrato uguale al bootstrap dopo la sola normalizzazione dei guard di teardown delle fixture per porta/datadir.

Cinque controlli SQL calendario a zero anomalie su entrambe le istanze, oltre ai controlli del predecessore. Schema canonico, contract, migrazione, sidecar, validatori e manifest aggiornati. Manifest: 256 artefatti, SHA256 `CB1097278CC0A67CE386CBF2D87FBD2BA4B5EF1319C9A2A38373499719609E23`. DDL non transazionale: nessuna promessa di rollback automatico; recovery e stop-on-error documentati. Nessun database produttivo modificato. I processi sacrificabili sono arrestati dopo le verifiche di identità.

## Difetti trovati e corretti

Il primo schema esplorativo (65 tabelle) ha evidenziato MySQL 1442 usando FOR UPDATE sulla lezione nel suo trigger: corretto con indice occupazioni protetto e nuovo schema finale, senza alterare istanze produttive. Una prima asserzione nuova confrontava collezioni della receipt per riferimento: corretta in confronto strutturale; non era una duplicazione applicativa.

La prima regressione completa ha registrato 7 failure: quattro percorsi di cancellazione classe fermati dal nuovo FK non classificato e tre backfill che scambiavano time_zone_id per GUID. Correzione circoscritta all'inventario calendario completo e ai due campi fuso con tipo/collation esatti. Le classi vuote legacy restano cancellabili; storico calendario e FK sconosciute restano protetti. Riesame mirato dei casi falliti e successiva regressione completa presenti nei log. I run falliti restano conservati e sono esclusi dai totali Passed finali.

Il controllo OpenAPI ha trovato una JsonException dell'exporter .NET sul default opzionale di un Guid della preview. PreviewLessonId è ora un valore esplicito sempre prodotto dal server, senza default opzionale; le risposte sono tipizzate e il documento OpenAPI reale viene richiesto e verificato dal test. Nessuna permissività dei converter JSON è stata introdotta per aggirare il difetto.

Un ulteriore run sulla vecchia istanza riutilizzata ha registrato 1 failure della retention anagrafica: il servizio processa al massimo 20 staging scaduti per chiamata e il database accumulava anteprime dei run precedenti. Dopo il run risultavano ancora 19 anteprime scadute non elaborate. La regressione certificata è stata eseguita su un nuovo datadir/bootstrap isolato, senza modificare il servizio o il test preesistente; anche il run fallito resta conservato ed escluso dai totali finali.

## Limiti e ambiente

Niente scheduling automatico, TeacherAvailability, solver, frontend, presenze/firme, voti, notifiche, tirocini operativi o Fase 6. Limiti espliciti: lezione entro lo stesso giorno locale; insieme rigoroso di stili Excel; mapping corretto tramite nuova preview; TTL invalida il commit ma non esegue purge fisico di batch/audit; pubblicazione atomica della singola classe/mese. Nessun limite rigido del budget, soltanto warning/riconoscimento.

Python installato eseguito direttamente, nessun nuovo interprete o pacchetto. Nessuna modifica a NuGet.Config o execution policy. Warning NU1900 per feed advisory NuGet non raggiungibile: non certifica un audit aggiornato delle vulnerabilità, ma non ha impedito compilazione/test. Nessuna prova manuale host attribuita all'esecuzione corrente. Validatore offline verifica hash/contract/TRX, non pretende di eseguire SQL o HTTP.

## Criteri di accettazione

1. Verificato — Import CSV e XLSX tramite HTTP e SDK reale. Evidenze: Calendar_EndToEndCsvThreeClassesPublicationPersonalViewsCompletionHoursAndAudit, Calendar_InvalidImportIsAllOrNothingAndXlsxImportPersists.
2. Verificato — Preview per riga con errori/conflitti/colonne e impatto. Evidenze: Calendar_InvalidImportIsAllOrNothingAndXlsxImportPersists, Calendar_ImportCommitRejectsUntrustedOrStalePreview.
3. Verificato — Binding reale Class/CourseEdition/Published Module e ClassSubject. Evidenze: Calendar_LegacyClassAndModuleFromAnotherPublishedVersionAreRejected, Calendar_ForeignOrInventedReferencesCannotCreateLessons.
4. Verificato — Durate flessibili al minuto senza slot. Evidenze: FlexibleDurations, Calendar_PublishedAmendmentCancellationRecoveryAndDeliveredCorrectionPreserveHistory.
5. Verificato — Fuso tenant esplicito, DST e UTC. Evidenze: DstIsNeverResolvedSilently, Calendar_ExpiredPreviewCannotCommitAndLatePublicationDoesNotCertifyDelivery.
6. Verificato — Conflitti docente su tutte le classi del tenant. Evidenze: Calendar_ConcurrentOverlappingManualWritesHaveOneWinnerAndAdjacentLessonsAreAllowed.
7. Verificato — Conflitti classe. Evidenze: Calendar_SqlRejectsSignedPublishedOverlapAndOccupancyTampering.
8. Verificato — Importazioni concorrenti rivalidate sotto lock SQL. Evidenze: Calendar_CompetingImportConfirmationsRevalidateTeacherBookings, Calendar_ConcurrentImportAndManualBookingCannotDoubleBookTeacher.
9. Verificato — Aggregato classe/mese e Draft persistenti. Evidenze: Calendar_EndToEndCsvThreeClassesPublicationPersonalViewsCompletionHoursAndAudit.
10. Verificato — Pubblicazione esplicita e atomica. Evidenze: Calendar_ConcurrentPublicationAndConcurrentCompletionAreRevisionProtected, Calendar_PublishingOneClassDoesNotExposeOtherDraftsAndBudgetsStaySeparate.
11. Verificato — Operazioni Admin/Staff sul calendario. Evidenze: Calendar_StaffPublishesAndCombinedStaffTeacherDoesNotGainIamPrivileges.
12. Verificato — Modifiche Published auditate. Evidenze: Calendar_PublishedChangesAppearInBothPersonalViewsAndReasonsRemainPrivate.
13. Verificato — Annullamento, riprogrammazione e recuperi senza DELETE. Evidenze: Calendar_PublishedAmendmentCancellationRecoveryAndDeliveredCorrectionPreserveHistory.
14. Verificato — Sostituzioni senza riscrivere ClassSubject. Evidenze: Calendar_SubstitutionChangesRealTeacherViewWithoutChangingClassSubject.
15. Verificato — Vista Teacher unificata. Evidenze: Calendar_EndToEndCsvThreeClassesPublicationPersonalViewsCompletionHoursAndAudit.
16. Verificato — Vista Student dalla sola classe corrente. Evidenze: Calendar_StudentCalendarFollowsAuthorizedTransferWithoutRewritingLessons.
17. Verificato — Vista istituzionale amministrativa filtrata e paginata. Evidenze: Calendar_CombinedOperationalRolesDoNotWidenPersonalStudentScope.
18. Verificato — Nessuna Draft/audit nelle viste personali. Evidenze: Calendar_PersonalRolesCannotUseOperationalCapabilities, Calendar_PublishingOneClassDoesNotExposeOtherDraftsAndBudgetsStaySeparate.
19. Verificato — PlannedMinutes dal piano Published. Evidenze: Calendar_PublishingOneClassDoesNotExposeOtherDraftsAndBudgetsStaySeparate.
20. Verificato — ScheduledMinutes da Published attive. Evidenze: Calendar_PublishedAmendmentCancellationRecoveryAndDeliveredCorrectionPreserveHistory.
21. Verificato — DeliveredMinutes soltanto da conferme esplicite. Evidenze: Calendar_ExpiredPreviewCannotCommitAndLatePublicationDoesNotCertifyDelivery.
22. Verificato — Totali SQL per classe/modulo, UF e progressione. Evidenze: Calendar_EndToEndCsvThreeClassesPublicationPersonalViewsCompletionHoursAndAudit.
23. Verificato — Nessun doppio conteggio fra viste/occupazioni. Evidenze: Calendar_ConcurrentPublicationAndConcurrentCompletionAreRevisionProtected.
24. Verificato — Ownership e viste tenant-aware. Evidenze: Calendar_TeacherInTwoInstitutionsCannotLeakLessonsAcrossSelectedContexts, Calendar_SqlRejectsSignedPublishedOverlapAndOccupancyTampering.
25. Verificato — Capabilities calendario senza escalation IAM/Exam/Grade. Evidenze: Calendar_PersonalRolesCannotUseOperationalCapabilities, Calendar_TeacherStudentCombinationRetainsStudentScopeWithoutInventingTeacherProfile.
26. Verificato — EventId actor-bound e ricevute persistenti per ogni mutazione. Evidenze: Calendar_SameEventWithDifferentPayloadConflictsAndCompletedReplayDoesNotDoubleHours, Calendar_AfterCommitResponseFailureReplaysDurableReceiptOnAnotherHost.
27. Verificato — Audit append-only, revisioni, rollback e retry. Evidenze: Calendar_FaultInjectionRollsBackRowsPeriodsAndAudit, Calendar_ImportFaultOnSecondLessonRollsBackEntireBatchAndRetainsPreview, Calendar_NativeDeadlockLeavesOneDurableLessonAndSameIntentCanRetry.
28. Verificato — Migrazione MySQL attestata con backup SHA256. Evidenze: database migration identity/predecessor/backup/postflight artifacts.
29. Verificato — Bootstrap indipendente uguale alla migrazione. Evidenze: database independent schema comparison artifacts.
30. Verificato — Identità e sorgenti dei test precedenti preservate. Evidenze: baseline distinct identity comparison and generated-file initial-byte snapshots.
31. Verificato — Nuovi test HTTP/MySQL passati, inclusi race e deadlock. Evidenze: completed final TRX and native SQL assertions.
32. Verificato — Documentazione e OpenAPI coerenti con le funzionalità. Evidenze: API controller/strict DTO/rollout and reviewed documentation manifest.

## File modificati

- `.gitignore`
- `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCalendarController.cs`
- `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/ExceptionHandling/CalendarErrorsAttribute.cs`
- `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Program.cs`
- `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Security/CalendarControllerFeatureProvider.cs`
- `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/appsettings.example.json`
- `Backend/ElectronicRegisterAPI/src/Application/ElectronicRegisterAPI.Application/DependencyInjection/AddApplicationManagers.cs`
- `Backend/ElectronicRegisterAPI/src/Application/ElectronicRegisterAPI.Application/Managers/InstitutionCalendarManager.cs`
- `Backend/ElectronicRegisterAPI/src/Business/ElectronicRegisterAPI.Business/DependencyInjection/ServiceCollectionExtensions.cs`
- `Backend/ElectronicRegisterAPI/src/Business/ElectronicRegisterAPI.Business/Services/CalendarImportRules.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/Models/CalendarPermissions.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/Models/CalendarTeachingBinding.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/Models/InstitutionCalendar.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/Models/InstitutionCalendarImport.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/Models/InstitutionCalendarRules.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/ElectronicRegisterContext.Calendar.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/ElectronicRegisterContext.Multitenancy.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/Entities/InstitutionCalendarEventRecord.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/Entities/InstitutionCalendarImportRecord.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/Entities/InstitutionCalendarOccupancyRecord.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/Entities/InstitutionCalendarPeriodRecord.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/Entities/InstitutionCalendarSettingsRecord.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/Entities/InstitutionLessonRecord.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/Migrations/CalendarIntegrity.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/Migrations/Mt3aOwnershipMigration.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Repositories/InstitutionCalendarRepository.Imports.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Repositories/InstitutionCalendarRepository.Lessons.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Repositories/InstitutionCalendarRepository.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Repositories/InstitutionClassRepository.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Services/InstitutionImportFileReader.Calendar.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Services/InstitutionImportFileReader.Xlsx.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Services/InstitutionImportFileReader.cs`
- `docs/api/INSTITUTION_CALENDAR.md`
- `docs/architecture/ITS_INSTITUTION_CALENDAR.md`
- `migrations/bootstrap/001_schema.sql`
- `migrations/bootstrap/manifest.json`
- `migrations/bootstrap/schema-contract.json`
- `migrations/institution-calendar/001_expand.sql`
- `migrations/institution-calendar/002_validate.sql`
- `migrations/institution-calendar/predecessor-contract.json`
- `tests/Integration/INSTITUTION_CALENDAR_MANIFEST.json`
- `tests/Integration/INSTITUTION_CALENDAR_REPORT.md`
- `tests/Integration/INSTITUTION_CALENDAR_VERIFICATION.json`
- `tests/Integration/InstitutionCalendarAtomicityTests.cs`
- `tests/Integration/InstitutionCalendarConcurrencyTests.cs`
- `tests/Integration/InstitutionCalendarDeadlockTests.cs`
- `tests/Integration/InstitutionCalendarFileReaderTests.cs`
- `tests/Integration/InstitutionCalendarHttpTests.cs`
- `tests/Integration/InstitutionCalendarIntegrityTests.cs`
- `tests/Integration/InstitutionCalendarOpenApiTests.cs`
- `tests/Integration/InstitutionCalendarRecoveryTests.cs`
- `tests/Integration/InstitutionCalendarSecurityLifecycleTests.cs`
- `tests/Integration/TestResults/phase5-file-readers.trx`
- `tests/Unit/CalendarImportRulesTests.cs`
- `tests/Unit/CalendarTeachingRulesTests.cs`
- `tests/Unit/InstitutionCalendarRulesTests.cs`
- `tests/Unit/TestResults/phase5-baseline.trx`
- `tests/Unit/TestResults/phase5-domain.trx`
- `tests/Unit/TestResults/phase5-unit.trx`
- `tools/calendar_schema_contract.py`
- `tools/calendar_test_db.py`
- `tools/finalize-calendar-evidence.py`
- `tools/generate-calendar-schema.py`
- `tools/promote-calendar-bootstrap.py`
- `tools/record-calendar-verification.py`
- `tools/refresh-calendar-manifest.py`
- `tools/run-calendar-tests.py`
- `tools/scaffold-calendar-sidecar.py`
- `tools/test_calendar_evidence.py`
- `tools/verify-institution-calendar.py`
- `tools/verify-institution-email.py`
- `tools/verify-institution-formative-plan.py`
- `tools/verify-institution-import.py`
- `tools/verify-institution-staff.py`
