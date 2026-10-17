# Fase 3 — Atomicita, concorrenza e recupero del worker

## 1. Base e verifica iniziale

Repository locale: `xxdema10xx/ElectronicRegister`, branch `refactor-database-entities`.
Commit iniziale: **`7a94cf1` — feat: complete Phase 2C API and result visibility**.
Workspace inizialmente pulito. Nessun pull, reset o checkout; il lavoro usa lo stato locale della Fase 2C.

Prima di modificare i file e stata eseguita la soluzione completa: **29 unit + 81 integration = 110 Passed, 0 Failed, 0 Skipped**. Risultati nei due `TestResults/phase3-baseline.trx`. Verificato il report Phase2C contro lo stato locale.

## 2. Audit e matrice delle garanzie

Esaminati TransactionManager (`Repositories/TransactionRepository.cs`), interfacce, ExamSubmissionRepository, processor, manager Exam/ExamSubmission, worker, AuditRepository, GradeRepository, contesto e mapping EF, schema di test, fixture, seed, test concorrenti e di sicurezza delle Fasi 2B/2C e test unitari.

| Garanzia | Classificazione iniziale | Evidenza precedente | Intervento Fase 3 / esito |
| --- | --- | --- | --- |
| Autosave: revision massima accettata prevale | GARANTITO E GIA TESTATO | ConcurrentSaveAnswers_HigherRevisionWins e test revision obsolete | Nessuna modifica o duplicazione |
| Autosave dopo deadline o consegna non modifica risposte | GARANTITO E GIA TESTATO | AutosaveAfterDeadline_NoWrite, AutosaveConcurrentToSubmit_NoAnswerSavedAfterSubmit | Nessuna modifica o duplicazione |
| Submit concorrenti: unica consegna e audit, niente Grade prematuro con open | GARANTITO E GIA TESTATO | ConcurrentSubmit_OneDeliveryOnly | Nessuna modifica |
| Finalize concorrenti/retry: Grade e audit unici, richieste diverse in conflitto | GARANTITO E GIA TESTATO | FinalizeConcurrencyTests, Phase2B_AdditionalTests, Phase2C_ApiTests | Nessuna modifica |
| Review dopo Finalized rifiutata, ownership e visibilita | GARANTITO E GIA TESTATO | Test Fasi 2B/2C | Nessuna modifica |
| Rollback di scritture SQL gia completate per submit/finalize/review | GARANTITO DAL CONFINE TRANSAZIONALE MA NON TESTATO CON FAULT DOPO SCRITTURA | I test unitari dimostravano propagazione, non rollback MySQL | 10 casi di rollback effettivo, tutti passati |
| Review/finalize nei due ordini di lock, verifica del punteggio piu recente | GARANTITO DAI LOCK MA NON TESTATO CON ORDINE CONTROLLATO | Esistevano avvii concorrenti non deterministici | 2 nuovi casi con barriera dopo SQL e osservazione del secondo FOR UPDATE |
| Manuale/automatico sovrapposti attraverso la deadline | GARANTITO DAL PROCESSOR MA NON TESTATO NEI DUE ORDINI | Copertura separata di submit e scadenza | 4 casi: entrambi gli ordini, closed-only e open; 1 caso aggiuntivo sulle due guardie temporali |
| Recupero da stop controllato del worker | GARANTITO DALLO STATO PERSISTITO MA NON TESTATO | Selezione InProgress scadute presente | Nuova istanza recupera il batch; seconda scansione non ridispatcha i completati |
| Due worker selezionano lo stesso batch | GARANTITO DA LOCK/IDEMPOTENZA MA NON TESTATO DAL PERCORSO WORKER | Solo processor/finalize concorrenti | Selezioni SQL sincronizzate, sei dispatch su tre submission, solo tre consegne effettive |
| Errore su un elemento non ferma il batch e permette recupero successivo | GARANTITO DAL CATCH PER ELEMENTO MA NON TESTATO | Catch e scope per elemento presenti | Fault dopo audit SQL, errore osservato nei log, altri elementi completati, successivo batch recupera solo il fallito |
| Avvio worker con validazione degli scope DI | POTENZIALE BUG, POI DIFETTO DIMOSTRATO | Singleton IHostedService dipendeva da ITimeProvider scoped | Test rosso riprodotto; clock ora risolto nel listScope; test verde |
| Recupero da kill del processo, arresto MySQL o perdita rete durante commit | NON GARANTITO DALLE PROVE DI QUESTA FASE | Nessuna simulazione di questi eventi | Non dichiarato come verificato |
| Assenza universale di deadlock sotto qualsiasi carico | NON GARANTITO | Nessuna prova puo inferirla da poche interleaving | Nessun deadlock/timeout nei casi controllati; nessun retry indiscriminato aggiunto |

