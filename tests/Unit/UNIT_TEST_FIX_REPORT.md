# Correzione dei 10 fallimenti unitari — 17 ottobre 2026

## Ambito e diagnosi

Esecuzione iniziale completa: **19 Passed, 10 Failed, 0 Skipped**.
Risultati originali e stack trace: `TestResults/unit-before.trx`.

Analizzati `ExamSubmissionProcessor`, `ExamSubmissionManager`, `SubmissionGradingService`, mapper, interfacce repository/servizi, implementazioni di salvataggio, avvio submit e transazione e fixture unitaria. La fixture è interna a `ExamSubmissionProcessorTests`; non utilizza la fixture MySQL.

Classificazione: **A** = mock/dati incompleti; **B** = aspettativa incompatibile con il contratto corrente; **C** = regressione produttiva. Nessuna modifica produttiva necessaria in questo intervento. Le modifiche produttive della Fase 2B già presenti sono conservate.

## Analisi per test

Le righe iniziali si riferiscono al file prima della correzione, come registrato nel TRX. Per tutti i test il file modificato è `tests/Unit/Application/ExamSubmissionProcessorTests.cs`.

| Test | Errore iniziale e riga | Atteso / effettivo | Causa e classificazione | Correzione |
| --- | --- | --- | --- | --- |
| `ClosedOnly_GradedAndGradeCreated` | `NullReferenceException`, processor:104, test:54 | Submission Graded e un Grade / eccezione prima del calcolo | **A**: `SaveAnswersAsync` non configurato restituisce null; mancavano anche avvio submit, conversione e lettura del Grade creato | Salvataggio con risultato Accepted e risposte persistite nella fixture; avvio condizionato allo stato; archivio Grade in memoria; conversione reale; verifiche su voto 10, SuggestedGrade, Passed, audit e attore manuale |
| `OpenQuestions_PendingReview_NoGradeCreated` | `NullReferenceException`, processor:104, test:74 | PendingReview senza Grade / eccezione durante salvataggio | **A**: risultato SaveAnswers assente; anche TryBeginSubmit non configurato | Configurati salvataggio e avvio; mantenuta assenza di Grade, aggiunte assenza di SuggestedGrade/conversione e presenza di audit |
| `FailedAuto_NoGradeCreated` | `NullReferenceException`, processor:104, test:92 | Stato FailedAuto senza Grade / eccezione prima della valutazione | **A**: salvataggio e avvio non configurati | Ripristinata raggiungibilità del ramo di fallback; verificati stato, nessun Grade e audit. Questo è un test isolato del ramo del processor: il servizio reale corrente non produce FailedAuto per un normale voto insufficiente |
| `DoubleSubmit_NoDuplicateGradeCreated` | `InvalidOperationException`: GradeId presente ma Grade non trovato, processor:61, test:108 | Restituzione voto esistente senza duplicati / stato incoerente | **A**: GradeId valorizzato ma nessun Grade disponibile nel mock di lettura nullable | Inserito Grade coerente nel deposito della fixture; verificati voto restituito 7, unico Grade e nessuna nuova scrittura, correzione o audit |
| `RequiredMissing_ManualRejected` | Attesa `InvalidOperationException`, ricevuta `NullReferenceException`, processor:104, test:142 | Rifiuto per domanda obbligatoria assente / errore SaveAnswers | **A**: il mock non permetteva di raggiungere il controllo obbligatorietà | Salvataggio vuoto accettato e avvio configurati; verificato messaggio relativo alle obbligatorie, nessun calcolo, Grade o audit |
| `RequiredMissing_AutoAllowed` | Assert.Equal, test:163 | Graded / InProgress | **A**: TryBeginSubmit non configurato restituisce false e attiva il ritorno idempotente; dati max=0 e PassingPercent=0, conversione assente | Avvio condizionale configurato; massimo 10 e soglia 60%; verificati voto minimo 1, Passed=false, audit di sistema e nessun salvataggio DTO |
| `AutoSubmit_NoDto_NoCaller_UsesPersistedAnswers` | Assert.Equal, test:181 | Graded / InProgress | **A**: avvio non configurato; conversione e dati non validi come sopra | Fixture coerente; verifica che il grading riceva proprio la risposta persistita, nessun SaveAnswers, unico Grade e ActorUserId=null |
| `PersistenceError_RollsBack_AndThrows` | Attesa `InvalidOperationException`, ricevuta `BusinessRuleException` per scadenza, processor:81, test:196 | Propagazione errore DB / rifiuto manuale per deadline default | **A**: deadline non impostata, quindi scaduta; inoltre salvataggio e avvio non configurati | Orologio fisso e deadline futura; raggiunta AddAsync che solleva DB error; verificata stessa eccezione nel delegato transazionale, nessun Grade memorizzato, aggiornamento o audit |
| `DeadlineReached_NoNewAnswersButExistingProcessed` | Assert.Equal, test:213 | Graded / InProgress | **A**: TryBeginSubmit false per default, conversione assente e dati invalidi | Deadline scaduta rispetto a orologio fisso; avvio configurato; verifica assenza SaveAnswers, voto minimo, audit di sistema e persistenza |
| `OpenAnswersEmpty_TreatedAsZeroPoints` | Assert.Equal, test:230 | Graded e zero punti / InProgress | **A**: avvio non configurato. **B** aggiuntiva: il test non conteneva alcuna domanda o risposta aperta e simulava Graded; il contratto corrente richiede review per OpenMaxPoints positivo anche con testo vuoto | Inserite vera domanda aperta da 5 punti e risposta vuota, usati grading e valutazione reali; mantenuta verifica zero punti aperti, verificati PendingReview, nessun voto o Passed ufficiale e nessun Grade creato |

