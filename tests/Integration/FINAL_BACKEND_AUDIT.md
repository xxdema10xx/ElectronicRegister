# ElectronicRegister - Final Backend Audit

Data: 18 ottobre 2026. Ambito: cinque progetti backend, otto controller, 73 operazioni HTTP; nessuna nuova fase funzionale.

## 1. Executive Summary

**Il codice e sufficientemente pronto per iniziare l'integrazione frontend, dopo le correzioni di questo audit. Il deployment produttivo non e certificato.**

La baseline verde nascondeva problemi reali fuori dai percorsi principali Esami: validazione issuer Microsoft assente, letture Student senza filtro docente, un percorso Subject incoerente, modifica/cancellazione dei Grade d'esame dal CRUD generico, scritture account non atomiche e contratti di creazione/OpenAPI errati. Sono stati riprodotti prima delle rispettive correzioni; le prove fallite sono conservate.

Tutti i problemi HIGH confermati sono corretti e verificati. Non e stata dimostrata una vulnerabilita CRITICAL o corruzione indipendente dalle condizioni indicate nei finding. Restano rischi MEDIUM di configurazione, volume, policy e copertura: non sono dichiarati risolti perche i test passano.

Risultato definitivo: **39 unit + 197 integration = 236/236 Passed**, due esecuzioni complete consecutive, **0 Failed, 0 Skipped**. I 211 test originali restano invariati; aggiunti 25 casi pertinenti. MySQL reale MAMP **5.7.24**, isolamento **REPEATABLE-READ**, database esclusivo `electronic_register_test`. Il bootstrap reale viene eseguito in Production e Development, con worker attivo e arresto ordinato.

## 2. Repository State

- Repository locale del progetto `xxdema10xx/ElectronicRegister`; branch `refactor-database-entities`; commit iniziale `970d5d7` (`fix: enforce exam closure rules and preserve retake grades`). Stato iniziale pulito. Nessun fetch, merge, commit o push effettuato nell'audit.
- Baseline prima di modificare il codice: build riuscita, 39/39 unit e 172/172 integration, zero Failed/Skipped. TRX sotto `TestResults/final-audit/baseline/`.
- Ricognizione iniziale completata e registrata prima delle modifiche produttive: [INITIAL_FINDINGS.md](TestResults/final-audit/INITIAL_FINDINGS.md). Le candidature iniziali sono state raffinate con le riproduzioni: per esempio l'hash password vuoto produceva 400, non il 500 ipotizzato inizialmente.
- Nessun file frontend, schema, entity scaffolding o test precedente modificato. Nessun aggiornamento NuGet, nuova dipendenza, indice o infrastruttura. Nessuna modifica a ConvertToGrade, recuperi, tentativi, lock o worker.

## 3. Architecture Review

| Livello | Dipendenze effettive e responsabilita | Valutazione |
| --- | --- | --- |
| Domain | DTO, enum, modelli, risultati, eccezioni, interfacce; nessun riferimento EF/ASP.NET | Separazione corretta; DTO di trasporto nel Domain e scelta esistente |
| Business | Domain e astrazioni DI; validazioni e calcoli; alcuni servizi interrogano repository | Coerente con struttura attuale; non interamente puro, nessun refactoring necessario |
| Application | Domain e riferimento Business; manager orchestrano autorizzazione, mapping, transazioni e audit | Riferimento diretto Business generalmente non necessario, ma non e un bug; authorization CRUD admin spesso affidata al controller |
| Infrastructure | Domain, EF Core/Pomelo/MySqlConnector, sicurezza JWT/BCrypt/Microsoft | Mapping espliciti Domain/entity; nessuna serializzazione diretta delle navigation EF nei controller |
| Api | Riferimenti ai quattro livelli per composition root; controller, middleware, auth, worker | Dipendenze appropriate al bootstrap. ServiceProvider ora valida scope e costruzione in entrambi gli ambienti |

DbContext, manager, servizi, repository e servizi di sicurezza sono scoped; ConfigurationManager OpenID e singleton; worker e singleton e usa IServiceScopeFactory, senza catturare DbContext scoped. Il vero Program costruisce e avvia la DI completa; la risoluzione di un manager scoped dal root fallisce come previsto nel test. Non sono emersi servizi mancanti o repository incompatibili con le interfacce nella build e nelle risoluzioni eseguite.

Non osservati async void applicativi, attese .Result/.Wait nei flussi backend, SQL costruito con input concatenato o catch che fingano il successo di scritture fallite. Le transazioni propagano gli errori dopo rollback. `MicrosoftLoginAsync` converte invece ogni eccezione di validazione/rete in mancato login, senza distinguere l'outage: rischio operativo aperto, non falso successo.

Repository CRUD legacy possono ritornare silenziosamente se la riga scompare tra lettura manager e update/delete; esiste quindi una lacuna sui race con cancellazioni amministrative, non una perdita dimostrata nel normale flusso. Alcune validazioni/limiti DB e nullable non sono uniformi. Restano warning nullable preesistenti in StudyPathRepository e vecchi test; nessun warning nuovo nei file dell'audit. I manager StudyArea/StudyPath non sono esposti da controller. Un file DI fuori dalla cartella del progetto Infrastructure non e una registrazione runtime della soluzione.

## 4. Security Findings

Per ogni finding sono separati fatto, condizioni e impatto. Le prove Microsoft utilizzano il validatore produttivo con metadati e chiave RSA locali controllati: non sono un attacco eseguito sul tenant Microsoft reale.

### SEC-01 - HIGH - issuer Microsoft non validato - vulnerabilita nel validatore, corretta

