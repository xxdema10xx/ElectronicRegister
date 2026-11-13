# Contratti frontend Esami - MT-3D / MT-3E

Queste API sono istituzionali e separate dalle route legacy. **Non costituiscono un cutover produttivo.** Sono esercitate nei test con entrambi i flag server-side `Multitenancy:SchoolApi:Enabled=true` e `Multitenancy:ExamApi:Enabled=true`. Assenza, falso o valore non valido di uno dei due mantiene chiuso il modulo. Il frontend non puo abilitare i flag. MT3E aggiunge il worker offline con flag separato `Multitenancy:InstitutionWorker:Enabled`, default false: il solo avvio delle API non abilita la consegna offline. [Release gate](../architecture/MULTITENANCY_RELEASE_GATE.md).

## Autenticazione e convenzioni

Ogni richiesta usa `Authorization: Bearer <InstitutionAccess>` emesso dopo login identita e selezione della membership. Un selection ticket, un JWT legacy `admin`, un token scaduto o per audience errata non sono accettati. InstitutionId deriva dal contesto verificato dal server: non inviarlo per autorizzare body, query, route o header.

Ruoli: **A** InstitutionAdmin, **T** Teacher con profilo e assegnazione corretti, **S** Student con profilo della membership selezionata. A non implica un profilo T/S. Una membership puo avere piu ruoli; le operazioni personali richiedono comunque il profilo Student e le letture di gestione restano legate alle assegnazioni Teacher. Ruoli in A/B non si sommano.

JSON camelCase, Guid stringhe, decimal numeri. Gli enum sono attualmente **numerici**, secondo OpenAPI:

| Campo | Valori |
| --- | --- |
| Exam.Status | 0 Draft, 1 Published, 2 Closed, 3 Cancelled |
| Submission.Status | 0 InProgress, 1 Graded, 2 PendingReview, 3 FailedAuto, 4 Finalized |
| Question.Type | 0 Single, 1 Multiple, 2 Open |

I campi nullable possono essere omessi nella risposta: un campo assente equivale a null, **non** a false/zero. Date e deadline sono UTC; usare ISO 8601 con `Z` negli input, visualizzando localmente. Precisione persistita DATETIME(6), microsecondi. Il tempo del server prevale sul timer del browser.

401: autenticazione assente/non valida, rinnovare login/selezione se necessario; nessun refresh-token endpoint introdotto. 403: ruolo, membership/istituto revocato, profilo mancante o rollout chiuso. 404: ID inesistente, fuori tenant o non visibile, senza distinguere i casi. 400: body/compilazione non validi. 409: conflitto di stato, revision sul submit, dipendenza o idempotency key incoerente. 413: body oltre limite. Non mostrare messaggi SQL; usare i ProblemDetails pubblici.

## Route

Tutte le route sotto iniziano con `/api/institution`.

| Metodo e route | Ruoli | Input | Output riuscito |
| --- | --- | --- | --- |
| GET /class-subjects | A,T | nessuno | 200 ClassSubjectChoiceDto[] |
| GET /students?classId=Guid | A,T | classId facoltativo | 200 StudentDto[] |
| GET /exams | A,T | nessuno | 200 ExamSummaryDto[] |
| GET /exams/{id} | A,T | ExamId | 200 ExamDetailDto |
| GET /exams/{id}/eligible-students | A,T | ExamId | 200 StudentDto[] |
| POST /exams | A,T | CreateExamDto | 201 Guid + Location istituzionale |
| PUT /exams/{id} | A,T | UpdateExamDto | 204 |
| PUT /exams/{id}/students | A,T | UpdateExamStudentsDto | 204 |
| POST /exams/{id}/publish | A,T | nessun body | 204 |
| POST /exams/{id}/close | A,T | nessun body | 204 |
| POST /exams/{id}/cancel | A,T | nessun body | 204 |
| DELETE /exams/{id} | A,T | nessuno | 204, solo Draft secondo regole esistenti |
| GET /exams/assigned | S | nessuno | 200 AssignedExamDto[] |
| POST /exams/{id}/start | S | nessun body | 200 StartExamDto |
| GET /exams/{id}/submissions?status=N | A,T | SubmissionStatus facoltativo | 200 SubmissionSummaryDto[] |
| GET /submissions/{id}/session | S | SubmissionId | 200 SubmissionSessionDto |
| PUT /submissions/{id}/answers | S | SaveExamAnswersDto | 200 SaveAnswersResponseDto |
| POST /submissions/{id}/submit | S | SubmitExamDto | 200 SubmissionResultDto |
| POST /submissions/{id}/submit-expired | S | **nessun body** | 200 SubmissionResultDto |
| POST /submissions/{id}/violations | S | InstitutionViolationDto | 200 ViolationReceiptDto |
| GET /submissions/{id}/violations | A,T | nessuno | 200 ViolationEventDto[] |
| GET /submissions/{id}/review | A,T | nessuno | 200 SubmissionReviewDto |
| PUT /submissions/{id}/review | A,T | ReviewOpenAnswersDto | 204 |
| POST /submissions/{id}/finalize | A,T | FinalizeSubmissionDto | 204 |
| GET /submissions/{id}/result | A,T,S | nessuno | 200 SubmissionResultDto |
| GET /grades | A,T,S | nessuno | 200 GradeDto[] |
| GET /grades/{id} | A,T,S | GradeId | 200 GradeDto |
| GET /grades/count | A,T,S | nessuno | 200 int |
| GET /grades/filters | A,T,S | nessuno | 200 GradeFiltersDto |
| GET /grades/statistics | A,T,S | nessuno | 200 GradeStatisticsDto; 404 senza voti |
| GET /grades/paged | A,T,S | pageNumber, pageSize, subjectId?, studentId?, date? | 200 GradePageDto |
| POST /grades | A,T | CreateGradeDto | 201 Guid + Location |
| PUT /grades/{id} | A,T | UpdateGradeDto | 204 |
| DELETE /grades/{id} | A | nessuno | 204 |