## Contratti preservati e limiti della verifica

Il contratto B è esplicito in `SubmissionGradingService.EvaluateAfterSubmit`: `OpenMaxPoints > 0` comporta PendingReview. È coerente con la separazione fra SuggestedGrade e voto ufficiale della Fase 2B. Non è stata introdotta una nuova regola produttiva per correggere il test.

I mock restituiscono Task completati o risultati asincroni coerenti con le interfacce. Il deposito Grade consente che Add e GetById rappresentino lo stesso voto. TryBeginSubmit controlla stato e SubmittedAt, quindi non restituisce incondizionatamente true. L'orologio è fisso. La conversione delega alla formula reale, già coperta dai 18 casi di test del servizio, evitando voti scelti solo per soddisfare le asserzioni.

Il mock di ITransactionManager esegue e attende il delegato, propagando gli errori. Il test di persistenza verifica il confine transazionale e l'interruzione del flusso; **non dimostra un rollback MySQL reale**, nonostante il nome storico del test. Non sono stati modificati lock, transazioni, schema o scaffolding per simulare tale garanzia.

Nessun test eliminato, ignorato o marcato skipped. Nessuna modifica produttiva, nessuna Fase 2C. Le 57 integrazioni sono state realmente eseguite con MySQL disponibile.

## Verifica finale

| Esecuzione | Passed | Failed | Skipped | Risultati |
| --- | ---: | ---: | ---: | --- |
| Dieci fallimenti originali, filtro ricavato dal TRX iniziale | 10 | 0 | 0 | `TestResults/unit-original-ten.trx` |
| Unit completa, esecuzione 1 | 29 | 0 | 0 | `TestResults/unit-final-1.trx` |
| Integration completa, esecuzione 1 | 57 | 0 | 0 | `../Integration/TestResults/integration-final-1.trx` |
| Unit completa, esecuzione 2 | 29 | 0 | 0 | `TestResults/unit-final-2.trx` |
| Integration completa, esecuzione 2 | 57 | 0 | 0 | `../Integration/TestResults/integration-final-2.trx` |

Comandi: `dotnet test tests/Unit/Unit.Tests.csproj --no-restore` e `dotnet test tests/Integration/Integration.Tests.csproj --no-restore`, con logger TRX per ciascuna esecuzione. Per i dieci originali: `--no-build --filter` con i dieci nomi estratti dai risultati Failed iniziali.
