# Fase 2C — API di correzione e visibilità dei risultati

Branch: `refactor-database-entities`. Base: `f32a33b` (`test: complete Phase 2B with 86 passing tests`).

## Analisi iniziale

Esaminati controller e base API, handler globale, interfacce e implementazioni dei manager, mapper, DTO di review/summary/result/input, ClaimsContext, DI, servizi di grading e test di sicurezza/integrazione. Le quattro route di correzione non esistevano; `IExamSubmissionManager` era già registrato. Esistevano già start, salvataggio, submit, result e gestione esami: nessuna route duplicata e nessun controller aggiuntivo.

Le lacune erano: dati provvisori visibili nei risultati e negli elaborati personali; FinalGrade sempre null; SubmitReason e ViolationCount fissi nella lista; commenti non disponibili all'admin; conflitti applicativi indistinguibili dagli InvalidOperationException interni nell'handler HTTP. Il mapper per start usa DTO studente privi di IsCorrect; il dettaglio completo dell'esame è già limitato a teacher/admin. Gli esami assegnati non contengono voti o opzioni corrette.

## Endpoint aggiunti

Tutti in `ExamController`, tutti autenticati e limitati a `teacher,admin`:

| Metodo | Route | Manager | Risposta di successo |
| --- | --- | --- | --- |
| GET | `/api/Exam/{examId}/submissions?status=PendingReview` | GetByExamAsync | 200, lista SubmissionSummaryDto |
| GET | `/api/Exam/submission/{id}/review` | GetByIdAsync | 200, SubmissionReviewDto |
| PUT | `/api/Exam/submission/{id}/review` | ReviewAsync | 204 |
| POST | `/api/Exam/submission/{id}/finalize` | FinalizeAsync | 204, anche per retry identico |

Il filtro status è opzionale. Nomi non riconosciuti e valori numerici fuori enum producono 400. La logica di validazione/correzione resta applicativa.

## Visibilità e DTO

Nessuna definizione DTO modificata o proprietà aggiunta. Corretti i mapping di:

- SubmissionSummaryDto: SubmitReason e ViolationCount reali.
- SubmissionReviewDto: FinalGrade da Grade.Value persistito; commenti disponibili anche all'admin.
- SubmissionResultDto: SubmitReason reale; Grade dal repository, mai da SuggestedGrade.

La policy statica interna `SubmissionVisibility` è condivisa dal mapper dei risultati e dalla proiezione di review. Tutti i ritorni del processor, inclusi quelli idempotenti, passano il caller al mapper; GetResultAsync usa lo stesso percorso. Nessuna modifica alle firme pubbliche dei manager o del processor.

| Stato | Visibilità studente |
| --- | --- |
| InProgress | Elaborato personale accessibile dal manager; nessun voto o dato provvisorio |
| PendingReview | Nessun voto, SuggestedGrade, punteggio provvisorio o Passed provvisorio, anche dopo review completa |
| FailedAuto | Nessun risultato ufficiale o valore provvisorio pubblicato |
| Graded | Voto disponibile solo se esiste il Grade persistito |
| Finalized | Voto disponibile solo se esiste il Grade persistito, incluso dopo override |

La presenza dello stato Graded/Finalized senza Grade non rende pubblicabili i dati. Quando il voto esiste, Passed restituito segue Grade.Value >= 6. Lo studente non vede mai SuggestedGrade, identità del revisore, commenti interni, IsCorrect delle risposte/opzioni o AwardedPoints per risposta. Prima del voto sono oscurati anche i punteggi aggregati, GradeId e FinalizedAt. Testo delle domande, opzioni selezionate e risposte personali restano accessibili tramite il contratto applicativo già esistente. OverrideReason non è presente nei DTO restituiti allo studente.

La lettura di FinalGrade richiede una query al repository Grade solo se GradeId è presente: il dato non era altrimenti disponibile nella submission. Nessun nuovo servizio o repository DI.

## Autorizzazioni ed errori

Ownership verificata dal manager per lista, lettura, review e finalize: teacher solo sulla propria ClassSubject; admin autorizzato. Per lo studente, GetResult/GetById richiedono la propria StudentId. Ruoli non previsti sono respinti. La base API rifiuta claim identitari mancanti o malformati e identificativi di ruolo assenti, evitando query non filtrate anche nei percorsi Grade già esistenti.

| Caso | HTTP |
| --- | ---: |
| Input non valido, punti/voto fuori intervallo, override senza motivo necessario | 400 |
| JWT assente | 401 |
| Ruolo vietato, ownership errata, claim di ruolo mancanti in identità autenticata | 403 |
| Risorsa inesistente | 404 |
| Review/finalize incompatibile con lo stato, risposte non revisionate, richiesta finale differente | 409 |
| Errore interno imprevisto o incoerenza GradeId/Grade | 500 |

`SubmissionErrors` classifica esplicitamente i rifiuti applicativi mediante un marcatore interno in Exception.Data. Questo mantiene il tipo esatto InvalidOperationException previsto dai contratti e test della Fase 2B, distinguendo 400/409 senza confrontare messaggi o convertire indiscriminatamente tutti gli InvalidOperationException in 409. Le incoerenze interne rimangono 500. BusinessRuleException conserva il mapping esistente a 409. I ProblemDetails non riportano i messaggi originali delle eccezioni, evitando dettagli interni.