### Flussi e confini reali

- Manual submit: ExamController -> ExamManager (ownership e InProgress) -> ExamSubmissionProcessor -> TransactionManager.ExecuteAsync -> repository -> MySQL. Il processor acquisisce `SELECT ... FOR UPDATE`, controlla tempo/idempotenza, applica il DTO tramite SaveAnswersAsync e rilegge sotto lock. TryBeginSubmitAsync salva SubmittedAt/SubmitReason/PendingReview. Il grading salva eventuale Grade, aggiornamento della submission e audit. Il commit avviene solo dopo il completamento del delegato.
- Automatic submit: PeriodicTimer -> selezione GetExpiredInProgressAsync in uno scope -> nuovo scope per elemento -> stesso processor/TransactionManager. Nessun DTO applicato. I retry trovano SubmittedAt e ritornano lo stato persistito senza scritture.
- Review: manager -> ExecuteAsync -> lettura FOR UPDATE -> validazione e aggiornamento AwardedPoints/commenti/OpenScore/TotalScore/SuggestedGrade -> UpdateAfterReviewAsync -> SaveChangesAsync -> commit.
- Finalize: manager -> ExecuteAsync -> FOR UPDATE -> completezza review/override -> GradeRepository.AddAsync o UpdateAsync -> aggiornamento submission -> AuditRepository.AddAsync -> commit.
- Autosave: manager -> SaveAnswersAsync -> transazione propria quando non esiste una ambient -> FOR UPDATE -> controlli revision/stato/deadline -> sostituzione risposte e opzioni + revision -> SaveChangesAsync -> commit. Dentro un submit riusa la transazione ambient e non committa separatamente.

GradeRepository e AuditRepository invocano SaveChangesAsync sullo stesso DbContext scoped della transazione applicativa. UpdateAfterGradingAsync/UpdateAfterReviewAsync salvano anch'essi sul medesimo contesto. Passed ufficiale deriva dal Grade, review conserva il proprio valore provvisorio, la policy di visibilita della Fase 2C resta invariata.

## 3. Difetto produttivo dimostrato e correzione minima

Il nuovo test `HostedWorker_ResolvesWithScopeValidation`, prima della correzione, falliva costruendo il provider con ValidateScopes/ValidateOnBuild:

> Cannot consume scoped service 'ElectronicRegisterAPI.Domain.Interfaces.Services.ITimeProvider' from singleton 'Microsoft.Extensions.Hosting.IHostedService'.

Il clock era iniettato nel costruttore del worker singleton pur essendo registrato scoped. Ora viene risolto nel listScope gia esistente, insieme al repository. Nessuna modifica al lifetime globale del clock, all'intervallo di 15 secondi, al batch size 50 o al comportamento del processor.

`ProcessBatchAsync` passa da private a internal, con accesso friend per Integration.Tests: i test attraversano esattamente selezione e dispatch usati dal timer, senza aspettare 15 secondi. Non sono aggiunti flag produttivi, scheduler, servizi o tabelle. Nessun altro difetto produttivo dimostrato dai nuovi casi; manager, repository e transazioni rimangono invariati.

## 4. File modificati

Produzione:

- `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/BackgroundServices/ExpiredExamSubmissionWorker.cs`: risoluzione scoped del clock e visibilita internal del batch.
- `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Properties/AssemblyInfo.cs`: InternalsVisibleTo per Integration.Tests.

Test/documentazione:

