# Fase 4 — Recuperi, ciclo di vita e API degli esami

## 1. Base e baseline effettiva

Branch locale: `refactor-database-entities`.
Commit iniziale: **`9b10368` — test: complete Phase 3 transaction and worker resilience**.
Workspace inizialmente pulito. Nessun reset, checkout o pull; il lavoro parte dalla Fase 3 locale.

Prima delle modifiche: **29 unit + 102 integration = 131 Passed, 0 Failed, 0 Skipped**. MySQL realmente disponibile. TRX iniziali: `tests/Unit/TestResults/phase4-baseline.trx` e `tests/Integration/TestResults/phase4-baseline.trx`.

## 2. Analisi e matrice dei gap

Esaminati controller, manager e service Exam, relative interfacce e repository, modelli/stati, DTO di creazione/update/assegnazione/lettura, mapper di creazione e lettura, manager submission, handler globale, repository/modello audit, fixture/seed, test precedenti e host HTTP Fase 2C. Tutti gli endpoint richiesti esistevano: nessun nuovo endpoint o controller.

| Funzionalita | Stato iniziale | Intervento / prova |
| --- | --- | --- |
| Grading, submit, review, finalize, visibilita e rollback delle submission | Gia implementato e testato | Invariati; tutti i 131 test precedenti passano |
| Creazione recupero con parent esistente e stessa ClassSubject | Implementato ma non sufficientemente testato | Test HTTP, parent mancante/altra ClassSubject, indipendenza originale/recupero |
| Update RetakeOfExamId | Incompleto: DTO ignorato | Collegamento modificabile/rimuovibile solo Draft; validazione completa e persistenza |
| Update ClassSubjectId | Bug: cambio ignorato | Relazione mantenuta immutabile e cambio rifiutato esplicitamente con 400 |
| Assegnazioni UpdateStudents | Bug: confronto Student.ClassId con Exam.ClassSubjectId | Confronto con ClassSubject.ClassId, controlli null e test di persistenza/rollback |
| Sostituzione domande Draft | Bug dimostrato: 500/DbUpdateConcurrencyException | Nuove righe esplicitamente Added; sostituzione e proprieta nello stesso confine transazionale |
| Chiusura | Bug: assenza controllo transizione | Solo Published -> Closed; altre origini rifiutate |
| Avvio dopo Finalized | Bug: lasciava passare il controllo prima del vincolo univoco DB | Ogni tentativo esistente impedisce un nuovo start sullo stesso Exam |
| Publish: totale e configurazione voto | Controlli incompleti | Totale coerente con domande; validazione soglia coerente con il dominio della formula |
| Publish: domande, date, studenti, ownership | Implementato ma non sufficientemente testato | Revalidazione anche delle assegnazioni/relazione recupero prima della pubblicazione |
| Audit publish/close/cancel | Incompleto: assente | Tre eventi atomici con stato, attore e before/after |
| Delete originale referenziato | Incompleto: FK avrebbe potuto azzerare il collegamento al recupero | Rifiuto applicativo 409 prima della cancellazione |
| Autorizzazioni HTTP sul ciclo di vita | Implementato ma non sufficientemente testato | Matrice anonimo/studente/non-owner, admin e assegnati/non assegnati |

Nessuna funzionalita corretta e gia testata e stata reimplementata. La sostituzione delle assegnazioni nel repository e stata verificata e mantenuta nella forma originale, evitando una riscrittura non necessaria.

## 3. Bug corretti e prove inizialmente rosse

I primi test hanno riprodotto: start consentito dal service dopo Finalized; pubblicazione con TotalPoints incoerente; UpdateStudents valido restituito 400; chiusura Draft/Cancelled restituita 204; audit publish assente; update bozza restituito 500.

La chiamata applicativa diretta all'update ha mostrato `DbUpdateConcurrencyException` in ReplaceStructureAsync: le nuove domande con Guid preassegnato, aggiunte soltanto alla navigation di un'entita esistente, non erano esplicitamente marcate Added. Ora vengono aggiunte al DbSet. Le vecchie domande sono eliminate con un primo SaveChanges prima delle nuove insert, evitando anche conflitti sull'indice univoco delle posizioni; l'intera sostituzione e l'update dei metadati sono nella medesima transazione. Un fault dopo la cancellazione SQL delle vecchie domande ne dimostra il ripristino.