Le collezioni valide senza risultati restituiscono 200 e array vuoto. I parametri della paginazione restano quelli esistenti: pageNumber minimo 1; pageSize fuori 1..100 normalizzato a 20. Filtri date in formato `yyyy-MM-dd`; subjectId/studentId filtrano ulteriormente la visibilita gia autorizzata. Le vecchie route Grade/bydate e bysubject si esprimono nel nuovo flusso tramite i filtri paginati, senza riaprire le route legacy.

## Dropdown ClassSubject e selezione studenti

`ClassSubjectChoiceDto` contiene solo:

```json
{
  "classSubjectId": "guid",
  "classId": "guid",
  "className": "Classe",
  "subjectId": "guid",
  "subjectName": "Materia",
  "teacherId": "guid",
  "teacherFirstName": "Nome",
  "teacherLastName": "Cognome"
}
```

Usare ClassSubjectId come valore della selezione. Il testo visualizzato puo combinare ClassName/SubjectName/Teacher. Non sostituire il Guid con nomi. A vede il proprio istituto; T solo le assegnazioni del proprio profilo; S non puo usare questo menu amministrativo.

Scelta una classe-materia, caricare `/students?classId=<classId>`. Senza classId resta il comportamento MT3C; con filtro vengono verificati tenant e permessi sulla classe **in SQL**. Classe esterna/non autorizzata/inesistente -> 404; Student directory -> 403. Non filtrare soltanto nel browser.

Per un Exam esistente usare `/exams/{id}/eligible-students`: il backend deriva la classe da Exam -> ClassSubject. Restituisce tutti gli studenti attualmente appartenenti a quella classe, compresi quelli gia assegnati; confrontare con `ExamDetailDto.AssignedStudents`. Nessun requisito di insufficienza per i recuperi.

`UpdateExamStudentsDto`: `{ "students": [{ "studentId": "guid", "extraTimeMinutes": 0 }] }`. E una sostituzione dell'insieme, non un append. Duplicati, extra time negativo, classe/tenant errati e stati non modificabili vengono rifiutati lato server.

## Creazione e ciclo Exam

CreateExamDto: classSubjectId, retakeOfExamId nullable, name, description nullable, availableFrom, availableUntil, durationMinutes, passingPercent, assignedStudents, questions. QuestionInputDto: position, text, type, points, required, options; ciascuna opzione amministrativa ha position, text, isCorrect. UpdateExamDto contiene gli stessi campi di struttura/schedule senza assignedStudents.

I DTO amministrativi con soluzioni sono riservati ad A/T autorizzati. Lo studente riceve soltanto ExamForStudentDto attraverso Start/Session. Le risposte corrette non appartengono al contratto sessione.

Pubblicazione, cancellazione, modifica e cancellazione Draft conservano le regole esistenti. Close richiede Published e **zero submission InProgress**, anche se scadute. Una deadline individuale non chiude l'Exam. Non mostrare un Close riuscito prima della risposta 204.

## AssignedExam, Start e ripresa

AssignedExamDto contiene id (ExamId), classSubjectId, name, description, availableFrom/until, durationMinutes, extraTimeMinutes, status, submissionStatus nullable, **submissionId nullable**, startedAt/deadlineAt/submittedAt nullable, canStart, isRetake.

SubmissionId e null/assente se la prova non e iniziata; dopo Start e l'ID realmente persistito per quello studente/istituto, mantenuto dopo submit e finalize. La lettura non genera ID o tentativi.