- `tests/Integration/Phase3_TestSupport.cs`: provider isolati con DI reale, clock fissi, interceptor EF, barriera dei lock e snapshot SQL.
- `tests/Integration/Phase3_TransactionRollbackTests.cs`: 10 casi di rollback MySQL.
- `tests/Integration/Phase3_ConcurrencyTests.cs`: 7 casi con ordine controllato e guardie temporali.
- `tests/Integration/Phase3_WorkerRecoveryTests.cs`: 4 casi, incluso il difetto DI.
- `tests/Integration/SeedData.cs`: scenari isolati 30/31/32, con lo stesso reset dipendenze gia esistente; 30/32 closed-only, 31 con domanda aperta.
- Questo report e i TRX delle esecuzioni.

Non modificati vecchi test, schema, scaffolding, grading, API HTTP, autorizzazioni, worker scheduler, frontend o Google Forms. Nessuna dipendenza aggiunta.

## 5. Fault injection e prova di rollback reale

L'interceptor di test `Phase3.SaveProbe.SavedChangesAsync` viene chiamato **dopo il completamento del SaveChangesAsync reale**. Verifica che esista ancora una transazione ambient aperta. L'hook e installato solo in provider DI dedicati al test, mai nella fixture condivisa o nella produzione.

Prima dell'errore vengono eseguite proiezioni SQL AsNoTracking nella transazione per osservare lo stato scritto su MySQL. Si solleva poi esclusivamente `Phase3.InjectedFailure`; Assert.ThrowsAsync richiede quel tipo esatto. Il TransactionManager reale esegue rollback. Il provider/scope che ha fallito viene eliminato. La verifica finale apre un nuovo scope della fixture con un nuovo DbContext e legge nuovamente MySQL.

Lo snapshot comprende Status, SubmittedAt, SubmitReason, revision, GradeId, SuggestedGrade, tutti i punteggi/Passed, OverrideReason, FinalizedAt, ReviewedByUserId e UpdatedAt; per ogni risposta ID, QuestionId, testo, AwardedPoints, commento, IsCorrect, LastSavedAt e opzioni; inoltre righe Grade e AuditEvent con valori, attori e JSON. Ordinamento stabile e uguaglianza dello snapshot prima/dopo garantiscono che non siano rimaste modifiche parziali, incluse risposte sostituite e collegamenti opzioni.

| Caso | Prima | Stato letto da MySQL nella transazione, prima del fault | Dopo rollback, nuovo scope |
| --- | --- | --- | --- |
| Manual submit, fault immediatamente dopo SaveAnswers, closed/open | InProgress, revision 0, due risposte e relative opzioni, nessun Grade/audit | Revision 1 e una sola risposta: l'altra e stata realmente eliminata con i collegamenti; risposta rimanente sostituita | Snapshot iniziale identico, incluse entrambe le risposte originali/opzioni/revision |
| Manual submit, fault dopo AuditRepository.SaveChanges, closed/open | Come sopra | Submission consegnata, audit submit presente, Grade presente solo per closed-only, risposte sostituite | InProgress, SubmittedAt/SubmitReason/GradeId/SuggestedGrade iniziali, nessun Grade/audit; risposte originali |
| Automatic submit, fault dopo audit, closed/open | InProgress, revision 0, risposte persistite | SubmitReason time_expired, audit reale, Grade presente solo per closed-only | Snapshot iniziale identico, nessuna consegna o scrittura parziale |
| Finalize ordinario | PendingReview, SuggestedGrade 7, AwardedPoints 2, nessun Grade/finalization timestamp | Finalized, Grade 7 realmente inserito, GradeId, timestamp e audit finalize | PendingReview originale, nessun Grade/GradeId/timestamp o audit finale; SuggestedGrade e AwardedPoints invariati |
| Finalize con override, nuovo Grade | PendingReview come sopra | Finalized, Grade 8, motivo override e audit override con JSON | Stato iniziale identico |
| Finalize con override, Grade gia esistente | Graded, Grade 6 e GradeId persistiti dal submit closed-only | Stesso Grade aggiornato a 8 e submission finalizzata con audit override | Grade originale 6 con stesso ID, stato Graded e audit solo submit; nessun timestamp finale |
| Review | PendingReview, punti aperti 2, OpenScore 2, totale 7, SuggestedGrade 7, commento precedente | AwardedPoints/OpenScore 5, totale/SuggestedGrade 10 e nuovo commento effettivamente letti da SQL | Snapshot iniziale identico, inclusi punti/commenti precedenti |