## File dell'intervento

Produzione, sotto `Backend/ElectronicRegisterAPI/src/`:

- `Api/ElectronicRegisterAPI/Controllers/ExamController.cs`
- `Api/ElectronicRegisterAPI/Controllers/ApiControllerBase.cs`
- `Api/ElectronicRegisterAPI/ExceptionHandling/GlobalExceptionHandler.cs`
- `Application/ElectronicRegisterAPI.Application/Managers/ExamManager.cs`
- `Application/ElectronicRegisterAPI.Application/Managers/ExamSubmissionManager.cs`
- `Application/ElectronicRegisterAPI.Application/Mappers/ExamSubmissionMapper.cs`
- `Application/ElectronicRegisterAPI.Application/Mappers/SubmissionVisibility.cs` (nuovo)
- `Application/ElectronicRegisterAPI.Application/Services/ExamSubmissionProcessor.cs` (solo passaggio del caller alle tre proiezioni di risultato)
- `Domain/ElectronicRegisterAPI.Domain/Exceptions/SubmissionErrors.cs` (nuovo)

Test e documentazione:

- `tests/Integration/Integration.Tests.csproj`: riferimento al progetto API e framework ASP.NET Core, nessun nuovo pacchetto NuGet.
- `tests/Integration/IntegrationFixture.cs`: connection string disponibile internamente ai test HTTP dopo la selezione/validazione del database di test.
- `tests/Integration/Phase2C_HttpHost.cs` (nuovo): Kestrel su loopback/porta dinamica, JWT firmati di test, MVC, DI applicativa, repository MySQL e handler reali.
- `tests/Integration/Phase2C_ApiTests.cs` (nuovo).
- `tests/Integration/Phase2C_VisibilityTests.cs` (nuovo).
- Questo report.

Il test host costruisce la pipeline HTTP con le registrazioni produttive; non avvia direttamente Program.cs. Il worker è escluso soltanto dal test host per non elaborare scenari durante le verifiche. Program.cs e worker produttivo non sono modificati. Il database viene dalla fixture esistente: container MySQL oppure esclusivamente il database locale autorizzato `electronic_register_test`. Nessun mock/in-memory database e nessun nuovo Skip.

## Nuovi test

**24 casi d'integrazione aggiunti**, contando le righe delle Theory:

| Gruppo | Casi |
| --- | ---: |
| Quattro API vietate a studenti e anonimi | 4 |
| Quattro operazioni vietate al docente non proprietario, sia HTTP sia manager | 4 |
| Docente proprietario e admin: review, override, retry, conflitto su voto/motivo, immutabilità voto/timestamp/attore e audit singolo | 2 |
| Finalizzazione ordinaria senza override | 1 |
| Summary e filtro stato con metadati reali | 1 |
| Identificativi di ruolo mancanti | 1 |
| Input/errori 400, 404, 409, 500 e dettagli non sensibili | 1 |
| Redazione di campi provvisori nei cinque stati senza Grade ufficiale | 5 |
| Elaborato personale InProgress e PendingReview dopo review completa | 2 |
| Isolamento dei risultati altrui e divieto review applicativa allo studente | 1 |
| Submit HTTP: PendingReview e Graded con Grade persistito | 2 |

Il test preesistente Student_CannotSeeCorrectOptions rimane invariato e passa. I test della baseline non sono stati modificati per aggirare regressioni.

## Validazione effettiva

Build completa: `dotnet build Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx --no-restore`, riuscita. Restano avvisi di nullabilità preesistenti; nessun errore di build.

| Esecuzione | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Integrazioni baseline, filtro esclusione Phase2C | 57 | 0 | 0 |
| Nuovi test Phase2C, filtro dedicato | 24 | 0 | 0 |
| Soluzione completa, prima esecuzione: unit | 29 | 0 | 0 |
| Soluzione completa, prima esecuzione: integration | 81 | 0 | 0 |
| Soluzione completa, seconda esecuzione: unit | 29 | 0 | 0 |
| Soluzione completa, seconda esecuzione: integration | 81 | 0 | 0 |

Due esecuzioni consecutive di `dotnet test Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx --no-build`, con TRX distinti `phase2c-full-1.trx` e `phase2c-full-2.trx` nelle cartelle TestResults di ciascun progetto. Risultato: **110/110 in entrambe**, inclusa la baseline originale **86/86**. MySQL effettivamente disponibile; nessuno skipped contato come passed. Risultati dei filtri in `phase2c-baseline.trx` e `phase2c-new.trx`.

## Vincoli e problemi residui

Solo interventi indispensabili alla Fase 2C; nessuna ottimizzazione facoltativa o refactoring generale. Invariati formula, PassingPercent, regole di override, semantica dei retry/concorrenza, lock, transazioni, schema, scaffolding, worker e frontend. Nessuna Fase 2D e nessuna funzione Google Forms.

Nessun problema funzionale residuo emerso dalla validazione. La verifica HTTP copre controller/middleware/manager/repository reali, ma non il bootstrap di Program.cs o il worker, esclusi dal test host come descritto sopra.
