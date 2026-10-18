# ElectronicRegister - aggiornamento regole di business dopo Fase 4

Validazione del 18 ottobre 2026. Baseline dichiarata: 197 test (39 unit, 158 integration).
Risultato finale: **211/211 Passed per due esecuzioni complete consecutive**, con MySQL reale MAMP su 127.0.0.1:3306, database dedicato `electronic_register_test`. Zero Failed e zero Skipped.

## Modifica produttiva

`ExamManager.CloseAsync` mantiene la transazione esistente e acquisisce prima il lock della riga Exam tramite `GetByIdForUpdateAsync`, esattamente come Start. Dopo autorizzazione e validazione Published, invoca il nuovo `IExamRepository.HasInProgressSubmissionsAsync` prima di modificare stato, ClosedAt o audit.

Il repository esegue `AnyAsync(s => s.ExamId == examId && s.Status == "in_progress")` sul DbSet MySQL. Non usa navigation collection, dati tracciati o filtri sulla deadline. Richiede una transazione attiva. Se la query trova una submission, il manager solleva BusinessRuleException, mappata dal gestore HTTP esistente a 409; la transazione viene annullata. Nessuna modifica a submission, risposte, stato Exam, ClosedAt o audit.

PendingReview, Graded, Finalized e FailedAuto non bloccano; uno studente assegnato senza submission non blocca. Anche una InProgress scaduta blocca finche la consegna non viene completata.

File produttivi modificati: `ExamManager.cs`, `IExamRepository.cs`, `ExamRepository.cs`. Nessuna modifica a Start, Submit, worker, grading, schema, scaffolding, frontend, DI o infrastruttura produttiva.

## Analisi della concorrenza

Ordine: inizio transazione -> lock Exam -> autorizzazione/validazioni -> query SQL InProgress -> aggiornamento Exam e audit -> commit. Il lock Exam resta detenuto fino a commit/rollback.

- **Start precede Close:** Start conserva il lock Exam fino al commit dell'inserimento. Close attende quel lock. Le letture consistenti della nuova transazione Close avvengono dopo l'acquisizione del lock: anche con REPEATABLE READ, lo snapshot include lo Start appena committato. La query vede InProgress e rifiuta Close.
- **Close precede Start:** Close detiene il lock Exam durante query, aggiornamento e commit. Start attende e legge poi Closed, quindi viene rifiutato senza creare submission.
- **Submit/worker:** conservano i propri lock e transazioni. Possono solo far uscire una submission da InProgress; non ne creano di nuove e non riportano submission terminate a InProgress. Se Close vede ancora InProgress, rifiuta conservativamente; dopo il commit della consegna il retry riesce. Se vede la consegna committata, puo chiudere. Non e possibile committare Closed lasciando InProgress preesistenti o create da Start nel frattempo.

Non si aggiungono lock sulle submission. Una prima implementazione sperimentale con `FOR UPDATE` anche su exam_submission ha prodotto un deadlock nella prova concorrente con Submit manuale: i lock della struttura Exam e quelli delle risposte/submission potevano interagire in ordine inverso. Quella implementazione e stata rimossa. La query di esistenza non bloccante mantiene la garanzia fornita dal lock Exam senza introdurre tale dipendenza. Non sono stati aggiunti retry per nascondere deadlock.

La correttezza dello snapshot dipende dall'ordine esistente: non introdurre letture consistenti nella transazione Close prima del lock Exam. Le verifiche persistite usano nuovi scope e DbContext. Le barriere `Phase3.OrderedAsync` sospendono una transazione dopo SQL reale e avviano la seconda prima di rilasciare la prima: non si basano su ritardi arbitrari. Le prove concorrenti di consegna accettano solo successo oppure BusinessRuleException, verificando stato/ClosedAt/audit coerenti e successo del retry dopo il commit. Le prove non dimostrano assenza universale di deadlock per ogni carico possibile.

## Copertura e test esistenti

Nuovo file `tests/Integration/ExamBusinessRulesUpdateTests.cs`, parte della classe fixture esistente: **14 casi aggiunti**.