Flusso consigliato:

1. Login identita e selezione membership; GET `/exams/assigned`.
2. Se submissionId assente e CanStart vero, POST `/exams/{examId}/start`.
3. StartExamDto restituisce submissionId, startedAt, deadlineAt, exam (struttura studente).
4. Se submissionId gia presente, GET `/submissions/{submissionId}/session`, anche dopo login su un altro dispositivo.
5. Per InProgress ricostruire timer e risposte dalla sessione; per stato consegnato passare al flusso risultato.

**Un solo tentativo per Exam**: richiamare Start dopo l'avvio restituisce 409. Resume non chiama Start e non estende la deadline.

SubmissionSessionDto: submissionId, examId, status, startedAt, deadlineAt, submittedAt nullable, currentRevision, exam, answers. `answers` contiene solo questionId, openText, selectedOptionIds. `exam` contiene ID/nome/descrizione/durata/fine disponibilita/peso totale e domande/opzioni nella posizione persistita. I pesi teorici delle domande sono parte della struttura, non correzioni. Non contiene isCorrect, awardedPoints, teacherComment o suggestedGrade.

Nessuno shuffle viene eseguito nei GET. L'ordine e deterministico per position/ID; non e ancora un ordine individuale randomizzato. Un futuro ordine individuale persistito dovra sostituire questo ordinamento mantenendo la continuita del resume.

## Autosave e revision

SaveExamAnswersDto e SubmitExamDto hanno `clientRevision` (intero long monotono) e `answers` (snapshot completo):

```json
{
  "clientRevision": 12,
  "answers": [
    { "questionId": "guid", "selectedOptionIds": ["guid"] },
    { "questionId": "guid", "openText": "Risposta aperta" }
  ]
}
```

Il salvataggio sostituisce le risposte della submission. Campi di grading del client non sono accettati. Domande/opzioni esterne, duplicati e struttura Single/Open invalida sono rifiutati. Autosave puo conservare risposte parziali/vuote.

SaveAnswersResponseDto: accepted, currentRevision, savedAt nullable. Una revision vecchia/uguale o un save scaduto puo restituire **200 con accepted=false**: non considerarlo un salvataggio riuscito. Ricaricare Session e confrontare/riconciliare le modifiche; inviare poi una revision nuova, senza rispedire ciecamente lo snapshot obsoleto. Due device sono serializzati sulla singola submission; uno stale non sovrascrive una revision nuova. Dopo submit un nuovo save restituisce 409.

localStorage puo essere una cache, non la fonte autorevole per submissionId, revision, risposte o deadline.

## Submit manuale e scadenza

Prima della deadline, POST submit puo includere l'ultimo snapshot con revision nuova. Required significa compilazione effettiva: Open con testo non whitespace; Single/Multiple con almeno un'opzione selezionata valida. Non significa risposta corretta o selezione di tutte le opzioni corrette. Un Required non compilato produce 400; la transazione rollbacka snapshot/revision/metadati del submit, preservando l'ultimo autosave riuscito.

Dopo la deadline, il manuale non accetta nuove risposte: 409 se la submission e ancora InProgress. Se gia consegnata, un retry restituisce il risultato persistito senza riapplicare il body. Nessun Grade duplicato.

`POST /submissions/{id}/submit-expired` e l'azione **dello studente proprietario autenticato**, senza body, disponibile a deadline raggiunta/scaduta. Prima della deadline su InProgress -> 409. Con body -> 400. Usa solo le risposte gia persistite, ignora il requisito di compilazione Required, assegna zero alle chiuse vuote e conserva PendingReview per esami con aperte. E idempotente e non cambia Exam.Status o deadline. SubmittedAt e l'istante di elaborazione sul server.

Questo permette il timer del client senza inviare risposte tardive. **La consegna quando il browser e offline e gestita dal server soltanto dopo l'effettiva abilitazione e validazione del worker istituzionale MT3E.** Il flag InstitutionWorker e indipendente da SchoolApi/ExamApi, default false. Il servizio non usa JWT, completa solo submission gia avviate e scadute, con risposte salvate, ownership verificata e transazione per item. Poll ogni 15 secondi, batch massimo 50: SubmittedAt puo essere successivo alla deadline, anche dopo un'interruzione del servizio. Non mostrare una consegna riuscita soltanto perche il timer browser e arrivato a zero: ricaricare sessione/risultato quando l'accesso e autorizzato.