- File/metodo: `Infrastructure/Security/MicrosoftTokenValidator.ValidateAsync`.
- Prima: `ValidateIssuer=false`; un token firmato dalla chiave dei metadati, con audience valida ma issuer estraneo, veniva accettato. Riproduzione: `AuditMicrosoftTests.MicrosoftValidator_RequiresConfiguredTenantIssuer(false)` fallisce per assenza dell'eccezione; il caso issuer corretto passa.
- Condizioni dell'impatto end-to-end: disponibilita di un token Microsoft validamente firmato per l'audience accettata e configurazione Azure che permetta il rilascio dal tenant estraneo; il manager collega poi l'email a un utente locale. Non e stata dimostrata l'acquisizione di un simile token contro il tenant reale, ne impersonificazione con semplice JWT autofirmato.
- Correzione: issuer v2 vincolato al TenantId configurato, ValidateIssuer e ValidateIssuerSigningKey attivi; audience, firma e scadenza conservate. Entrambi i casi verificati. Nessun bypass della firma nei test. Deployment deve usare il GUID del tenant, non `common`/`organizations`; eventuale supporto v1/multitenant richiede una policy distinta e non viene aggiunto qui.

### SEC-02 - HIGH - letture Student fuori dalle classi del docente - accesso non autorizzato confermato, corretto

- File/metodi: StudentController GET/count/bylastname/id; StudentManager e StudentRepository.
- Qualunque docente autenticato poteva leggere nomi/ID degli studenti anche senza assegnazioni, perche non veniva passato il caller. Quattro richieste HTTP con `OtherTeacher` riproducevano risposta 200/dati invece di 404/zero.
- Correzione: caller esplicito, filtro SQL sulle ClassSubjects del docente per count/list/id/cognome; admin mantiene visibilita completa, student resta escluso dai ruoli. Nessun filtro tardivo su una lista globale in memoria.
- Prova: `Audit_TeacherWithoutAssignmentsCannotReadStudents`, quattro casi; controllo positivo docente assegnato/admin. Nessuna esposizione di voti o soluzioni attraverso questo DTO e stata dimostrata: l'impatto confermato e anagrafico.

### SEC-03 - MEDIUM - Subject/byteacher aggirava la visibilita della classe - accesso incoerente confermato, corretto

- File/metodo: SubjectManager.GetSubjectsByTeacherIdAsync.
- Uno studente senza classe riceveva 404 da GET Subject, ma 200 con materie usando il Guid di un docente. Prova HTTP `Audit_StudentTeacherSubjectsCannotBypassClassVisibility`, inizialmente fallita.
- Correzione: le assegnazioni del docente sono intersecate con la classe dello studente; senza classe non si restituiscono materie. Contratti Subject invariati per admin e docente proprietario. Impatto limitato a nomi/ID Subject, non Grade o Exam di altre classi.

### Controlli esaminati senza vulnerabilita confermata

- JWT locale: firma simmetrica, issuer, audience e lifetime richiesti. Il vero Program rifiuta token alterato, chiave errata, issuer/audience errati e token scaduto da oltre lo skew. Claim sub malformato viene rifiutato con 403 da CurrentCaller; role e enum sono controllati, TeacherId/StudentId necessari secondo ruolo. Skew lasciato al default della libreria; non e stato ridotto per far passare prove.
- HTTP reale: inventariate 73 azioni MVC; per tutte quelle protette il test bootstrap verifica 401 con bearer assente/non valido e 403 con ruolo non ammesso, anche quando il body non sarebbe valido. Le due login restano intenzionalmente anonime. Questa prova verifica i gate di ruolo, non tutta l'ownership per ogni Guid possibile.
- Grade letture: filtri teacher/student applicati in SQL, ID diretto restituisce 404 fuori ambito. Nuove prove HTTP su lettura, listing, paged con studentId manipolato, filters; bystudentid resta admin-only. Creazione/update controllano studente/classe/materia/docente. Users e scritture anagrafiche amministrative sono protette da ruoli; cambio password verifica caller e vecchia password.
- Exam e submission: ownership ClassSubject verificata dai manager, non dal solo Guid/DTO; studente accede solo alle proprie submission. Start non accetta StudentId dal client. Teacher non-owner e student/anonymous sono gia coperti dai test HTTP precedenti.
- Riservatezza: DTO Start non contiene IsCorrect; Result non contiene risposte/soluzioni; Submit/Result nascondono voto e punteggi non ufficiali allo studente. Review HTTP e teacher/admin-only; filtro SubmissionVisibility nasconde comunque IsCorrect, AwardedPoints, TeacherComment, SuggestedGrade e ReviewedByUserId ai caller student applicativi. AuditEvent e OverrideReason non sono esposti da endpoint student. Le navigation EF non vengono restituite.
- SQL raw: i tre punti FOR UPDATE usano parametri EF `{0}`. Nessun SQL dinamico alimentato da DTO trovato nei repository produttivi. Mass assignment limitato da DTO e mapping espliciti; le risposte client non impostano grading fields.
- ProblemDetails: nessuno stack trace o dettaglio DB in risposta; 500 usa messaggio generico. I log applicativi esaminati non scrivono password/JWT; exception logging del framework richiede comunque retention/accesso controllati. Non e una scansione del filesystem privato o di tutti i segreti storici Git.
- NuGet: comando richiesto eseguito con transitive su sette progetti; zero pacchetti segnalati dalle fonti disponibili. [Output](TestResults/final-audit/nuget-audit.txt). Non significa assenza universale di vulnerabilita; nessun aggiornamento massivo.

CORS/trasporto/rate limit rimangono rischi di configurazione separati, dettagliati nelle sezioni 11-12. Nessuna prova di SQL injection o lettura di Grade altrui dopo le correzioni nei percorsi verificati.

## 5. Data Integrity Findings

### INT-01 - HIGH - CRUD Grade aggirava la finalizzazione - bug confermato, corretto

