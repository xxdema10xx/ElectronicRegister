# Convenzioni trasversali

Regole valide per tutti gli endpoint, salvo eccezioni indicate in [API.md](API.md).

---

## 1. Formato JSON

| Regola | Dettaglio | Fonte |
|---|---|---|
| Nomi proprietà | **camelCase** in uscita. In ingresso l'abbinamento non distingue maiuscole/minuscole (default ASP.NET), ma usare sempre camelCase. | default `System.Text.Json` web |
| `null` in uscita | **I campi con valore `null` sono omessi** dalla risposta. Nel frontend trattare "assente" e `null` come equivalenti (`?.` / `??`). | `Api/DependencyInjection/ServiceCollectionExtensions.cs` riga 13 |
| Campi sconosciuti in ingresso | Molti DTO hanno `JsonUnmappedMemberHandling.Disallow`: un campo non previsto produce **400**. Per gli utenti Staff vale per tutti i POST/PUT di studenti, docenti, esami e voti (vedi [AUTH §4.3](AUTH.md#43-regola-speciale-per-staff-campi-sconosciuti)). | DTO in `Domain/DTOs` |
| Campi obbligatori | `[JsonRequired]` / `[Required]`: se mancano → **400**. | DTO |
| Body di creazione | Molte `POST` di creazione rispondono **201** con body = **solo il GUID** creato (stringa JSON, es. `"3fa85f64-5717-4562-b3fc-2c963f66afa6"`) e header `Location`. | controller (`CreatedAtAction`) |
| Content-Type | `application/json` per tutti i body, tranne gli upload (`multipart/form-data`) e il download dei template (`text/csv`). | |

### 1.1 Enum: attenzione, due formati diversi

| Enum | Formato JSON | Valori |
|---|---|---|
| `ExamStatus`, `SubmissionStatus`, `QuestionType`, `SubmitReason`, `CalendarConflictKind` | **numero intero** (indice) | vedi [MODELS.md §2](MODELS.md#2-enum-e-costanti) |
| `EmailCategory`, `EmailNameCase`, `EmailAccentHandling`, `EmailPunctuationHandling`, `EmailCollisionStrategy`, `ImportCategory`, `ImportEmailMode`, `ImportField`, `ImportRowStatus`, `CalendarImportField`, `CalendarImportMode`, `FormativeVersionStatus` | **stringa**, case-sensitive e senza alias numerici | |
| `MembershipMutationAction`, `InstitutionRole` (nel body delle mutazioni membership) | **stringa** case-sensitive | |
| Stati "testuali" (`status` delle edizioni corso, iscrizioni, lezioni, membership…) | **stringhe libere** con valori fissi documentati per campo | |

Gli enum degli esami non hanno un convertitore a stringa: il backend li emette come numeri. Il frontend esistente li converte con `fromEnum` (`Frontend/ElectronicRegisterReact/src/api/examContract.js`). In ingresso, ad esempio per `QuestionType`, inviare il **numero**. Vedi [OPEN_QUESTIONS Q-02](OPEN_QUESTIONS.md#q-02).

### 1.2 Date e ore

| Tipo C# | Formato JSON | Esempio | Note |
|---|---|---|---|
| `DateOnly` | `"YYYY-MM-DD"` | `"2026-10-31"` | date senza ora (voti, iscrizioni, lezioni, bienni). Anno ≥ 1000 dove validato. |
| `TimeOnly` | `"HH:mm:ss"` | `"08:30:00"` | orari lezione; devono essere **al minuto** (secondi = 0), altrimenti `calendar.minute-precision-required`. |
| `DateTime` | ISO 8601 | `"2026-11-03T08:00:00Z"` | **inviare sempre in UTC con `Z`**: gli esami confrontano `availableFrom`/`availableUntil` con `DateTime.UtcNow`. I campi che finiscono in `Utc` sono UTC. |
| `long` revision/generation | numero | `3` | contatori di concorrenza |

Il fuso orario del calendario è configurato per istituto (`CalendarSettings.timeZoneId`, es. `Europe/Rome`). Le lezioni hanno ora **locale** (`date`, `startsAt`, `endsAt`) e i corrispondenti **UTC** (`startUtc`, `endUtc`).

### 1.3 ID, numeri, valute

- **Tutti gli ID sono GUID** (stringhe `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`). Il GUID vuoto `00000000-0000-0000-0000-000000000000` è quasi sempre rifiutato con 400.
- I vincoli di route `{id:guid}` fanno sì che un ID non-GUID nel percorso dia **404**, non 400.
- Numeri decimali (`decimal`): voti, punti, percentuali. Arrivano come numeri JSON (es. `7.5`).
- **Nessun campo valuta** nel backend.

### 1.4 `eventId`: idempotenza dei comandi

Molti comandi (calendario, piani formativi, email, import, membership, inviti, iscrizioni, trasferimenti, violazioni esame) richiedono un campo **`eventId` (GUID generato dal client)**.

- Generarlo **una volta per azione dell'utente** (es. `crypto.randomUUID()`) e **riusarlo nei retry** della stessa azione.
- Riutilizzare lo stesso `eventId` con **lo stesso payload** restituisce lo stesso risultato (replay idempotente). Ad esempio, per le violazioni la risposta ha `duplicate: true`.
- Riutilizzarlo con **payload diverso** → **409** (es. `"EventId già utilizzato."`, `calendar.event-payload-conflict`, `EventIdConflict`).

---

## 2. Formato degli errori

Gli errori sono in formato **RFC 7807 ProblemDetails** (`application/problem+json` o `application/json`):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Richiesta non valida",
  "status": 400,
  "detail": "Richiesta non valida",
  "code": "FileTooLarge"
}
```

- `title` è sempre presente. `detail` è presente per gli errori gestiti dall'handler globale. `type` e `traceId` possono esserci o mancare a seconda di chi genera l'errore.
- Campi extra opzionali: `code` (calendario, import), `details` (conflitti calendario), `hourSummary` (piani formativi).
- **Il messaggio specifico dell'eccezione non viene restituito** dall'handler globale: per la maggior parte delle regole di business il client riceve solo il `title` generico. Fanno eccezione i trasferimenti di classe, dove `detail` contiene il messaggio.
- Ogni risposta ha l'header **`X-Correlation-ID`** da riportare nelle segnalazioni di bug.

### 2.1 Errori di validazione automatica (model binding)

Quando il JSON è malformato, manca un campo `[Required]`/`[JsonRequired]`, c'è un campo sconosciuto o un tipo è sbagliato, ASP.NET risponde **400** con `ValidationProblemDetails`:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "$.email": ["The JSON value could not be converted to System.String. ..."] },
  "traceId": "00-…"
}
```

I messaggi sono quelli **standard di ASP.NET in inglese**: non mostrarli all'utente così come sono.

### 2.2 Mappa status → significato (handler globale `Api/ExceptionHandling/GlobalExceptionHandler.cs`)

| Status | `title` | Quando |
|---|---|---|
| 400 | `Richiesta non valida` | parametri/valori non validi (lunghezze, intervalli, GUID vuoti, filtri non ammessi, paginazione fuori range) |
| 400 | `Importazione non valida` + `code` | errori di parsing import (vedi §6.3) |
| 401 | `Non autenticato` | token assente/scaduto/non valido |
| 401/403 | `Non autorizzato` | permesso o proprietà del dato insufficiente (401 se non autenticato) |
| 403 | `Non autorizzato` | ruolo non ammesso, modulo disattivato, profilo non collegato |
| 404 | `La risorsa non esiste` | risorsa inesistente **o non visibile** nell'istituto corrente |
| 404 | *(body vuoto o ProblemDetails standard)* | `NotFound()` esplicito di alcuni GET/PUT |
| 409 | `Operazione non consentita` | violazione di una regola di business (stato errato, duplicati, revisione obsoleta, ecc.) |
| 409 | `Conflitto sulla membership istituzionale` | concorrenza o regola su membership/inviti (es. rimozione dell'ultimo admin) |
| 409 | `Conflitto sulla struttura formativa` | edizioni corso, sedi, iscrizioni: vincoli DB o versione obsoleta |
| 409 | `Conflitto sulla preview di importazione` + `code` | conflitti di import (vedi §6.3) |
| 409 | `Trasferimento non consentito` + `detail` | trasferimento classe |
| 409 | `Piano formativo non pubblicabile` + `hourSummary` | pubblicazione piano formativo incompleto |
| 409 | `<codice calendario>` + `code` (+ `details`) | conflitti calendario (vedi §6.1) |
| 413 | `Richiesta troppo grande` / `Importazione non valida` | body oltre il limite |
| 415 | `Importazione non valida` | formato/codifica file non supportati |
| 422 | `Importazione non valida` (`code: RowsInvalid`) | conferma import con righe non valide |
| 428 | `If-Match richiesto` | PUT sul catalogo senza header `If-Match` |
| 429 | `Request limit exceeded` + header `Retry-After: 60` | rate limit (vedi §5) |
| 500 | `Errore interno del server`, `detail: "Si è verificato un errore interno."` | errore inatteso |
| 503 | *(nessun body)* | solo `/health/ready` |

### 2.3 Come gestirli nel frontend

| Status | Comportamento consigliato |
|---|---|
| 400 | mostrare "Dati non validi" vicino al form, e validare lato client con le regole di [API.md](API.md) per evitarlo |
| 401 | sessione scaduta → riselezione istituto (se il ticket è valido) o login |
| 403 | nascondere/disabilitare la funzione; dopo un 403 inatteso richiamare `/api/Auth/context` |
| 404 | "Elemento non trovato o non più disponibile"; ricaricare la lista |
| 409 | ricaricare la risorsa e chiedere all'utente di riprovare; per i `code` noti mostrare un messaggio dedicato |
| 413/415/422 | messaggio specifico sul file |
| 428 | errore di programmazione: inviare sempre `If-Match` |
| 429 | attendere `Retry-After` secondi; disabilitare temporaneamente il pulsante |
| 500 | "Errore imprevisto"; mostrare `X-Correlation-ID` |

---

## 3. Paginazione, filtri, ordinamento, ricerca

La paginazione **non è uniforme**: i nomi dei parametri e il formato della risposta cambiano da modulo a modulo.

| Modulo | Parametri | Default | Limiti | Forma della risposta |
|---|---|---|---|---|
| Catalogo: study-areas, study-paths, biennia, biennium-study-areas, biennium-study-paths | `pageNumber`, `pageSize` | 1, 20 | `pageSize` 1–100 | `{ items, totalCount, pageNumber, pageSize }` |
| Classi | `pageNumber`, `pageSize` | 1, 20 | 1–100 | `{ items, totalCount, pageNumber, pageSize }` |
| Sedi (campuses), classi-materie | `pageNumber`, `pageSize` | 1, 20 | 1–100 | **array**, totale nell'header **`X-Total-Count`** |
| Edizioni corso (e sotto-risorse), iscrizioni | `pageNumber`, `pageSize` | 1, 20 | 1–100, `pageNumber` ≤ 1.000.000 | **array** senza totale |
| Membri | `pageNumber`, `pageSize` | 1, 20 | 1–100 | **array** senza totale |
| Storico trasferimenti | `pageNumber`, `pageSize` | 1, 20 | 1–100 | `{ items, totalCount, pageNumber, pageSize, currentRevision }` |
| Voti (`/grades/paged`) | `pageNumber`, `pageSize` | 1, 20 | **valori fuori range non danno errore**: `pageNumber<1`→1, `pageSize` fuori 1–100→20 | `{ items, totalCount }` |
| Audit membership, acquisizioni profilo, calendario (storico), email (storico), piani formativi, import | `page`, `size` | 1, 20 | 1–100 | **array** |
| Email assignments, elenco import | `page`, `size` | 1, 20 | 1–100 | `{ pageNumber, pageSize, totalCount, items }` |
| Righe di un import | `page`, `size` | 1, 20 | 1–100 | `{ pageNumber, pageSize, totalCount, items }` |
| Lezioni calendario | `page`, `size` | 1, **100** | 1–100 | **array** |
| Studenti, docenti, materie, esami, voti (non paginati) | – | – | – | **array completo** |

**Filtri.** Alcuni endpoint **rifiutano con 400 qualunque parametro di query non previsto** (`"Filtro non supportato."`): catalogo, classi, sedi, classi-materie, storico trasferimenti, `transfer-class`. Non aggiungere parametri "di comodo" (cache-buster come `?_=123`) a questi URL.

**Ordinamento.** Nessun endpoint accetta parametri di ordinamento: l'ordine è fissato dal server (es. lezioni per `startUtc`). Ordinare lato client se serve.

**Ricerca testuale.** Solo:
- `GET /api/institution/admin/members?search=` (1–100 caratteri, non vuoto);
- `GET /api/institution/students/bylastname/{lastName}`, `GET /api/institution/teachers/bylastname/{lastName}`;
- `GET /api/institution/subjects/byname/{name}` (corrispondenza esatta lato repository, non verificata come "contiene").

Un filtro **presente ma vuoto** (`?status=`) è un errore 400 negli endpoint membership/audit. Se non si vuole filtrare, omettere il parametro.

---

## 4. Concorrenza ottimistica

Sono presenti **tre meccanismi**, a seconda del modulo.

### 4.1 `ETag` / `If-Match` (catalogo)

Si applica a sedi, aree di studio, percorsi, bienni, classi e classi-materie.

1. Il `GET /{id}` restituisce l'header `ETag: "<revision>"`. Il body contiene anche `revision`.
2. Il `PUT /{id}` richiede l'header **`If-Match: "<revision>"`**, con le virgolette e solo cifre.
   - Header mancante → **428**.
   - Header malformato → **400**.
   - Revisione obsoleta → **409** (dal repository).
3. Usare il campo `revision` del body per costruire `If-Match`: l'header `ETag` potrebbe non essere leggibile cross-origin (vedi §7).

### 4.2 `expectedRevision` nel body

Si applica a calendario, piani formativi, email, import, iscrizioni, trasferimenti e membership. Il body contiene `expectedRevision`, che deve coincidere con la `revision` corrente della risorsa. Se non coincide → **409**, ad esempio `calendar.revision-conflict`, `StalePreview` o `"Revision obsoleta."`.

- Calendario: per **creare** una lezione e per la **prima configurazione** delle impostazioni si usa `expectedRevision: -1`. Le revisioni partono da 0.
- Email policy: `POST` vuole `expectedRevision: 0`, mentre `PUT` vuole `expectedRevision >= 1`.

### 4.3 `version` (edizioni corso)

`CourseEditionDto.version` è un hash SHA-256 esadecimale di 64 caratteri, calcolato sul contenuto. Va rimandato invariato in `PUT /course-editions/{id}`. Se non coincide → **409**.

### 4.4 `clientRevision` (risposte d'esame)

Il salvataggio delle risposte usa un contatore **crescente gestito dal client**. Se si invia un valore ≤ a quello già salvato, la risposta è `accepted: false` con il `currentRevision` del server. Vedi [FLOWS §5](FLOWS.md#5-svolgimento-esame-studente).

---

## 5. Rate limiting

Fonte: `Operations/OperationalPipeline.cs`, `ExceptionHandling/GlobalExceptionHandler.cs`.

| Limite | Ambito | Valore | Risposta |
|---|---|---|---|
| Globale sui `POST` | **tutti i `POST` sotto `/api`**, per IP | `Transport:SensitivePermitLimit` richieste/minuto (default **60**), finestra fissa di 1 minuto, nessuna coda | **429** + `Retry-After: 60` |
| Upload import anagrafiche | `POST /api/institution/imports/preview` | `Multitenancy:InstitutionImport:ConcurrentUploads` upload **contemporanei** (default 2, max 8), nessuna coda | **429** |
| Cambio password | per utente | un tentativo alla volta | **429** + `Retry-After: 60` |

`GET`, `PUT` e `DELETE` **non sono limitati**. Per gli esami: `PUT .../answers` (autosalvataggio) non è limitato, mentre `POST .../submit` e `POST .../violations` lo sono. Con molti studenti dietro lo **stesso IP** (es. NAT di una scuola) il limite di 60 POST/min è condiviso: vedi [OPEN_QUESTIONS Q-11](OPEN_QUESTIONS.md#q-11).

---

## 6. Codici di errore applicativi (`code`)

### 6.1 Calendario (`Api/ExceptionHandling/CalendarErrorsAttribute.cs`)

Risposta: `{ "title": "<code>", "status": 400|409, "code": "<code>", "details": <opzionale> }`.

- **409**: conflitti (`CalendarConflictException`, es. `calendar.overlap`, `calendar.teacher-unavailable`, `calendar.duplicate-identity`, `calendar.event-payload-conflict`, `calendar.database-conflict`) e codici di validazione che contengono `revision` o `required`, cioè precondizioni di stato. Esempi: `calendar.revision-conflict`, `calendar.draft-required`, `calendar.scheduled-required`, `calendar.completed-required`, `calendar.published-plan-required`, `calendar.settings-required`, `calendar.teaching-configuration-required`, `calendar.event-required`.
- **Eccezioni → 400**: `calendar.minute-precision-required`, `calendar.reason-required`.
- **400**: tutti gli altri codici di validazione.

| Codice | Significato pratico |
|---|---|
| `calendar.reason-required` | `reason` mancante, vuota, > 500 caratteri o con caratteri di controllo |
| `calendar.minute-precision-required` | orari con secondi ≠ 0 |
| `calendar.same-day-positive-interval-required` | `endsAt` ≤ `startsAt` |
| `calendar.nonexistent-local-time` / `calendar.ambiguous-local-time` | orario che cade nel cambio ora legale |
| `calendar.revision-conflict` | `expectedRevision` obsoleta |
| `calendar.overlap`, `calendar.teacher-unavailable`, `calendar.class-unavailable` | sovrapposizione con altre lezioni / risorsa non disponibile |
| `calendar.module-unavailable`, `calendar.module-or-class-subject-unavailable`, `calendar.class-subject-required`, `calendar.inconsistent-module-subject` | modulo del piano o materia della classe non coerenti |
| `calendar.published-plan-required`, `calendar.inconsistent-plan-binding` | l'edizione corso della classe non ha un piano formativo pubblicato collegato |
| `calendar.draft-required`, `calendar.no-draft-lessons` | pubblicazione/modifica possibile solo su lezioni in bozza |
| `calendar.scheduled-required`, `calendar.published-scheduled-required`, `calendar.completed-required` | transizione non ammessa nello stato attuale |
| `calendar.invalid-delivered-minutes`, `calendar.unconfirmed-delivered-minutes` | minuti erogati non validi (1–1440) |
| `calendar.invalid-substitution`, `calendar.invalid-recovery` | sostituzione o lezione di recupero non valide |
| `calendar.invalid-time-zone`, `calendar.invalid-approval-lead-days`, `calendar.invalid-lead-days` | impostazioni non valide (lead days 0–366) |
| `calendar.range-limit` | intervallo `from`–`to` > 366 giorni o invertito (**400 senza `code`**: arriva dall'handler globale) |
| `calendar.invalid-page` | paginazione non valida (**400 senza `code`**) |
| `calendar.invalid-import-settings` | JSON `settings`/`parsing` dell'upload non valido. Body `{ "code": "calendar.invalid-import-settings" }`, **non** ProblemDetails |
| `calendar.preview-expired-or-consumed`, `calendar.preview-stale-or-invalid`, `calendar.preview-digest-conflict`, `calendar.warnings-not-acknowledged` | conferma import calendario non valida |
| `calendar.invalid-mapping`, `calendar.ambiguous-class`, `calendar.ambiguous-module`, `calendar.row-outside-month`, `calendar.row-outside-scope`, `calendar.invalid-excel-date`, `calendar.invalid-excel-time`, `calendar.invalid-row-width`, `calendar.reference-required` | problemi nelle righe del file importato |
| `calendar.preview-limit`, `calendar.staging-limit` | troppe anteprime o file troppo grande |
| `calendar.under-scheduled`, `calendar.over-scheduled`, `calendar.delivery-incomplete`, `calendar.over-delivered` | **non sono errori**: sono *warning* nei report ore |

### 6.2 Piani formativi

Le issue di validazione (`FormativeHourSummary.issues[].code`) **non sono errori HTTP**:

| Codice | Significato |
|---|---|
| `minutes.invalid` | minuti fuori range |
| `identity.invalid-or-duplicate` | ID duplicati o non validi |
| `order.invalid-or-duplicate` | `displayOrder` duplicati |
| `code.duplicate` | codici unità/modulo duplicati |
| `owner.invalid` | proprietario non valido |
| `version.invalid` | versione non valida |
| `units.count` | numero di unità fuori limite |
| `modules.count` | numero di moduli fuori limite |
| `subject.required-or-unavailable` | modulo senza materia valida |
| `theory.total-mismatch` | somma dei minuti dei moduli ≠ `theoryPlannedMinutes` |

La pubblicazione di un piano con issue → **409** `"Piano formativo non pubblicabile"` con l'estensione `hourSummary`.

### 6.3 Import anagrafiche e calendario (`code` nell'estensione)

| Codice | HTTP | Significato |
|---|---|---|
| `UnsupportedFormat` | 415 | estensione diversa da `.csv` / `.xlsx` |
| `UnsupportedEncoding` | 415 | `parsing.encoding` diverso da `"utf-8"` |
| `InvalidEncoding` | 400 | il file non è UTF-8 valido / è binario |
| `InvalidDelimiter`, `AmbiguousDelimiter`, `DelimiterNotDetected` | 400 | separatore CSV: solo `;` o `,`, da specificare se ambiguo |
| `InvalidParsingOptions`, `InvalidMapping`, `InvalidClassChoice`, `InvalidEmailCandidate`, `NoDataRows` | 400 | impostazioni o scelte non valide, file vuoto |
| `FileTooLarge`, `TooManyRows`, `TooManyColumns`, `CellTooLong`, `RecordTooLong`, `StageTooLarge`, `ZipLimitExceeded`, `XmlLimitExceeded`, `TooManySharedStrings` | 413 | limiti superati (vedi §6.4) |
| `RowsInvalid` | 422 | conferma con righe non valide |
| `StalePreview`, `PreviewContextChanged`, `EventIdConflict`, `WarningsNotAcknowledged`, `RepeatedFileNotAuthorized`, `EmailAvailabilityChanged`, `PreviewQuotaExceeded`, `DatabaseConflict`, `NotCompleted`, `AlreadyCompleted`, `PreviewExpired` | 409 | conflitti di stato dell'anteprima |

### 6.4 Upload file

| Endpoint | Content-Type | Campi form | Limiti | Formati |
|---|---|---|---|---|
| `POST /api/institution/imports/preview` | `multipart/form-data` | `file` (1 solo), `settings` (JSON string, max 262144 caratteri), `parsing` (JSON string facoltativo, max 2048) | richiesta ≤ 64 MiB + 256 KiB. **Limiti effettivi del parser** (configurabili): file 10 MiB, 2000 righe dati, 16 colonne, 512 caratteri/cella, 8192/riga, XLSX ≤ 128 voci zip, 40 MiB decompressi, 200000 nodi XML | `.csv` (UTF-8, separatore `;` o `,`), `.xlsx` |
| `POST /api/institution/calendar/imports/preview` | `multipart/form-data` | `file` (1 solo), `settings` (JSON), `parsing` (JSON facoltativo) | richiesta ≤ 12 MiB | stessi formati |

Nessun altro campo form è ammesso (→ 400). Le anteprime di import scadono dopo **30 minuti** (`TtlMinutes`, configurabile 1–120) e ce ne possono essere al massimo **20 pendenti** (`MaxPendingPreviews`).

### 6.5 Limiti di dimensione del body

Limite globale **1 MiB** (`Program.cs`, Kestrel `MaxRequestBodySize`). Eccezioni:

| Area | Limite |
|---|---|
| Inviti, mutazioni membership, accettazione invito, email, transizioni iscrizione | 16 KiB |
| Violazioni esame | 4 KiB |
| Import anagrafiche (tranne `preview`) | 256 KiB |
| Piani formativi | 2 MiB |
| Calendario | 12 MiB |

Oltre il limite → **413**.

---

## 7. CORS e header

Fonte: `Operations/OperationalPipeline.cs`, `Operations/RuntimePolicy.cs`.

- **Origini consentite**: solo quelle elencate in `Transport:AllowedOrigins`, uguaglianza esatta, senza wildcard né percorso. In Staging/Production le origini devono essere `https`. Se la lista è vuota, **nessuna** richiesta cross-origin è permessa.
- Metodi e header di richiesta: tutti ammessi (`AllowAnyHeader`, `AllowAnyMethod`). Credenziali/cookie: non abilitati (non servono).
- ⚠️ **Nessun header di risposta è esposto** (manca `WithExposedHeaders`). In un'app browser cross-origin, JavaScript **non può leggere** `X-Total-Count`, `ETag`, `X-Correlation-ID`, `Retry-After`, `Location`. Vedi [OPEN_QUESTIONS Q-03](OPEN_QUESTIONS.md#q-03). Come contromisura usare `revision` dal body per `If-Match` e non fare affidamento su `X-Total-Count`.
- Header di sicurezza su ogni risposta: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Permissions-Policy`, **`Cache-Control: no-store`**. Fuori da Development c'è anche `Content-Security-Policy: default-src 'none'`.
- Fuori da `LocalTest`: HSTS e redirect HTTP → HTTPS.

---

## 8. Feature flag e moduli disattivati

I moduli si accendono da configurazione. Valori di default in `appsettings.example.json`: **tutti `false`**.

| Flag | Moduli / endpoint interessati | Effetto se spento |
|---|---|---|
| `Multitenancy:SchoolApi:Enabled` | prerequisito di **tutte** le API `/api/institution/...` (tranne admin membri/inviti/audit) | 403 |
| `Multitenancy:CourseApi:Enabled` | sedi, edizioni corso, iscrizioni, trasferimento classe | 403 |
| `Multitenancy:ExamApi:Enabled` | esami, consegne, voti, classi-materie | 403 |
| `Multitenancy:InstitutionCalendar:Enabled` | calendario | **404** (controller rimosso) |
| `Multitenancy:FormativePlans:Enabled` | piani formativi | **404** (controller rimosso) |
| `Multitenancy:InstitutionEmail:Enabled` | email istituzionali (**flag non presente nell'esempio**) | 403 |
| `Multitenancy:InstitutionImport:Enabled` | import anagrafiche (**flag non presente nell'esempio**) | 403 |
| `Multitenancy:MembershipAdminApi:Enabled` | `/api/institution/admin/members`, `/audit`, `/invitations` | 403 |
| `Multitenancy:MembershipWriteApi:Enabled` | scritture membri/inviti (oltre al precedente) | 403 |
| `Multitenancy:Onboarding:Enabled` | inviti (invio/revoca/ri-emissione/accettazione) | 403 |
| `Multitenancy:InstitutionWorker:Enabled` | chiusura automatica delle consegne d'esame scadute | nessuna chiusura automatica |
| `Multitenancy:Governance:Enabled` | operazioni di piattaforma (nessun endpoint HTTP) | – |

Fuori da `LocalTest`, abilitare qualunque flag `Multitenancy:*` richiede anche `Deployment:RolloutApproved=true`, altrimenti il server non parte.

Il frontend non ha un endpoint per scoprire quali moduli sono attivi: va concordato con chi gestisce l'ambiente, oppure si deduce dai 403/404. Vedi [OPEN_QUESTIONS Q-09](OPEN_QUESTIONS.md#q-09).

---

## 9. WebSocket, SSE, webhook

**Non presenti.** Non ci sono notifiche push: tutto avviene via polling. Gli eventi asincroni lato server sono:

| Job | Frequenza | Effetto visibile al frontend |
|---|---|---|
| `ExpiredExamSubmissionWorker` | ogni 15 s, lotti da 50 | le consegne `InProgress` oltre la `deadlineAt` vengono consegnate automaticamente (`submitReason = TimeExpired`), **solo se** `Multitenancy:InstitutionWorker:Enabled=true` |
| `InstitutionImportRetentionWorker` | ogni minuto | le anteprime di import scadute vengono eliminate (poi `GET` → 404/409) |

---

## 10. Health check

| Endpoint | Auth | Risposta |
|---|---|---|
| `GET /health/live` | nessuna | `200 {"status":"live"}` |
| `GET /health/ready` | nessuna | `200 {"status":"ready"}` oppure `503` senza body (DB non raggiungibile, schema non pronto, identità del DB diversa da quella attesa) |