Una Institution sospesa o una membership revocata blocca le nuove richieste interattive; il worker puo comunque conservare e consegnare la prova gia avviata senza riattivare accessi. Non inviare credenziali/token a un endpoint tecnico: non esiste una API worker. Errori prima del commit lasciano InProgress per un successivo sweep; nessun nuovo tentativo, estensione deadline o perdita di risposte. Con flag disabilitato la consegna offline istituzionale non e operativa, anche se le API rispondono correttamente. Mantenere il rollout operativo chiuso fino alla checklist di rilascio.

## Risultato: Status e Passed

SubmissionResultDto: submissionId, examId, status, submittedAt, submitReason, totalScore/maxScore nullable, grade nullable, passed nullable.

```text
passed === true  -> Superato
passed === false -> Non superato
altrimenti       -> Esito non ancora disponibile
```

Non aggiungere SubmissionStatus.Failed. FailedAuto e uno stato operativo preesistente e non una conversione del voto insufficiente. Il servizio reale attuale usa Graded per chiuse e PendingReview per aperte; un eventuale FailedAuto senza voto ufficiale non autorizza un esito didattico.

Quando il Grade e ufficiale, Passed segue **Grade.Value >= 6**, compresi override motivati: 5 false, 6/9 true. Prima del voto ufficiale, lo studente non riceve punteggi/esito provvisori. Un esame solo chiuso corretto automaticamente puo avere Graded e Passed=false: non e un errore di consegna.

## Review, finalize, voti e recuperi

Review amministrativa: SubmissionReviewDto con stato, studente, risposte, punteggi, suggerimento, eventuale Grade e violationCount. Riservata ad A/T autorizzati. `ReviewOpenAnswersDto.answers` contiene answerId, awardedPoints, comment opzionale. FinalizeSubmissionDto: finalGrade nullable, overrideReason nullable; se finalGrade differisce da SuggestedGrade serve motivazione non vuota (max 1000 caratteri). Finalize e atomico/idempotente con Grade unico; retry discordante -> 409.

GET `/submissions/{id}/violations` consente al docente autorizzato di consultare i segnali del browser. Non considerarli prova certa di scorrettezza.

CRUD Grade generico: CreateGradeDto studentId, subjectId, value (1..10), date; UpdateGradeDto subjectId, value, date. Classe e assegnazione vengono verificate dal backend. Non puo modificare/cancellare Grade associati a submission ufficiali: 409, usare review/finalize. Teacher vede solo assegnazioni, Student propri voti. Nelle letture Grade, Teacher+Student puo leggere sia classi insegnate sia voti personali, entro la stessa membership.

Un recupero e un nuovo Exam con retakeOfExamId, stessa ClassSubject e vincoli di stato esistenti. Il Grade del recupero **affianca** quello originale: nessuna sostituzione, modifica o media automatica, nessuna ammissione subordinata all'insufficienza introdotta.

## Violazioni idempotenti

InstitutionViolationDto: eventId Guid non vuoto, type in `tab_hidden`, `window_blur`, `fullscreen_exit`, metadata stringa JSON facoltativa (oggetto, UTF-8 <=1024 byte, profondita <=4). Il body HTTP e limitato a 4096 byte. Generare EventId una volta e riutilizzarlo soltanto per retry dello stesso evento.

Il server assegna OccurredAt UTC, salva l'evento e incrementa ViolationCount nella stessa transazione/lock della submission. ViolationReceiptDto contiene eventId, violationCount e duplicate. Retry identico -> 200 duplicate=true senza incremento; stesso EventId con contenuto/submission diversi -> 409. Un EventId non puo essere riutilizzato neanche in un altro istituto (PK globale, errore generico senza dati dell'evento esterno).

Nuovi eventi soltanto prima della deadline e in InProgress. Limiti: massimo 5 nuovi eventi ogni 10 secondi per submission, massimo 128 totali; superamento -> 409 e nessun incremento. Retry gia persistiti non consumano il limite. Non inviare dati personali sensibili nei metadata; il server li tratta come contenuto non attendibile.

**Nessuna penalizzazione, insufficienza, invalidazione o consegna automatica per violazioni e stata introdotta.** SubmitReason.Violations non viene usato automaticamente. Il browser puo produrre eventi involontari o essere manipolato; le eventuali sanzioni richiedono una policy funzionale separata.

## Limiti dopo MT-3E

Scansione/dispatch tecnico per item, audit atomico e recovery applicativo sono testati su MariaDB locale, incluso timer reale offline. Non equivalgono a un deployment produttivo: restano restore certificato, monitoraggio/alerting e sicurezza dell'ambiente, governance e API amministrative di membership/account ancora chiuse. Nessuna API globale audit, CourseEdition, StudentEnrollment, randomizzazione o modifica React introdotta. Le API legacy rimangono separate e rifiutano i token istituzionali. Il frontend deve integrare soltanto i contratti istituzionali qui descritti; i flag e la checklist di rilascio sono responsabilita server/operativa.