Il caso Grade gia esistente non pretende di annullare il submit precedente gia committato: annulla solo le scritture della nuova finalizzazione. FK sempre attive; niente SQLite, InMemory, mock di repository o rollback soltanto in memoria.

## 6. Concorrenza controllata

`Phase3.OrderedAsync` usa provider, scope, DbContext e transazioni indipendenti. Dopo la prima scrittura SQL il primo flusso mantiene il lock e attende una TaskCompletionSource. Un DbCommandInterceptor osserva il tentativo FOR UPDATE del secondo flusso. Solo allora il primo viene rilasciato. Nessun Delay/Sleep; attese limitate a 15 secondi. La prima transazione deve terminare prima che la seconda ottenga il lock.

- **Review prima:** review completa iniziale da 2 punti, poi revisione concorrente a 5. Finalize attende e crea Grade/SuggestedGrade 10, usando la review piu recente committata. Un solo Grade e audit finale.
- **Finalize prima:** parte da review realmente completa a 2 punti. Finalize crea Grade 7; la review a 5 viene rifiutata con InvalidOperationException classificata come conflitto applicativo. Lo snapshot completo dopo la finalizzazione e identico dopo il rifiuto, inclusi AwardedPoints, Grade, SuggestedGrade, Passed, timestamp e audit.
- **Manuale prima, clock prima della deadline:** ultime risposte accettate e revision 1 persistite. L'automatico, con clock dopo la deadline, attende il lock e ritrova la consegna senza sovrascriverla.
- **Automatico prima:** consegna le risposte precedentemente salvate, revision 0 e TimeExpired. Il manuale che si sovrappone mentre l'automatico e ancora non committato ritorna il risultato persistito senza applicare il DTO tardivo. Non si tratta di accettazione di nuove risposte dopo la scadenza.
- Entrambi gli ordini sono provati su closed-only (un Grade) e open (PendingReview senza Grade). Audit submit singolo, attore, SubmittedAt, SubmitReason, revision e opzioni verificati. Un successivo retry automatico non cambia lo snapshot.
- Un caso separato dimostra che automatico prima della scadenza e manuale dopo la scadenza, su submission non consegnata, sono respinti senza alcuna scrittura.

Non viene accettata una generica Exception come esito valido. I soli rifiuti attesi sono tipi/marcatori applicativi precisi. Deadlock, timeout, DbUpdateException, ObjectDisposedException o setup errato falliscono i test.

## 7. Recupero e multi-worker

I tre scenari dedicati hanno deadline antecedenti al clock fisso 2000-01-02. Le submission degli altri test hanno deadline attuali e non entrano nel batch; non viene aggiunto un filtro fittizio al repository. Ogni test ricrea tutti e tre gli scenari eliminando i record dipendenti tramite SeedData.ResetScenarioAsync. Provider e interceptor vengono sempre eliminati a fine uso.

- **Arresto controllato prima del dispatch:** prima istanza avviata e fermata prima del primo tick, nessun dispatch ne modifica lo stato persistito. Una nuova istanza/provider sullo stesso database esegue esplicitamente il metodo di batch produttivo. Tutte e tre vengono recuperate, con SubmittedAt, TimeExpired, opzioni e risposte preservate. La scansione successiva non effettua nuovi dispatch o modifiche. Ogni elemento usa un DbContext diverso.
- **Due worker:** un interceptor sincronizza il completamento delle due SELECT di scadenza prima del dispatch. Entrambe le istanze leggono lo stesso batch e tentano ogni ID. Sei DbContext di processing distinti; tre consegne effettive, due Grade closed-only, nessun Grade per l'open e un audit per submission. Il totale di SaveChanges e 11 (4+4+3): i tre dispatch perdenti non aggiungono alcun salvataggio. Nessun errore tecnico registrato dai due worker.
- **Errore sul primo elemento:** il piu vecchio, closed-only, fallisce dopo che anche il suo audit e stato salvato dentro la transazione. Il logger contiene esattamente InjectedFailure, quindi il fallimento non viene scambiato per successo. Nuovo scope verifica rollback integrale e stato recuperabile. I due elementi successivi completano. Un successivo worker/batch con provider nuovo seleziona soltanto l'ID fallito e lo completa, senza Grade/audit duplicati. Il suo ContextId e diverso da quelli del batch fallito.

