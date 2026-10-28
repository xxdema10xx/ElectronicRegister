# API calendario istituzionale

Prefisso `/api/institution/calendar`. InstitutionAccess corrente, tenant/membership attivi, ruoli esatti e SecurityGeneration corrente. Rollout InstitutionCalendar e SchoolApi; default chiuso, abilitazione con restart. Nessuna attivazione automatica in produzione. Le prove eseguite e lo stato di certificazione sono nel report Integration.

A = InstitutionAdmin, S = Staff, T = Teacher, U = Student. Manage calendario non concede IAM, piani Published, Exam o Grade. Ruoli combinati sono ammessi senza allargare le viste personali.

| Metodo | Route | Ruoli |
|---|---|---|
| GET / PUT | settings | A,S |
| GET | / (query calendario istituzionale) | A,S |
| GET | classes/{classId} | A,S |
| GET | teachers/me | T |
| GET | students/me | U |
| GET | months/{year}/{month}/classes/{classId} | A,S |
| POST | months/{year}/{month}/classes/{classId}/publish | A,S |
| GET | lessons/{id} e lessons/{id}/module | A,S,T,U autorizzati alla lezione |
| POST / PUT | lessons e lessons/{id} | A,S |
| POST | lessons/{id}/cancel | A,S |
| POST | lessons/{id}/reschedule | A,S |
| POST | lessons/{id}/substitute | A,S |
| POST | lessons/{id}/complete | A,S,T assegnato |
| POST | lessons/{id}/correct-delivered-time | A,S |
| GET | lessons/{id}/history | A,S |
| POST | imports/preview | A,S, staging privato |
| GET | imports/{id} | Solo attore proprietario A/S |
| POST | imports/{id}/confirm | Solo attore proprietario A/S |
| GET | hours?classId=GUID | A,S |

Le viste personali leggono solo Published e non contengono attori, motivi di sostituzione/cancellazione o audit. Teacher è risolto da InstitutionTeacherProfile e comprende tutte le classi del tenant; Student da InstitutionStudentProfile/Student.ClassId corrente. Filtri client restringono questi insiemi, non possono ampliarli. Nessun uso di User.TeacherId o della sola Enrollment. L'accesso modulo tramite lezione comprende anche un sostituto esplicitamente assegnato.

## Letture

Liste: from e to locali inclusivi, massimo 366 giorni di differenza; page=1, size=100 (1-100). Filtri opzionali classId, teacherId, moduleId, courseEditionId, publication (Draft/Published), status (Scheduled/Cancelled/Completed), includeCancelled=false. Ordine startUtc/id crescente. Un docente non deve fornire classId per vedere tutte le proprie lezioni.

Lesson include ID canonico, period/class/edition/version/module/subject/ClassSubject, docente originario e programmato, nomi classe/modulo/docente, date/ore locali, fuso, UTC, durata in minuti, stati, revision, minuti svolti, recupero e informazioni didattiche pubbliche. Period include revisione, stato dell'ultima pubblicazione, publisher, timestamp e Approval con deadline locale inclusiva, ritardo e overdue.

History usa page=1&size=20, massimo 100, ordine UTC/EventId. BeforeJson conserva il precedente record SQL, StateJson la nuova proiezione; entrambi sono dati amministrativi. Hours restituisce moduli con Planned/Scheduled/Delivered/residui/eccedenze/warning, UF, totali classe e progressione mensile. Nessuna lezione Draft/Cancelled aumenta il programmato pubblicato; Completed conta la durata programmata nel Scheduled e i minuti confermati nel Delivered.

## Comandi JSON rigorosi

Campi sconosciuti vietati anche nei DTO annidati. Tenant, attori, stato, revisione corrente, audit e timestamp tecnici non sono assegnabili dal client. EventId è un UUID non vuoto; Reason obbligatoria, massimo 500 senza controlli. ExpectedRevision Int64 esatta; `-1` soltanto per inizializzare settings o creare una lezione, le altre operazioni richiedono la revisione letta.

Settings PUT: eventId, expectedRevision, timeZoneId, approvalLeadDays, reason. Configurazione esplicita, nessun default Rome; cambio fuso vietato dopo creazione lezioni.

Creazione/PUT lezione:

```json
{"eventId":"GUID","expectedRevision":-1,"lesson":{"classId":"GUID","moduleId":"GUID","teacherId":"GUID","date":"2026-11-05","startsAt":"09:15:00","endsAt":"11:45:00","room":"Lab 1","description":"SQL","externalReference":null,"substitutionReason":null,"recoveryOfLessonId":null},"reason":"Programmazione amministrativa"}
```