| Requisito | Copertura |
| --- | --- |
| 1. Published senza InProgress | `CloseBusinessRule_HttpAndMySql("none")`: assegnazione senza Start, HTTP 204 e persistenza |
| 2. Una InProgress | caso `in_progress`: HTTP 409 |
| 3. Piu submission, almeno una attiva | caso `mixed`: InProgress + PendingReview, HTTP 409 |
| 4. Solo PendingReview | caso `pending_review`: HTTP 204 |
| 5. Graded/Finalized | casi `graded`, `finalized`, `completed` (entrambe), HTTP 204 |
| Stato FailedAuto | caso `failed_auto`: HTTP 204 |
| 6. InProgress scaduta | caso `expired` e test worker: HTTP 409 |
| 7. Dopo consegna automatica ultima attiva | `ExpiredLastSubmission_WorkerDeliveryAllowsHttpClose`: batch reale del worker, timestamp/reason persistiti, poi HTTP 204 |
| 8. Rifiuto senza effetti | snapshot Exam/audit e submission invariati, ClosedAt nullo, nessun exam.close in tutti i casi bloccati; snapshot completo nel test worker |
| 9. Start/Close concorrenti | `CloseAndStart_AreSerializedInBothOrders`: due casi con barriere MySQL; conteggi submission e audit, stato e ClosedAt da nuovo contesto |
| Interazione con consegna | `CloseDuringSubmit_PreservesInvariantAndAllowsRetry`: due casi, manuale e automatica, transazioni sovrapposte |
| 10. Entrambi i Grade persistiti | test esistente `RetakeFullHttpFlow_KeepsOriginalGradeAndSubmissionIndependent`, invariato |
| 11. Secondo Start rifiutato | teoria unit esistente sui cinque stati e flusso HTTP originale/recupero esistente, invariati |

La copertura recupero esistente esegue Start/Save/Submit/Review/Finalize/Result per originale e recupero, legge da MySQL le submission con GradeId distinti, verifica Grade originale ancora 5 e risultato ufficiale recupero 9, e confronta lo snapshot originale prima/dopo. Non servono duplicazioni di quei test. La logica dei tentativi e la persistenza dei voti non sono state modificate.

**Unico test preesistente modificato:** `CloseWithActiveSubmission_PreservesIt_AndAutomaticDeliveryStillWorks`, rinominato `CloseWithActiveSubmission_IsRejected_AndClosesAfterAutomaticDelivery`. L'aspettativa iniziale Close 204 diventa 409, con snapshot Exam/audit invariato. Restano le verifiche sulla submission e la consegna automatica; la chiusura 204 e la verifica del singolo audit vengono eseguite dopo la consegna. E l'unica aspettativa precedente incompatibile con la nuova specifica. Nessun test eliminato, disabilitato o indebolito; gli altri 196 restano invariati.

I nuovi Exam hanno Guid propri e usano il teardown gia esistente. Le prime prove aggiuntive che usavano l'Exam seed hanno rilevato audit residui tra casi; sono state corrette usando Exam creati per ogni test, senza cambiare fixture o seed condivisi. Le due esecuzioni definitive sono successive a tutte le correzioni.

## Build e validazione finale

Build completa della soluzione riuscita, zero errori. Nelle ricompilazioni sono presenti warning nullable preesistenti in `StudyPathRepository.cs`, `OpenScoreDoubleCountTests.cs` e `Phase2B_AdditionalTests.cs`; nessun nuovo warning nei file modificati. `git diff --check` senza errori di whitespace.

Comandi dalla cartella `Backend/ElectronicRegisterAPI`, con `TEST_MYSQL_CONNECTION` impostata nella sessione PowerShell alla connessione MAMP fornita dall'utente (credenziali non salvate nel repository):

```powershell
dotnet build ElectronicRegisterAPI.slnx
dotnet build ElectronicRegisterAPI.slnx --no-restore
# Per ciascuno di N=1,2, senza modifiche al codice tra le esecuzioni:
dotnet test ../../tests/Unit/Unit.Tests.csproj --no-build --logger:trx --results-directory ./validation-results/final-N/unit
dotnet test ../../tests/Integration/Integration.Tests.csproj --no-build --logger:trx --results-directory ./validation-results/final-N/integration
```

| Esecuzione | Suite | Totale | Passed | Failed | Skipped/notExecuted |
| --- | --- | ---: | ---: | ---: | ---: |
| 1 | Unit | 39 | 39 | 0 | 0 |
| 1 | Integration MySQL | 172 | 172 | 0 | 0 |
| 2 | Unit | 39 | 39 | 0 | 0 |
| 2 | Integration MySQL | 172 | 172 | 0 | 0 |

**197 + 14 = 211 test**, tutti superati in entrambe le esecuzioni consecutive. Contatori verificati nei quattro TRX, conservati in `tests/Integration/TestResults/business-rules/` con nomi `unit-1.trx`, `integration-1.trx`, `unit-2.trx`, `integration-2.trx`.

L'host HTTP e quello Kestrel/JWT/MySQL gia usato dalla Fase 4. Il test worker invoca il vero `ProcessBatchAsync` usato dal timer, con clock controllato e scope reali; non attende il tick e non simula un crash. MAMP e stato usato per tutte le esecuzioni definitive. I file temporanei dell'istanza MySQL isolata preparata prima della disponibilita di MAMP sono stati rimossi; nessun nuovo servizio o database produttivo introdotto. Nessuna fase successiva avviata.