I test non si limitano a chiamare il processor: invocano selezione batch e dispatch del worker. L'osservatore di dispatch delega al processor reale e misura ID/ContextId; non sostituisce il database.

## 8. Deadlock, timeout e strategia di retry

Non sono presenti retry EF automatici o logiche generiche di retry nel flusso esaminato. Le operazioni sulla stessa submission prendono prima il lock della submission, poi risposte/voto/audit. Gli effetti osservati sono transazionali sul medesimo DbContext. I casi controllati non hanno prodotto deadlock MySQL o lock timeout.

Non e quindi stato aggiunto un retry immediato. Il worker registra l'errore per elemento, elimina lo scope e riprova in una futura scansione se il database conserva InProgress. Il caso di fault ne verifica il recupero su contesto nuovo. Richieste API fallite terminano il proprio scope; non viene introdotto il riuso del DbContext dopo un errore. Il ChangeTracker non e una prova dello stato dopo rollback e non e usato per tale verifica.

Questo non dimostra il comportamento di tutti gli errori transienti possibili: non sono stati forzati codici MySQL 1205/1213 o indisponibilita del server. Autorizzazione, validazione e conflitti di finalizzazione non ricevono nuovi retry.

## 9. Verifica finale

Build completa della soluzione riuscita. Nessun errore; restano gli avvisi nullable preesistenti quando i progetti vengono ricompilati. `git diff --check` senza errori. Nessuna modifica ai 110 test precedenti per aggirare regressioni.

| Esecuzione | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Baseline iniziale: unit | 29 | 0 | 0 |
| Baseline iniziale: integration | 81 | 0 | 0 |
| Nuovi casi Phase3, filtro dedicato | 21 | 0 | 0 |
| Prima esecuzione completa: unit | 29 | 0 | 0 |
| Prima esecuzione completa: integration | 102 | 0 | 0 |
| Seconda esecuzione completa: unit | 29 | 0 | 0 |
| Seconda esecuzione completa: integration | 102 | 0 | 0 |

**131/131 in entrambe le esecuzioni consecutive**, inclusi i 110 test della baseline. MySQL realmente disponibile. Nessuno skipped contato come passed. I contatori passed/failed/notExecuted sono stati riletti dai TRX finali.

Comandi:

```text
dotnet build Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx --no-restore --verbosity quiet
dotnet test tests/Integration/Integration.Tests.csproj --no-restore --filter FullyQualifiedName~Phase3 --logger "trx;LogFileName=phase3-new.trx"
dotnet test Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx --no-build --logger "trx;LogFileName=phase3-full-1.trx" --verbosity quiet
dotnet test Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx --no-build --logger "trx;LogFileName=phase3-full-2.trx" --verbosity quiet
```

TRX sotto `tests/Unit/TestResults/` e `tests/Integration/TestResults/` con i nomi sopra; baseline `phase3-baseline.trx`.

## 10. Limiti e problemi residui

Nessun test fallito e nessun difetto funzionale residuo dimostrato nei flussi coperti. La sola correzione produttiva riguarda il lifetime DI del clock nel worker; la disponibilita internal del batch e per testabilita.

La prova di recupero e un arresto controllato seguito da una nuova istanza con esecuzione deterministica del batch. **Non e un crash reale, non verifica durabilita dopo kill/power loss, non attende il tick PeriodicTimer e non simula crash/riavvio MySQL o perdita di rete durante commit.** Le interleaving controllate non sono una prova di assenza universale di deadlock sotto ogni carico. Non e stata introdotta alcuna infrastruttura per promettere tali garanzie ulteriori.

Schema, FK, formula ConvertToGrade, PassingPercent, valori SuggestedGrade/Grade, override, audit JSON, semantica di idempotenza, autorizzazioni e visibilita Fase 2C restano invariati. Fase 4 non iniziata.