- File/metodi: GradeManager.UpdateAsync/DeleteAsync, IGradeRepository/GradeRepository.
- Condizioni: voto ufficiale gia collegato a una submission. Il docente proprietario o admin poteva modificarlo senza OverrideReason/audit; admin poteva cancellarlo, facendo scattare FK SetNull e lasciando la submission finalizzata senza GradeId. Non e una sostituzione automatica del voto di recupero: e un percorso amministrativo alternativo.
- Tre prove HTTP `Audit_ExamGradeCannotBypassFinalization` riproducevano 204 invece di 409. Correzione minima: controllo SQL della relazione, rifiuto 409 nel CRUD generico. Nessun Grade/submission/audit viene modificato. Flusso Finalize e retry invariati; voto gia finalizzato non diventa liberamente riapribile.
- Nuovo controllo positivo `Audit_OrdinaryGradeRemainsEditableAndDeletable`: voti ordinari continuano a creare/aggiornare/cancellare, verificati da nuovi DbContext. **Cambio HTTP esplicito:** PUT/DELETE Grade collegato a esame ora 409, anche per admin.

### INT-02 - MEDIUM - scritture account/profilo non atomiche - bug confermato, corretto

- File/metodi: AuthManager.RegisterAsync/RegisterForAdminAsync e UserManager.UpdateAsync.
- Condizioni: errore dopo una SaveChanges intermedia; prima restavano utenti/profili o email modificati nonostante l'operazione fallita. Quattro casi con interceptor dopo SQL reale dimostrano persistenza parziale pre-fix, e rollback da nuovo contesto post-fix.
- Correzione: ITransactionManager esistente intorno all'intera operazione. Nessuna nuova infrastruttura. Profilo collegato scomparso durante update causa eccezione/rollback, evitando false dopo una scrittura gia compiuta. Non aggiunta una politica di retry per gare sull'email univoca.

### DATA-01 - MEDIUM - controllo dipendenza StudyArea sul Guid errato - bug confermato, corretto

- StudyAreaRepository.HasStudyPathsAsync confrontava BienniumStudyAreaId con StudyAreaId; sono chiavi diverse. Test contro relazione reale seed: false prima, true dopo.
- Ora la query segue `BienniumStudyArea.StudyAreaId`. Le FK gia impedivano cancellazioni referenzialmente invalide; nessuna perdita di dati e stata dimostrata. Nessun endpoint pubblico StudyArea aggiunto.

### DATA-02 - MEDIUM - Biennium update non validava le date - bug confermato, corretto

- BienniumManager.UpdateAsync consentiva end < start, pur vietato in Add. Test HTTP riproduceva 204 e persistenza invalida.
- Riutilizzata la validazione esistente, senza nuove regole: input invalido 400 e dati invariati verificati con nuovo DbContext.

### Regole preservate e limiti delle FK

ExamStudent ha PK (exam_id,student_id); ExamSubmission ha UNIQUE (exam_id,student_id) e GradeId univoco; SubmissionAnswer ha UNIQUE (submission_id,question_id). Grade/Student/ClassSubject e retake hanno FK. Le FK non esprimono da sole uguaglianza della classe, aciclicita dei recuperi o stato della finalizzazione: i manager completano queste garanzie.

Draft -> Published/Closed/Cancelled e le mutazioni Draft sono validate dal manager/service; nessun altro controller restituisce direttamente un repository che permetta di impostare Exam.Status. RetakeOfExamId e verificato percorrendo la catena, rifiutando cicli e ClassSubject incoerenti. Domande e assegnazioni sono esplicite. Originale e recupero conservano entrambi Grade e submission indipendenti; tentativo unico per Exam invariato.

ConvertToGrade, PassingPercent, SuggestedGrade, Passed ufficiale >= 6, override con motivazione e audit restano invariati. Result/review risolvono il Grade ufficiale dal repository; non e stato sostituito con SuggestedGrade. SubmissionSummary contiene GradeId e punteggi, non un voto numerico: il frontend deve usare Result/review per il valore ufficiale.

Delete: Draft non elimina originali referenziati da recuperi; submission impediscono cancellazioni Exam inappropriate. Alcune FK sono SetNull o Cascade per scelta schema (es. reviewer/audit actor, user-profili, Grade.Student); gli endpoint applicano guardie ma accessi diretti DB possono aggirare regole applicative. Nessuna migrazione proposta come necessaria a questi fix e nessuna modifica SQL/schema eseguita. Prima del deployment confrontare lo schema reale con quello validato dai test, incluse azioni FK e precisioni; il verifier test controlla nomi/colonne/indici/FK, non ogni comportamento operativo del server.

## 6. Concurrency & Worker Review

Close e Start iniziano una nuova transazione e prendono prima il lock Exam. In Close non esiste alcuna query consistente precedente al lock: la successiva lettura ClassSubject (docente) e la query Any InProgress stabiliscono lo snapshot dopo che lo Start precedente ha committato. Su REPEATABLE READ non resta uno snapshot antecedente al lock. Un nuovo Start attende poi commit/rollback di Close e verifica lo stato corrente. La guardia ignora la deadline: anche una InProgress scaduta impedisce Close.

Non aggiunti lock sulle submission e non cambiato l'ordine esistente. Submit e worker fanno uscire da InProgress; una lettura che vede ancora lavoro attivo puo rifiutare Close, mentre dopo commit il retry riesce. I test con barriere gia presenti coprono entrambi gli ordini Start/Close, Start/Cancel, Review/Finalize, Submit/autosave, worker multipli e fault dopo SQL. Grade/submission/audit condividono DbContext e transazione. Le prove sono state rieseguite su MySQL reale, non EF InMemory.

Autosave protegge revisione/stato/deadline con lock; revisione obsoleta non sovrascrive, consegna manuale tardiva rifiutata, automatica prematura rifiutata. Finalize ripetuto identico e idempotente, diverso confligge; i test verificano persistenza e audit singolo. Gli assert unit mock sul transaction manager, da soli, non provano rollback: la prova viene dai test integration con nuovi scope.