UpdateStudents confrontava realmente gli ID sbagliati e dereferenziava elementi null prima della validazione. Corretto il ClassId usato e aggiunto il rifiuto esplicito di elementi null.

Il controllo ExamService sul PassingPercent accettava 100, mentre ConvertToGrade rifiuta tale valore. Il nuovo test dimostra entrambe le condizioni: ora la configurazione invalida viene rifiutata con 400 prima di poter raggiungere il grading. **La formula ConvertToGrade non cambia; non vengono riscritti PassingPercent persistiti o voti.** Rimane il dominio valido strettamente fra 0 e 100 richiesto dalla conversione delle fasi precedenti.

## 4. Recuperi: funzionalita completata

Il recupero resta un normale Exam autonomo con proprio Guid e RetakeOfExamId. Domande, orari, durata, stato, assegnazioni, submission, Grade e audit sono indipendenti.

In creazione, update Draft e pubblicazione viene percorsa la catena degli originali: ogni riferimento deve esistere ed essere della stessa ClassSubject. Un insieme di ID visitati rifiuta auto-riferimenti e cicli, anche indiretti. La modifica e la rimozione del collegamento sono consentite solo in Draft; Published/Closed/Cancelled non sono aggiornabili tramite gli endpoint Draft.

ClassSubjectId di un Exam esistente e immutabile: un DTO con un valore diverso viene rifiutato, anche per admin. Non si introducono migrazioni di classe, assegnazioni o ownership implicite.

Il test end-to-end crea e completa un originale con Grade 5, poi un recupero con Grade 9. Verifica submission e GradeId distinti, Grade originale ancora 5, snapshot della submission originale identico, audit submit/finalize separati e visibilita del risultato ufficiale corretta. Attraversa realmente Start -> SaveAnswers -> Submit -> Review -> Finalize -> Result via HTTP e MySQL.

Non sono introdotte sostituzione del voto precedente, iscrizione automatica degli studenti dell'originale, requisito automatico di insufficienza o obbligo che l'originale sia Closed. Sono politiche non definite e rimangono tali.

## 5. Transizioni e semantica dei retry

| Origine | Operazione | Regola implementata |
| --- | --- | --- |
| Draft | Update / UpdateStudents | Consentita con input valido |
| Draft | Publish | Consentita solo dopo tutte le validazioni |
| Draft | Delete | Consentita senza submission e senza recuperi che lo referenzino |
| Draft | Close / Cancel | 409 |
| Published | Update / UpdateStudents / Delete | 409 |
| Published | Publish | 409: mantenuto il contratto preesistente di non ripubblicare |
| Published | Close | Consentita, anche prima della fine della finestra e con submission InProgress |
| Published | Cancel | Consentita solo senza alcuna submission iniziata |
| Closed | Update / UpdateStudents / Publish / Close / Cancel / Delete | 409 |
| Cancelled | Update / UpdateStudents / Publish / Close / Cancel / Delete | 409 |

La seconda chiusura viene rifiutata con 409, senza modificare timestamp o audit. La chiusura e un'operazione amministrativa sullo stato Exam: **non consegna, annulla o finalizza automaticamente submission InProgress e non ne cambia deadline o risposte**. Il test verifica che il processor automatico possa ancora consegnare quella submission alla sua deadline dopo la chiusura. Worker e timer sono invariati.

Le mutazioni Exam e Start usano ITransactionManager e una lettura corrente con lock della riga Exam. Questa serializzazione evita doppie transizioni/audit e la race fra cancel e start. Non vengono cambiati lock o confini transazionali delle submission validati in Fase 3.

Tre prove con le barriere gia esistenti dimostrano: due publish concorrenti producono un solo cambiamento/audit; se cancel precede start non nasce una submission; se start precede cancel, cancel viene rifiutato e l'esame resta Published.

## 6. Assegnazioni e avvio studente

Studente esistente, classe corretta, assenza duplicati, ID non vuoto ed ExtraTimeMinutes non negativo vengono validati usando la ClassSubject effettiva. Assegnazioni modificabili solo in Draft, compresa la lista vuota (che impedisce poi Publish finche non viene assegnato uno studente). Elementi/lista null sono input non validi.

Il recupero puo essere creato senza assegnazioni e assegnato esplicitamente a uno studente diverso dall'originale. Il test verifica che nessuno venga copiato automaticamente e che admin possa creare, aggiornare la bozza, assegnare e pubblicare. L'originale mantiene le proprie assegnazioni.