Nessun SubjectId libero: deriva dal modulo. Modulo e ClassSubject devono appartenere al binding Published della classe. Creazione sempre Draft; PUT aggiorna Scheduled e conserva la pubblicazione attuale. Docente differente dall'assegnazione ordinaria/originaria richiede substitutionReason esplicita.

Publish/cancel: eventId, expectedRevision, reason. Publish richiede la revisione del periodo e approva tutte le Draft Scheduled di quella sola classe/mese. Periodo Published può ancora contenere nuove Draft private; il cambio data non pubblica nulla automaticamente.

Reschedule: eventId, expectedRevision, date, startsAt, endsAt, reason. Può spostare il periodo; Published viene modificata e approvata atomicamente. Substitute: eventId, expectedRevision, teacherId, reason, senza aggiornare ClassSubject. Complete/correct-delivered-time: eventId, expectedRevision, deliveredMinutes (1-1440), reason. Teacher può solo complete sulla propria Published Scheduled. Correct è amministrativo e mantiene lo storico. Completed non è riprogrammabile/annullabile dal normale workflow.

Receipt: eventId, resourceId, revision, lessonIds opzionali. EventId va riutilizzato con identico payload al retry; la ricevuta originale resta valida dopo altre revisioni. Payload o attore differente con lo stesso ID produce 409. Dopo mutazione rileggere il dettaglio corrente.

## Importazione

POST imports/preview multipart con esattamente un file `file`, campo `settings` JSON e `parsing` opzionale. Altri campi form non ammessi. Autorizzazione prima del parsing.

```json
{"eventId":"GUID","year":2026,"month":11,"classIds":["GUID"],"mode":"AppendDraft","mapping":null,"reason":"Preview calendario novembre"}
```

Mode stringa rigorosa AppendDraft/ReplaceDraftScope. ClassIds scope esatto e non ambiguo (1-100), niente ReplaceAll. Mapping opzionale array `{columnIndex:0,field:"Date"}`; destinazioni Ignore, Date, StartsAt, EndsAt, Class, Module, Teacher, Room, Description, ExternalReference. `parsing`: delimiter `;`/`,` ed encoding utf-8 per CSV, worksheetName per XLSX. Mapping diverso richiede nuova preview, non PUT sul batch esistente.

Preview include ID/revision/digest/SHA256/scadenza/stato, scope, Columns/ResolvedMapping, Added/Replaced/Preserved, TotalRows/ValidRows/InvalidRows/WarningRows/DuplicateRows, righe risolte, PreviewLessonId per collegare i conflitti nel file, errori e impatto ore per classe/modulo. Nessuna lezione definitiva viene creata. I nomi dei docenti non sono chiavi: GUID o reference Teacher Fase 3. Classi e moduli testuali devono risolversi senza ambiguità; usare GUID per disambiguare.

Confirm:

```json
{"eventId":"GUID","expectedRevision":0,"digest":"SHA256-DELLA-PREVIEW","acknowledgeWarnings":true,"reason":"Conferma calendario verificato"}
```

TTL 30 minuti; staging massimo 20 preview attive per attore. Ogni riferimento, revisione, conflitto, capability, generation e rollout viene ricontrollato sotto lock. Modifiche rilevanti richiedono nuova preview. Nessuna importazione parziale. Replace annulla soltanto Draft Scheduled dello scope dichiarato e preserva Published/altre classi. Warning monte ore non bloccano con riconoscimento esplicito; il budget non viene modificato.

CSV UTF-8/BOM, `;`/`,`, date yyyy-MM-dd e ore HH:mm. XLSX sicuro e stili numerici ammessi descritti nell'architettura. File massimo 10 MiB/2000 righe/16 colonne/512 caratteri cella, staging JSON 2 MiB. File ostili o strutturalmente invalidi sono rifiutati prima dello staging. Il TTL impedisce il commit scaduto; non elimina fisicamente batch e audit.

## Errori e client React Native

400 contratto/valori/testi/mapping/formato; 401 token assente/legacy/Selection; 403 ruolo/capability/profilo/generation/rollout; 404 assente/fuori tenant/non proprietario indistinguibili; 409 revisione/lifecycle/conflitti/preview stale o scaduta/riuso EventId/configurazione didattica mancante; 413 dimensione file. I problemi calendario espongono `code`, eventuali `details` solo autorizzati, senza SQL o dati di altri tenant. Conflitti SQL/deadlock/lock timeout hanno 409: rileggere o ripetere lo stesso intento con lo stesso EventId.

Il client deve distinguere stato periodo/stato lezione e minuti previsti/programmati/svolti; non certificare ore per una data passata. Conservare UUID di intento, digest e revisioni Int64 senza perdita JavaScript; mostrare scope e impatto prima della conferma, rileggere dopo receipt o 409. Non esistono notifiche, frontend, presenze, voti, scheduling automatico o sincronizzazioni esterne.