Worker: singleton corretto, scope separato per selezione e per elemento, batch 50 ordinato per deadline, timer 15 secondi, errori per elemento loggati senza fermare il batch. Errori batch vengono loggati e il ciclo continua. I test precedenti provano fallimento/recupero e due worker concorrenti; quello business-rules prova l'ultima consegna prima di Close.

Bootstrap nuovo: esegue l'entry point reale dell'assembly Api dentro il processo test, con applicationName esplicito equivalente all'eseguibile, URL `127.0.0.1:0`, configurazione isolata e DB autorizzato. DiagnosticListener osserva HostBuilt; **non sostituisce registrazioni, controller, JWT, middleware o worker**. In entrambi gli ambienti: DI validata, manager scoped vietato dal root, worker registrato/avviato, login BCrypt reale con account temporaneo, me autenticato, anonimo 401, matrice ruoli di tutte le azioni protette, JWT negativi, 404 ProblemDetails e Guid invalido 400, OpenAPI/Scalar solo Development, StopAsync e ritorno normale di Main con ApplicationStopped. Il monitor cerca errori significativi worker/startup, non pretende assenza di ogni log di richieste rifiutate.

Limiti: bootstrap non e un nuovo processo OS, non testa segnali del service manager, reverse proxy/TLS o il successivo tick del timer. Il worker non e disabilitato, ma i test rapidi non aspettano un ciclo periodico; batch/timer selection sono coperti separatamente dai test worker. Nessun crash/power loss, outage MySQL reale, prova di carico o dimostrazione universale di assenza deadlock. CancellationToken non arriva a tutte le query DB; elementi permanentemente falliti tra i primi 50 potrebbero affamare i successivi: rischio condizionale, non starvation riprodotta. Nessun retry generico introdotto.

## 7. Database Performance

Evidenza: [mysql-plans.txt](TestResults/final-audit/mysql-plans.txt), comandi SELECT/EXPLAIN/SHOW INDEX read-only sul solo database di test. Server 5.7.24/REPEATABLE-READ. Snapshot misurato piccolo: 12 Exam, 12 submission, 12 Student, 3 Grade. `table_rows` information_schema e stimato; i quattro conteggi riportati sono COUNT effettivi. Nessun dato di latenza sotto carico o cardinalita produttiva disponibile.

| Percorso/codice | Costo e piano osservato | Giudizio |
| --- | --- | --- |
| Worker: Status + DeadlineAt, ORDER BY deadline, LIMIT 50 | range su idx_exam_submission_expiration, circa una riga stimata | Indice gia adeguato al predicato; nessun indice nuovo |
| Close: EXISTS per exam_id e in_progress | optimizer sceglie uq_exam_submission_student sul piccolo seed; esiste anche idx_exam_submission_exam_status | Query SQL limitata; nessun problema misurato |
| GET Exam: GetSummariesAsync | proiezione DTO, Count assegnazioni via SQL, filtro docente; elenco senza paging | Non N+1 di struttura/risposte. Costo cresce con archivio; paging da valutare su volumi reali |
| GET Exam/{id} e lock Exam | Include Questions/Options e ExamStudents in query con potenziale prodotto domande-opzioni-assegnazioni | Rischio di moltiplicazione righe per esami grandi; non applicato AsSplitQuery perche cambierebbe le letture/lock validati |
| GET assigned | due query principali (Exam assegnati + submission studente), dizionario in memoria | Non N+1; tutte le submission storiche dello studente possono crescere, nessun paging |
| GET submissions | metadati senza risposte + studenti caricati in batch | Elenco non paginato. EXPLAIN equivalente usa indice exam_id e filesort started_at su una riga stimata |
| GET review | submission con risposte/opzioni, struttura esame, studente, eventuale Grade | Numero query limitato; ricerche FirstOrDefault in memoria possono costare Q*A su strutture molto grandi |
| Autosave SaveAnswersAsync | struttura in una query, poi una query QuestionOptions per ogni risposta chiusa | N+1 reale nel codice (non latenza misurata). Ottimizzazione batch possibile solo dopo budget/volume; non modificata nell'audit |
| Grade paged | filtri SQL, count + page, mapping dati correlati in batch | cap pageSize 100 gia presente. Piano studente usa idx_grade_student e filesort; nessun rallentamento dimostrato |
| Grade filters/statistics | filters carica tutti i voti visibili prima dei distinct; statistics aggrega in SQL | filters cresce con archivio; statistiche mensili raggruppano per mese senza anno, significato funzionale da chiarire prima di cambiarlo |
| Student | filtro docente SQL; piano su seed: scan 12 studenti + lookup ClassSubject indicizzato | Elenco senza paging; piano piccolo non dimostra necessita di nuovo indice |

EXPLAIN equivalente per Exam/ClassSubject + ordinamento mostra scan/filesort su 12 righe: non e prova di inefficienza produttiva. Le query EXPLAIN sono rappresentative dei predicati, non un dump completo di ogni SQL generato da EF. GradeMapper e UserManager caricano correlati in batch, non una query per ogni riga. Tracking in vari CRUD read e ServerVersion.AutoDetect nella factory DbContext aggiungono overhead potenziale; prima di intervenire misurare conteggio roundtrip/p95 su dataset e server di staging.

Nessun caching, Redis, nuovo indice o cambio delle strategie di lock introdotto. Un eventuale indice futuro deve seguire EXPLAIN/slow log sul volume reale, includere valutazione spazio/costo scritture e migrazione separata compatibile con dati esistenti.

## 8. API & Frontend Integration Readiness