GET assigned mostra solo esami non Draft assegnati al chiamante. Closed/Cancelled rimangono visibili nella consultazione personale secondo il contratto esistente, con CanStart=false. Un non assegnato non vede il recupero e non puo avviarlo; Start conserva il 404 esistente per assegnazione non trovata. Ruolo errato -> 403, assenza autenticazione -> 401.

Start richiede Published e finestra corrente. Qualunque submission preesistente, in tutti e cinque gli stati, blocca un secondo tentativo sullo stesso Exam con un errore applicativo, senza tentare un secondo inserimento. Il recupero e un altro Exam. Il tempo aggiuntivo viene persistito e incluso nella deadline; il flusso HTTP prova 30+5 minuti.

## 7. Audit e atomicita

Eventi aggiunti: **exam.publish**, **exam.close**, **exam.cancel**.
Ogni evento contiene ActorUserId persistito, EntityType=`exam`, EntityId, CreatedAt UTC e BeforeJson/AfterJson con Status, PublishedAt, ClosedAt, CancelledAt e RetakeOfExamId. Stato e audit sono salvati nello stesso DbContext/transazione tramite i repository esistenti. Errori e richieste ripetute senza transizione non creano nuovi eventi.

Tre test riusano `Phase3.SaveProbe`: dopo il SaveChanges dell'audit leggono da MySQL lo stato finale e l'evento realmente scritto, poi iniettano l'errore prima del commit. Dopo rollback, un nuovo scope/DbContext verifica lo snapshot iniziale identico, inclusi stato, timestamp e audit.

Altri tre casi verificano rollback della bozza dopo la cancellazione SQL della struttura, dopo il salvataggio dei nuovi metadati e dopo l'aggiornamento SQL del tempo aggiuntivo. Nessun flag produttivo, mock di persistenza o database sostitutivo.

Audit submission.submit/finalize/override invariati e non duplicati. Per Delete Draft non viene introdotto un nuovo evento o una nuova politica di conservazione: le transizioni auditate riguardano stati non eliminabili dall'endpoint Draft. Eventuali record audit storici non sono cancellati dal codice produttivo di Delete.

Il vincolo FK del recupero e SetNull: il controllo HasRetakes impedisce di cancellare l'originale invece di perdere silenziosamente la relazione. Nessuna modifica a FK/schema e nessuna cancellazione a cascata di recuperi, submission o Grade. La cancellazione di una bozza non referenziata conserva il comportamento esistente sui suoi soli dati di struttura/assegnazione.

## 8. Endpoint verificati e contratti HTTP

Nessuna route aggiunta o duplicata:

- POST `/api/Exam`: creazione normale/recupero, 201 e Location.
- PUT `/api/Exam/{id}`: bozza, collegamento recupero e rifiuti di stato/input.
- PUT `/api/Exam/{id}/students`: persistenza assegnazioni e rifiuti.
- POST `/api/Exam/{id}/publish`, `/close`, `/cancel`: transizioni e audit.
- DELETE `/api/Exam/{id}`: bozza libera, riferimenti, stati non consentiti.
- GET `/api/Exam/{id}`, `/api/Exam`, `/api/Exam/assigned`: dettaglio, elenco filtrato per docente e assegnazioni studente.
- POST `/api/Exam/{id}/start`: normale/recupero, extra time e autorizzazioni/finestra/tentativi.
- Percorso HTTP save/submit/review/finalize/result del recupero e dell'originale.

Verificati 400 input, 401 anonimo, 403 ruolo/ownership, 404 risorsa/assegnazione mancante, 409 stato incompatibile. La prova 500 usa il probe interno non sensibile gia presente nell'host Fase 2C; non rappresenta una simulazione di indisponibilita MySQL. GlobalExceptionHandler e controller non sono modificati.

## 9. File produttivi modificati

Sotto `Backend/ElectronicRegisterAPI/src/`:

- `Application/ElectronicRegisterAPI.Application/Managers/ExamManager.cs`: transazioni Exam/Start, validazione/persistenza recupero, ClassSubject immutabile, ClassId corretto, validazione publish, audit e guardia Delete.
- `Business/ElectronicRegisterAPI.Business/Services/ExamService.cs`: chiusura, blocco di tutti i tentativi preesistenti, totale coerente e limite configurazione PassingPercent.
- `Domain/ElectronicRegisterAPI.Domain/Interfaces/Services/IExamService.cs`: EnsureCanClose.
- `Domain/ElectronicRegisterAPI.Domain/Interfaces/Repositories/IExamRepository.cs`: lettura Exam con lock e verifica recuperi dipendenti.
- `Infrastructure/ElectronicRegisterAPI.Infrastructure/Repositories/ExamRepository.cs`: implementazione dei due metodi e corretta sostituzione delle domande.