Matrice completa di **73 operazioni** con metodo, route, ruolo, parametri/DTO, output e status principali: [API_ENDPOINT_MATRIX.md](TestResults/final-audit/API_ENDPOINT_MATRIX.md). Derivata dai controller finali e verificata staticamente; i gate di ruolo sono anche verificati HTTP dal bootstrap. Non significa che ciascun payload positivo sia coperto da un test end-to-end.

**Correzioni di contratto esplicite:** POST Student, Users e Biennium restituiscono ora Guid JSON e Location utilizzabile al posto di `true` e `/true`. I manager Add ritornano Guid. Il test legge l'ID, segue Location e verifica la riga da nuovo contesto. Biennium GET/all/active/inactive/id documentano ora BienniumDto anziche SubjectDto; il bootstrap Development ne verifica la presenza nello schema reale. I due alias storici Biennium/byname e byteacher continuano a restituire SubjectDto: contratto legacy strano ma invariato, da non interpretare come ricerca di bienni.

Convenzioni per il client:

- JSON camelCase. Guid come stringa; enum numerici: Exam Draft=0, Published=1, Closed=2, Cancelled=3; Submission InProgress=0, Graded=1, PendingReview=2, FailedAuto=3, Finalized=4; Question Single=0, Multiple=1, Open=2; SubmitReason Manual=0, TimeExpired=1, Violations=2. Ruoli JWT/UserDto sono stringhe minuscole.
- Null omessi (`WhenWritingNull`): assenza di Grade/Passed/TotalScore nello studente PendingReview e intenzionale, non zero. Collection DTO inizializzate; GET Exam/assigned/submissions/Grade restituisce 200 con collection vuota; diverse liste anagrafiche legacy restituiscono 404 se vuote. Gestire entrambe le convenzioni senza rinominarle arbitrariamente.
- DateOnly ISO `yyyy-MM-dd`; decimal come numero JSON, calcolo autorevole lato server. Evitare ricalcoli grading in JavaScript. DateTime usati come UTC dal codice, ma DATETIME non memorizza Kind; con connessione fornita il driver puo restituire Unspecified e JSON senza Z. E un rischio concreto del contratto temporale: il frontend deve trattare tali campi come UTC secondo convenzione e va verificato un roundtrip in timezone diversa prima del rilascio. Valutare `DateTimeKind=Utc` nella connessione con test di compatibilita; non modificato globalmente nell'audit ([documentazione MySqlConnector](https://mysqlconnector.net/connection-options/)).
- Start restituisce submissionId: `/api/Exam/{id}/answers/save` e `/submit` usano **quel submissionId**, non examId. Inviare lista completa delle risposte per autosave, ClientRevision crescente e gestire `accepted=false`; non assumere che ogni HTTP 200 significhi scrittura accettata.
- Un solo Start per Exam; recupero con proprio ExamId. Non iniziare di nuovo per riprendere: usare gli identificativi ottenuti e i percorsi gia disponibili; nessun nuovo endpoint resume introdotto.
- Start solo Published e finestra corrente; deadline calcolata con extra time secondo regola esistente. Deadline scaduta non autorizza Close: attendere worker. Submit tardivo e 409. Finalize identico idempotente, modificato 409; niente retry ciechi su mutazioni.
- 204 senza body; non chiamare response.json(). ProblemDetails sulle eccezioni gestite, ValidationProblemDetails sul binding; login errato restituisce invece stringa 401. Messaggi pubblici generici per non esporre dettagli interni; non usarli come codici macchina.
- OpenAPI/Scalar disponibili e provati solo Development. Non tutti gli ActionResult non generici descrivono un response schema completo, ne ogni status di errore e annotato: usare questa matrice/DTO sorgenti, non un client generato considerato automaticamente completo.

Checklist endpoint per integrazione:

| Famiglia | Stato | Condizioni/limiti |
| --- | --- | --- |
| Auth login/me, password | Pronta | bearer, conservazione sicura token, gestione 401/403; nessuna refresh/revoca immediata introdotta |
| Microsoft login | Pronta a integrazione configurata, non certificata sul tenant reale | tenant GUID/audience v2 reali e prova con identita Microsoft effettive prima del rilascio |
| Exam docente/admin | Pronta | ownership, stati, DTO, 409 Close attivo; niente eliminazione del Grade tramite CRUD |
| Exam studente/save/submit/result | Pronta | revisioni e UTC; Grade ufficiale solo quando disponibile; un tentativo |
| Grade letture/paged e voti ordinari | Pronta | filtri autorizzati SQL; per Grade d'esame PUT/DELETE ora 409 |
| Student | Pronta | docente limitato alle classi insegnate, admin CRUD; ID creazione corretto |
| Subject | Pronta | visibilita classe applicata anche a byteacher |
| Teacher | Pronta secondo policy attuale | directory nome/ID leggibile da tutti i tre ruoli; policy privacy da confermare, non trattata automaticamente come vulnerabilita |
| Users/Biennium admin | Pronta con note legacy | ID/Location corretti, atomicita User e date Biennium; contratti opzionali/validazioni admin da verificare nel form |
| StudyArea/StudyPath/class assignment | Nessuna nuova API | servizi interni esistenti non equivalgono a endpoint pubblici; nessuna funzionalita amministrativa mancante viene inventata dall'audit |

## 9. Test Coverage Assessment

Inventario baseline 211 casi, verificato nei TRX (il totale non e usato come prova universale):

| Classe | Casi | Cosa dimostra / limite |
| --- | ---: | --- |
| SubmissionGradingServiceTests | 18 | formule e limiti, puro unit |
| ExamSubmissionProcessorTests | 11 | orchestrazione con mock; non prova persistenza/rollback reale |
| ExamLifecycleServiceTests | 10 | regole pure, tutti gli stati del tentativo esistente |
| ConcurrencyIntegrationTests | 4 | MySQL concorrente; gate iniziale non forza ogni interleaving |
| ExamRegressionTests | 11 | review/finalize e replace/rollback reali |
| ExamSecurityTests | 2 | protezioni applicative mirate, non tutti i controller |
| ExamValidationTests | 9 | input/tampering e revisione |
| FinalGradeValidationTests | 5 | limiti voto finale e persistenza |
| FinalizeConcurrencyTests | 1 | finalizzazione concorrente, gate non esaustivo |
| GradeMappingTests | 4 | dati ufficiali/DTO e mapping |
| OpenScoreDoubleCountTests | 1 | regressione conteggio chiusi/aperti |
| Phase2B_AdditionalTests | 18 | override/retry/conflitti/limiti |
| Phase2B_ConversionAndOverrideTests | 2 | conversione e voto ufficiale |
| Phase2C_ApiTests | 14 | Kestrel/JWT/HTTP reale costruito per test |
| Phase2C_VisibilityTests | 10 | visibilita dati nei diversi stati/ruoli |
| Phase3_ConcurrencyTests | 7 | barriere dopo SQL e ordini di lock controllati |
| Phase3_TransactionRollbackTests | 10 | fault post-SQL e nuovo DbContext |
| Phase3_WorkerRecoveryTests | 4 | scope, worker multipli, recovery dopo errore |
| Phase4_ExamLifecycleTests (tre file partial baseline) | 70 | ciclo vita/recuperi/HTTP/atomicita/Close, include 14 business-rules |

Sovrapposizioni conversione/finalizzazione/visibility sono utili regressioni a livelli diversi e non sono state cancellate. I casi SkippableFact storici possono saltare se Docker/MySQL manca: qui nessuno e stato saltato. Le classi recenti falliscono esplicitamente se fixture indisponibile. Un exit code zero con skip non sarebbe accettabile.

Seed: un'unica collection MySQL serializza le classi; ID deterministici per scenario e reset dipendenti, piu Guid temporanei per Exam creati HTTP. Condivisione resta una dipendenza della suite, non test isolamento totale fra processi paralleli. Teardown e nuovi test ripristinano le proprie righe; prima/dopo usano nuovi scope, AsNoTracking o nuove query. L'audit non garantisce che due esecuzioni di dotnet test concorrenti sul medesimo database siano sicure.

Catch generici presenti in alcuni test concorrenti raccolgono eccezioni dei task, ma gli assert successivi verificano tipo/messaggio e stato persistito (es. Phase2B_AdditionalTests verifica InvalidOperationException e conflitto): non sono trattati automaticamente come falsi positivi. Le gare storiche Task.WhenAll sono meno forti delle barriere Fase 3; queste ultime restano la prova principale di interleaving.

Nuovi 25 casi: 21 nella partial AuditRegressionTests, 2 Microsoft, 2 bootstrap. Coprono i difetti riprodotti, Grade ordinario e accesso Grade filtrato. I due Microsoft test usano chiavi/metadati controllati: provano il validatore, non il provider esterno. I due bootstrap esercitano il vero Program; ogni ambiente verifica anche i gate delle 71 azioni protette. Nessuna nuova assertion dei test originali modificata. Il filtro regressioni seleziona anche tre test LifecycleAudit preesistenti: i 28 Passed del run mirato non significano 28 nuovi test.

Lacune residue: test di carico/archivio grande, proxy/certificati, Azure live, failover/backup restore, timeout DB e shutdown sotto query bloccata, combinazioni complete di payload malformati/admin CRUD, precisione/UTC in client diversi, policy invalidazione JWT dopo cambio account. Non dichiarate coperte dai 236 Passed.

## 10. Defects Fixed

| ID | Severity / categoria | Causa e file modificati | Riproduzione / verifica finale | Stato |
| --- | --- | --- | --- | --- |
| SEC-01 | HIGH, validazione sicurezza | MicrosoftTokenValidator: issuer disattivato -> tenant v2 vincolato | AuditMicrosoftTests, issuer straniero rifiutato e corretto accettato | Corretto e verificato |
| SEC-02 | HIGH, IDOR anagrafico | StudentController/Manager/Repository e interfacce: caller/filtro SQL mancanti | 4 casi teacher non-owner + owner/admin positivi | Corretto e verificato |
| SEC-03 | MEDIUM, visibilita incoerente | SubjectManager.GetSubjectsByTeacherId: mancava filtro classe studente | HTTP Subject contro byteacher, prima 200 fuori ambito | Corretto e verificato |
| INT-01 | HIGH, integrita/audit | GradeManager + IGradeRepository/GradeRepository: accesso alternativo Grade d'esame | 3 casi tampering + CRUD ordinario positivo, nuovi contesti | Corretto e verificato |
| INT-02 | MEDIUM, persistenza parziale | AuthManager e UserManager: SaveChanges separate -> transazione esistente | 2 register e 2 update con fault dopo SQL; rollback verificato | Corretto e verificato |
| API-01 | MEDIUM, contratto creazione | Student/User/Biennium manager e interfacce, BienniumController: bool come ID -> Guid | 3 POST, Location seguita e persistenza SQL | Corretto e verificato; cambio body esplicito |
| API-02 | MEDIUM, OpenAPI non coerente | BienniumController: tipi SubjectDto -> BienniumDto per vere letture biennio | bootstrap Development: schema assente prima, presente dopo | Corretto e verificato |
| AUTH-01 | MEDIUM, errore credenziali | BcryptPasswordHasher: hash vuoto inviato a BCrypt -> false | login HTTP pre-fix 400, post-fix 401 | Corretto per hash vuoto; hash corrotti non vuoti restano caso operativo da gestire |
| DATA-01 | MEDIUM, FK check | StudyAreaRepository: ID tabella ponte scambiato con StudyAreaId | query reale su relazione seed prima false poi true | Corretto e verificato |
| DATA-02 | MEDIUM, validazione | BienniumManager.UpdateAsync non riusava validazione date | HTTP prima 204, dopo 400 e persistenza invariata | Corretto e verificato |
| BOOT-01 | Copertura/configurazione, non vulnerabilita dimostrata | Program: ValidateScopes/ValidateOnBuild e binding configurabile | due ambienti, entry point reale e stop ordinato | Copertura aggiunta; default locale mantenuto |