Le estensioni alle interfacce esistenti servono a validare la transizione nel Business e a coordinare lock/FK nell'Application senza dipendenze EF fuori dall'Infrastructure. Nessun nuovo manager, repository, servizio DI o DTO. L'estensione del diff del manager deriva soprattutto dai blocchi transazionali intorno alle operazioni esistenti; nessuna state machine generica.

## 10. Test aggiunti e isolamento

- `tests/Unit/ExamLifecycleServiceTests.cs`: **10 casi** sulle regole pure, inclusi i cinque stati dei tentativi esistenti.
- `tests/Integration/Phase4_ExamLifecycleTests.cs` e `Phase4_ExamValidationAndAtomicityTests.cs`: **56 casi**, con HTTP reale, MySQL, test applicativi, rollback e tre interleaving concorrenti.
- `tests/Integration/SeedData.cs`: scenari isolati 40/41, con reset esistente dei dipendenti.

Totale nuovi test: **66**. Tutti i 131 precedenti restano invariati.

Le bozze create via API hanno Guid propri e vengono rimosse in teardown insieme ai soli record dipendenti del test; dati condivisi non eliminati. Le corruzioni controllate di dati per provare la revalidazione sono circoscritte agli scenari dedicati e ripristinate dove necessario. Fault interceptor/provider isolati e disposati. Nessuno Skip aggiunto, nessuna asserzione preesistente indebolita.

## 11. Build e due esecuzioni definitive

Comandi richiesti eseguiti sul codice finale:

```text
dotnet build Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx
dotnet test Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx --logger "trx;LogFileName=phase4-full-1.trx"
dotnet test Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx --logger "trx;LogFileName=phase4-full-2.trx"
```

Build riuscita. L'ultima build incrementale riporta 0 errori e 0 warning; gli avvisi nullable preesistenti dei vecchi file restano visibili quando quei progetti vengono ricompilati. `git diff --check` senza errori.

| Esecuzione | Totale | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: | ---: |
| Baseline unit | 29 | 29 | 0 | 0 |
| Baseline integration | 102 | 102 | 0 | 0 |
| Completa 1, unit | 39 | 39 | 0 | 0 |
| Completa 1, integration | 158 | 158 | 0 | 0 |
| Completa 2, unit | 39 | 39 | 0 | 0 |
| Completa 2, integration | 158 | 158 | 0 | 0 |

**197/197 Passed in entrambe le esecuzioni definitive consecutive.** MySQL effettivamente disponibile. I contatori total/passed/failed/notExecuted sono stati riletti dai TRX di ciascun progetto: zero notExecuted/skipped, non soltanto exit code positivo. TRX nelle cartelle TestResults di Unit e Integration con i nomi sopra.

## 12. Decisioni non definite e limiti delle prove

- **Non definite e non implementate:** sostituzione dei voti originali, eleggibilita automatica per insufficienza, copia automatica assegnazioni, tentativi multipli sullo stesso Exam, nuova politica di audit Delete. Il recupero resta indipendente e assegnato esplicitamente.
- **Decisioni esplicitate nel perimetro:** ClassSubject immutabile; collegamento recupero modificabile in Draft; Close solo da Published, anche anticipata/con submission attive; retry di Publish/Close/Cancel senza transizione restituisce 409.
- **Ottimizzazioni facoltative:** nessuna introdotta. Nessun refactoring stilistico, nuova infrastruttura, tabella o pacchetto.
- **Limiti:** host HTTP reale riusato dalla Fase 2C, non bootstrap completo di Program.cs; recovery del worker resta quello dimostrato dalla Fase 3, non crash reale. Le nuove prove controllate non dimostrano assenza universale di deadlock per ogni aggiornamento simultaneo di grafi di recupero; tali errori restano errori tecnici con rollback, senza retry indiscriminato. Nessun difetto residuo emerso nei casi validati.

Formula di grading, valori ufficiali/suggeriti, audit submission, override, idempotenza/conflitti, lock/transazioni delle submission, worker/scopes e visibilita Fase 2C sono invariati. Schema/scaffolding e frontend non modificati. Nessuna fase successiva iniziata.