Le prove pre-fix fallite sono in `TestResults/final-audit/reproduction/`, `subject-reproduction/`, `integrity-reproduction/`, `bootstrap-discovery/`. La prima prova bootstrap in-process richiedeva applicationName esplicito per non usare l'identita testhost: il 404 di discovery del test non e stato classificato come bug del processo produttivo. Nessun componente produttivo e stato rimosso per superare il test.

## 11. Remaining Risks

**CRITICAL aperti confermati: nessuno. HIGH applicativi aperti confermati: nessuno nei percorsi verificati.** Non equivale ad assenza universale di vulnerabilita. I punti seguenti restano aperti e hanno owner di configurazione/prodotto o necessitano di riproduzione/misura ulteriore.

| ID | Severity / categoria | File/condizioni ed evidenza | Impatto e raccomandazione | Stato |
| --- | --- | --- | --- | --- |
| CFG-01 | MEDIUM, rischio configurazione | Program usa AllowAnyOrigin/Method/Header anche Production; bearer esplicito, nessun AllowCredentials | Non dimostra CSRF/lettura senza token. Prima di esposizione pubblica adottare allowlist origini reali configurata per ambiente; oggi non esiste una chiave config che cambi la policy hardcoded | Aperto, sviluppo/integrazione funzionanti |
| CFG-02 | MEDIUM, configurazione auth | JwtOptions senza ValidateOnStart, chiave/issuer/audience/durata dipendono da segreti runtime; token stateless | Verificare chiave robusta e rotazione, skew richiesto, durata e invalidazione account; segnalazione fail-fast configurazioni mancanti raccomandata. Nessuna refresh/revoca nuova introdotta | Aperto |
| CFG-03 | MEDIUM, trasporto/deployment | HTTP 0.0.0.0 default, AllowedHosts esempio *, nessun forwarding proxy configurato in app | Sicuro solo dietro terminazione HTTPS attendibile e rete backend protetta, oppure binding HTTPS configurato. Verificare host/forwarded header e indirizzi proxy fidati; non assumere che esista gia il proxy | Non verificato in deployment |
| CFG-04 | MEDIUM, runtime DB / gap staging | SELECT VERSION ha restituito 5.7.24; test reali eseguiti su quella versione | MySQL 5.7 e in Sustaining Support dal 25/10/2023; non usare questo vecchio setup MAMP come attestazione del server produttivo. Ripetere suite e migrazione/restore su versione supportata scelta per staging; nessun upgrade automatico | Aperto, blocco del deployment se si intende riusare MAMP come produzione |
| SEC-04 | MEDIUM, protezione operativa | Nessun rate limiter applicativo su login/Microsoft; limiti payload non specifici al dominio | Rate limit al confine applicativo/proxy, budget dimensione/tempo e monitoraggio fallimenti prima dell'accesso pubblico; nessun attacco DoS eseguito e nessun nuovo limite funzionale introdotto | Aperto |
| SEC-05 | MEDIUM, identity/policy | MicrosoftLogin collega per preferred_username/email e catch(Exception) maschera outage; directory Teacher pubblica ai ruoli ammessi | Validare tenant live, policy identita immutabile/account lifecycle e privacy directory; non dichiarare impersonificazione dimostrata dopo issuer fix | Decisione/verifica esterna richiesta |
| REL-01 | MEDIUM, reliability condizionale | Worker cancellation non propagata alle query; batch prende sempre primi 50 | Misurare arresto sotto query lenta e gestione elementi permanentemente corrotti. Nessun retry indiscriminato, scheduler o broker raccomandato senza necessita | Aperto, starvation non riprodotta |
| PERF-01 | MEDIUM, costo concreto / prestazioni non misurate | N+1 opzioni autosave, liste senza paging, Include moltiplicativi, filters carica archivio | Piano/carico su cardinalita reali e budget query prima di ottimizzare; nessuna latenza grave dimostrata sul seed piccolo | Raccomandazione |
| API-03 | MEDIUM, gap input/errori/UTC | Validazioni legacy non uniformi, alcune InvalidOperationException non marcate, hash corrotti non vuoti e payload null annidati non coperti esaustivamente | Estendere regressioni prima di modificare status/contratti; definire UTC end-to-end e precisioni. Errori interni sono comunque redatti. Non viene dichiarata una suite completa di fuzzing | Aperto |
| DATA-03 | MEDIUM, gap admin/data model | Users.Add accetta Role/StudentId/TeacherId da admin; FK non validano coerenza semantica ruolo-profilo; gare unique email gestite dal DB | Validare form amministrativi e casi inconsistente/concorrenza; non considerato escalation da utente non admin, endpoint admin-only | Aperto |
| LOW-01 | LOW, miglioramento facoltativo | Riferimento Application->Business poco necessario, async wrapper, tracking read legacy, OpenAPI response metadata incompleti | Nessun refactoring estetico effettuato; annotazioni/ottimizzazioni solo se richieste dall'integrazione | Raccomandazione |

Fonte lifecycle DB: [MySQL Product Support EOL Announcements](https://www.mysql.com/support/eol-notice.html). Nessuna sostituzione del database di test e stata effettuata.

## 12. Production Checklist

Questa lista e un gate di deployment, non una nuova fase funzionale e non una dichiarazione che i punti siano gia configurati.

- [x] Build e baseline preservata, regressioni e bootstrap reale nei due ambienti, lock Esami invariati, prove ripetute su MySQL.
- [ ] Configurare `ConnectionStrings__DefaultConnection`, `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`, `Jwt__ExpiresInMinutes`; segreti fuori dal repository/log e credenziali separate per ambiente. `appsettings.example.json` e un esempio e non viene caricato automaticamente come appsettings reale.
- [ ] Per Microsoft: `AzureAd__TenantId` GUID effettivo, `AzureAd__Audience` dell'API v2; verificare token reale/issuer/audience e account provisioning. Instance/ClientId non sono usati dal validatore per autorizzare un token; non confonderli con Audience.
- [ ] Impostare `ASPNETCORE_ENVIRONMENT=Production`, `ASPNETCORE_URLS` appropriato e `AllowedHosts` reale. Program ora rispetta URL da configurazione/argomenti; fallback senza URL rimane `http://0.0.0.0:5000`. I profili launchSettings possono finalmente essere rispettati (5257/7225): nessuna porta fissa richiesta dai test.
- [ ] HTTPS end-to-end al confine pubblico; TLS terminato dall'app oppure proxy fidato. Backend HTTP ammesso solo su rete protetta; impedire accesso pubblico diretto al listener. Configurare limiti, forwarded headers/proxy fidati e host filtering nella topologia effettiva. Nessun dominio inventato aggiunto come origine reale.
- [ ] Restringere CORS alle origini effettive per Production con configurazione per ambiente e mantenere il locale funzionante; proteggere endpoint sensibili con rate limiting e limiti di trasporto. Lo schema bearer senza cookie evita credenziali ambientali, ma non sostituisce questi controlli.
- [ ] Verificare DB su versione supportata scelta dal progetto, storage/backup/point-in-time recovery e restore reale; utente app least privilege, TLS DB se remoto. Creazione/migrazione schema tramite processo controllato, non EnsureCreated a runtime produttivo. Nessuna migrazione eseguita nell'audit su DB utenti.
- [ ] Log centralizzati/accesso limitato, redazione credenziali e corpi sensibili, retention audit, alert su worker errori ripetuti, backlog scadute e latenza. Readiness/liveness e politica riavvio del processo da verificare in deployment; nessun servizio esterno introdotto.
- [ ] Prova di shutdown del service manager sotto carico, command timeout e indisponibilita DB, recovery worker, monitoraggio errori persistenti. Nessuna promessa di esattamente-una-volta oltre l'idempotenza DB testata.
- [ ] Smoke test React/React Native con bearer/401/403/409/204, UTC/DateOnly/decimal/null, upload payload realistici, percorsi submissionId, lifecycle account, origini browser reali. Nessuna modifica frontend richiesta dall'audit stesso.
- [ ] Conservare TRX e nuova baseline in CI con vincolo zero Skipped e DB test esclusivo; non lanciare suite concorrenti sullo stesso database seed.

## 13. Final Test Results

SDK .NET 10.0.401; target net10.0; EF Core/Pomelo 9.x come nel repository. Ultima build completa incrementale: zero errori/zero warning; ricompilazioni precedenti mostrano gli 11 warning nullable gia esistenti. Nessun pacchetto aggiornato. `git diff --check` senza errori di whitespace.

Comandi dalla cartella `Backend/ElectronicRegisterAPI`, con TEST_MYSQL_CONNECTION impostata alla connessione MAMP fornita dall'utente, credenziali non salvate nel report:

```powershell
dotnet build ElectronicRegisterAPI.slnx
dotnet list ElectronicRegisterAPI.slnx package --vulnerable --include-transitive
dotnet test ElectronicRegisterAPI.slnx --no-build --logger:trx --results-directory ./audit-evidence/definitive-1
dotnet test ElectronicRegisterAPI.slnx --no-build --logger:trx --results-directory ./audit-evidence/definitive-2
```

| Esecuzione | Unit Passed/Totale | Integration Passed/Totale | Failed | Skipped/notExecuted | Totale |
| --- | ---: | ---: | ---: | ---: | ---: |
| Baseline pre-fix | 39/39 | 172/172 | 0 | 0 | 211/211 |
| Definitiva 1 | 39/39 | 197/197 | 0 | 0 | 236/236 |
| Definitiva 2 consecutiva | 39/39 | 197/197 | 0 | 0 | 236/236 |

I contatori total/executed/passed/failed/notExecuted sono letti dai TRX, non inferiti dal solo exit code. I due run definitivi includono gli ultimi controlli di ruolo sui 73 endpoint, nessuna modifica al codice tra loro. Gli ulteriori run completi precedenti al rafforzamento bootstrap erano anch'essi verdi, ma non sostituiscono questi due.

Artefatti: `TestResults/final-audit/definitive-1/` e `definitive-2/`, con `unit.trx` e `integration.trx` ciascuno; baseline, riproduzioni fallite, regressioni mirate, piani MySQL, matrice API, inventario iniziale e scansione NuGet nella stessa cartella di evidenza. I 25 nuovi casi appartengono al progetto Integration; solo le prove Microsoft non usano la persistenza come oggetto del test. HTTP produttivo e worker non esclusi nel bootstrap. Nessuna esecuzione su database utenti/produzione.

## 14. Final Verdict

**READY FOR FRONTEND INTEGRATION**

Codice applicativo: i problemi bloccanti confermati sono corretti con regressioni e verifiche persistite; ciclo Esami, voti affiancati, tentativo unico e chiusura senza InProgress sono preservati.

Integrazione frontend: puo iniziare sui contratti documentati, tenendo espliciti UTC, enum numerici, null omessi, 204, revisioni e nuovi 409/Guid di creazione. Microsoft reale richiede la prova sul tenant configurato.

Deployment produttivo: non dichiarato pronto. Devono essere chiusi i gate di configurazione/sicurezza operativa e ripetute le verifiche sulla topologia e versione DB effettive. Un server MAMP 5.7.24 e due suite verdi non dimostrano production readiness.
