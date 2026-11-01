# Punti da verificare

Ambiguità, incoerenze, lacune e bug sospetti emersi dall'analisi del codice. **Nessuno è stato corretto**: il backend non è stato modificato.

I percorsi sono relativi a `Backend/ElectronicRegisterAPI/src/`. Le righe si riferiscono al commit `19e6368`.

Priorità:
- 🔴 blocca o compromette funzionalità per il frontend;
- 🟠 richiede decisioni o workaround;
- 🟡 incoerenza minore.

| ID | Priorità | Tema |
|---|---|---|
| [Q-01](#q-01) | 🔴 | Inviti non operativi: canale di consegna chiuso |
| [Q-02](#q-02) | 🔴 | Enum degli esami serializzati come numeri |
| [Q-03](#q-03) | 🔴 | CORS: header di risposta non esposti |
| [Q-04](#q-04) | 🔴 | Nessun refresh token, logout o recupero password |
| [Q-05](#q-05) | 🟡 | `Jwt:ExpiresInMinutes` non usato |
| [Q-06](#q-06) | 🟠 | `expectedRevision` delle membership non esposta |
| [Q-07](#q-07) | 🟡 | `GET /enrollments` senza `studentId` dà 404 |
| [Q-08](#q-08) | 🟠 | Lo Staff non può leggere le materie |
| [Q-09](#q-09) | 🟠 | Feature flag: nessuna discovery, flag mancanti nell'esempio |
| [Q-10](#q-10) | 🔴 | Bootstrap istituto e primo admin senza HTTP; nessuna ricerca utenti |
| [Q-11](#q-11) | 🟠 | Rate limit per IP su tutti i POST |
| [Q-12](#q-12) | 🟠 | LocalTest accetta solo client loopback; readiness legata a un percorso locale |
| [Q-13](#q-13) | 🟡 | Paginazione e forma delle risposte non uniformi |
| [Q-14](#q-14) | 🟡 | `FailedAuto` mai assegnato; `SubmitReason.Violations` mai usato |
| [Q-15](#q-15) | 🟡 | Errori 400 senza body o non ProblemDetails |
| [Q-16](#q-16) | 🟠 | `InvalidOperationException` non classificate diventano 500 |
| [Q-17](#q-17) | 🟠 | I messaggi di business non arrivano al client |
| [Q-18](#q-18) | 🟡 | Ambito del docente su `GET /class-subjects` non verificato |
| [Q-19](#q-19) | 🟡 | Statistiche mensili dei voti senza filtro per anno |
| [Q-20](#q-20) | 🟡 | OpenAPI solo in Development; `openapi.yaml` non prodotto |
| [Q-21](#q-21) | 🟡 | Formato `TimeOnly` accettato in ingresso |
| [Q-22](#q-22) | 🟡 | Status code di creazione incoerenti |
| [Q-23](#q-23) | 🟡 | File `.http` obsoleto |
| [Q-24](#q-24) | 🟡 | Campo `name` degli esami ignorato silenziosamente |
| [Q-25](#q-25) | 🟡 | `POST /grades` restituisce 404 per studente inesistente |
| [Q-26](#q-26) | 🟡 | Valori enumerati non documentati nel codice |

---

<a id="q-01"></a>
## Q-01 🔴 Inviti non operativi: canale di consegna chiuso

- **Dove:**
  - `Infrastructure/ElectronicRegisterAPI.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs` riga 55: registra `ClosedInvitationChannel`.
  - `Infrastructure/.../Security/ClosedInvitationChannel.cs` riga 19: `IsOperational => false`.
  - `Infrastructure/.../Repositories/InstitutionOnboardingRepository.cs` riga 56: rifiuta le operazioni se il canale non è operativo.
- **Effetto:**
  - `POST /api/institution/admin/invitations`, `.../reissue` e `POST /api/Auth/invitations/{id}/accept` rispondono **403** `"Onboarding non operativo."`, anche con `Multitenancy:Onboarding:Enabled=true`.
  - Funziona solo `.../revoke`.
  - Un adattatore esterno si può installare via configurazione `InvitationChannel:AssemblyPath/SHA256/Type` (`Security/InvitationChannelRegistration.cs`), ma il codice non ne fornisce nessuno.
- **Da chiarire:** come si consegna la `challenge` all'invitato (email? link?) e quando il canale sarà disponibile. Fino ad allora, come entrano gli utenti negli istituti?

<a id="q-02"></a>
## Q-02 🔴 Enum degli esami serializzati come numeri

- **Dove:** `Api/ElectronicRegisterAPI/DependencyInjection/ServiceCollectionExtensions.cs` riga 13 (nessun `JsonStringEnumConverter` globale); `Domain/.../Enums/ExamStatus.cs`, `SubmissionStatus.cs`, `QuestionType.cs`, `SubmitReason.cs`, `CalendarConflictKind.cs` (senza attributo `[JsonConverter]`).
- **Effetto:**
  - In questi campi il JSON contiene `0, 1, 2…`.
  - Gli altri enum (email, import, piani formativi) sono stringhe.
  - Il parametro di query `status` di `GET /exams/{id}/submissions` probabilmente accetta sia il numero sia il nome, perché il model binding ASP.NET degli enum lo consente. Non è stato verificato.
- **Da chiarire:** il formato numerico è definitivo? Se cambiasse, sarebbe un breaking change per il frontend esistente (`Frontend/ElectronicRegisterReact/src/api/examContract.js` usa `fromEnum`).

<a id="q-03"></a>
## Q-03 🔴 CORS: header di risposta non esposti

- **Dove:** `Api/ElectronicRegisterAPI/Operations/OperationalPipeline.cs` riga 42: `WithOrigins(...).AllowAnyHeader().AllowAnyMethod()`, senza `WithExposedHeaders`.
- **Effetto:** da browser cross-origin JavaScript non può leggere `X-Total-Count` (sedi, classi-materie), `ETag` (catalogo), `X-Correlation-ID`, `Retry-After` e `Location`.
- **Workaround:** `revision` dal body per `If-Match`; nessuno per `X-Total-Count`.
- **Da chiarire:** esporre questi header, oppure servire frontend e API dalla stessa origine (reverse proxy).

<a id="q-04"></a>
## Q-04 🔴 Nessun refresh token, logout o recupero password

- **Dove:** `Infrastructure/.../Security/InstitutionTokens.cs` righe 15 e 20 (durate fisse di 300 s e 900 s). Nessun controller espone refresh, logout o reset.
- **Effetto:**
  - Sessione utile di 15 minuti.
  - Il ticket per riselezionare l'istituto dura solo 5 minuti dal login.
  - Un esame di 60 minuti supera la durata del token.
  - Il logout è solo lato client.
- **Da chiarire:** è previsto un meccanismo di rinnovo? Come deve comportarsi lo studente durante un esame lungo?

<a id="q-05"></a>
## Q-05 🟡 `Jwt:ExpiresInMinutes` non usato

- **Dove:** `Api/ElectronicRegisterAPI/appsettings.example.json` riga 18 (`"ExpiresInMinutes": 60`); `Infrastructure/.../Options/JwtOptions.cs` non ha la proprietà.
- **Effetto:** la configurazione suggerisce 60 minuti, ma i token durano 5 minuti (ticket) e 15 minuti (access token).

<a id="q-06"></a>
## Q-06 🟠 `expectedRevision` delle membership non esposta

- **Dove:**
  - `Domain/.../DTOs/InstitutionMemberDto.cs` riga 4: il DTO non contiene revision o security generation.
  - `Api/ElectronicRegisterAPI/Contracts/MembershipMutationRequest.cs`: richiede `ExpectedRevision`.
  - `Domain/.../DTOs/Membership/MembershipMutation.cs`: commento "ExpectedRevision is the persisted security generation".
- **Effetto:** il frontend può ricavare il valore solo da `revisionAfter` dell'ultimo evento di audit, o dalla risposta di una mutazione precedente. Per una membership mai modificata il valore iniziale non è deducibile (probabilmente 0).
- **Da chiarire:** aggiungere la revisione a `InstitutionMemberDto`? Qual è il valore iniziale?

<a id="q-07"></a>
## Q-07 🟡 `GET /enrollments` senza `studentId` dà 404

- **Dove:** `Api/ElectronicRegisterAPI/Controllers/InstitutionEnrollmentController.cs` riga 17: `[FromQuery] Guid studentId`, non nullable.
- **Effetto:** se il parametro manca vale `Guid.Empty`; il manager verifica che lo studente esista e risponde **404** invece di 400.

<a id="q-08"></a>
## Q-08 🟠 Lo Staff non può leggere le materie

- **Dove:**
  - `Api/ElectronicRegisterAPI/Controllers/InstitutionSubjectController.cs` righe 12, 14, 16, 18, 20: `Roles = "InstitutionAdmin,Teacher,Student"`.
  - `Application/.../Services/InstitutionSchoolAuthorization.cs` (`ReadAsync`): ammetterebbe anche lo Staff.
- **Effetto:**
  - Lo Staff gestisce voti ed esami, ma non può elencare le materie con `/subjects`. Deve usare `GET /grades/filters`.
  - Simile: `GET /teachers/count` esclude lo Studente mentre `GET /teachers` lo ammette (`InstitutionTeacherController.cs` riga 16).
- **Da chiarire:** è voluto?

<a id="q-09"></a>
## Q-09 🟠 Feature flag: nessuna discovery, flag mancanti nell'esempio

- **Dove:**
  - `Api/ElectronicRegisterAPI/appsettings.example.json` righe 26 e seguenti: mancano `Multitenancy:InstitutionEmail:Enabled` e `Multitenancy:InstitutionImport:Enabled`, letti in `Infrastructure/.../DependencyInjection/ServiceCollectionExtensions.cs` righe 35 e 66.
  - Calendario e piani formativi spenti fanno sparire le route (404) invece di dare 403 (`Security/CalendarControllerFeatureProvider.cs`, `Security/FormativePlanControllerFeatureProvider.cs`).
- **Effetto:** il frontend non sa quali moduli sono attivi.
- **Da chiarire:** aggiungere un endpoint di capability, oppure documentare i flag per ambiente.

<a id="q-10"></a>
## Q-10 🔴 Bootstrap istituto e primo admin senza HTTP; nessuna ricerca utenti

- **Dove:**
  - `Domain/.../Interfaces/Managers/IInstitutionOnboardingManager.cs` riga 10 (`PlatformAsync`) e `IInstitutionGovernanceManager`: operazioni firmate (`SignedPlatformOperation`) **non esposte da nessun controller**.
  - `InstitutionOnboardingRepository.PlatformAsync` accetta solo `Provision`, `ReissueFirstAdmin` e `RevokeFirstAdmin`. Le altre azioni danno `"Azione non disponibile in questa fase."`.
  - Nessun endpoint cerca un utente per email.
- **Effetto:**
  - Non si può creare un istituto né il suo primo amministratore dal frontend.
  - Per invitare, l'admin deve conoscere il `userId` del destinatario.
  - Gli strumenti in `Backend/ElectronicRegisterAPI/tools/` sono script di migrazione/test, non sono stati analizzati come canale di provisioning.
- **Da chiarire:** procedura ufficiale di provisioning, e come l'admin ottiene il `userId` da invitare.

<a id="q-11"></a>
## Q-11 🟠 Rate limit per IP su tutti i POST

- **Dove:** `Api/ElectronicRegisterAPI/Operations/OperationalPipeline.cs` riga 61: finestra fissa per `RemoteIpAddress` su ogni `POST /api/*`, default 60/min (`Transport:SensitivePermitLimit`).
- **Effetto:** una classe dietro un unico IP pubblico condivide 60 `POST`/min, tra cui `submit` e `violations`. Dietro un proxy non configurato in `Transport:KnownProxies`, tutti gli utenti potrebbero risultare con lo stesso IP.
- **Da chiarire:** limite adeguato per gli esami in aula? Partizione per utente invece che per IP?

<a id="q-12"></a>
## Q-12 🟠 LocalTest accetta solo client loopback; readiness legata a un percorso locale

- **Dove:**
  - `Api/ElectronicRegisterAPI/Operations/OperationalPipeline.cs` riga 77: risponde 403 ai client non loopback in LocalTest.
  - Stesso file, riga 157: `/health/ready` richiede un `datadir` MySQL che termini con `/migrations/mt4/private/mt5a-mamp-13306/data`, se `Readiness:ExpectedDataDir` non è impostato.
  - `Operations/RuntimePolicy.cs`: database su `127.0.0.1:13306`, nome `electronic_register_test`.
- **Effetto:**
  - App mobile o emulatore su altro IP → 403.
  - `/health/ready` probabilmente restituisce 503 su macchine diverse da quella dello sviluppatore originale.
- **Da chiarire:** procedura di sviluppo locale condivisa per il team frontend.

<a id="q-13"></a>
## Q-13 🟡 Paginazione e forma delle risposte non uniformi

- **Dove:** vari controller, riepilogo in [CONVENTIONS §3](CONVENTIONS.md#3-paginazione-filtri-ordinamento-ricerca).
  - Parametri `pageNumber/pageSize` in alcuni endpoint, `page/size` in altri.
  - Risposte talvolta oggetto pagina, talvolta array con `X-Total-Count`, talvolta array senza totale.
  - `Application/.../Managers/GradeManager.cs` riga 84: per `/grades/paged` corregge silenziosamente i valori fuori range, mentre gli altri endpoint rispondono 400.
- **Effetto:** servono adattatori distinti nel client.

<a id="q-14"></a>
## Q-14 🟡 `FailedAuto` mai assegnato; `SubmitReason.Violations` mai usato

- **Dove:**
  - `Business/.../Services/SubmissionGradingService.cs` riga 78: lo stato dopo la consegna è solo `PendingReview` o `Graded`.
  - `Application/.../Services/ExamSubmissionProcessor.cs` riga 196: il ramo `FailedAuto` è quindi irraggiungibile.
  - `SubmitReason.Violations` compare solo nel mapper (`Infrastructure/.../Mappers/SubmitReasonMapper.cs` righe 11 e 17). `ProcessInstitutionAsync` accetta solo `Manual` e `TimeExpired`.
- **Effetto:**
  - Il frontend esistente mostra un messaggio per `failed_auto` che non comparirà mai.
  - Le violazioni non causano mai una consegna forzata.
- **Da chiarire:** funzionalità previste o residui?

<a id="q-15"></a>
## Q-15 🟡 Errori 400 senza body o non ProblemDetails

- **Dove:**
  - `Api/ElectronicRegisterAPI/Security/StaffBodyContractAttribute.cs` righe 27 e 33.
  - `Controllers/InstitutionImportController.cs` righe 33 e 43.
  - `Controllers/InstitutionCalendarController.cs` riga 98, e riga 109 (`{ code: "calendar.invalid-import-settings" }`).
  - `Controllers/InstitutionEmailController.cs` righe 23 e 31.
  - `Controllers/InstitutionSubmissionController.cs` riga 22.
- **Effetto:** il parser degli errori deve tollerare body vuoti e oggetti `{code}`.

<a id="q-16"></a>
## Q-16 🟠 `InvalidOperationException` non classificate diventano 500

- **Dove:** `Api/ElectronicRegisterAPI/ExceptionHandling/GlobalExceptionHandler.cs` riga 31 (`_ => 500`). Le `InvalidOperationException` senza il marcatore `SubmissionErrors` diventano 500, ad esempio:
  - `Application/.../Managers/ExamManager.cs` riga 432 (`"Stato incoerente: …"`);
  - le verifiche "struttura dell'esame incompleta" in `ExamManager.GetByIdAsync`.
- **Effetto:** alcuni stati incoerenti dei dati appaiono come errori generici del server.

<a id="q-17"></a>
## Q-17 🟠 I messaggi di business non arrivano al client

- **Dove:** `Api/ElectronicRegisterAPI/ExceptionHandling/GlobalExceptionHandler.cs` riga 44: `Detail = title`. Il messaggio dell'eccezione, ad esempio `"Lo studente ha voti registrati."`, non viene incluso.
- **Effetto:**
  - Per i 409 il frontend riceve solo `"Operazione non consentita"` e non può spiegare il motivo all'utente.
  - Eccezioni: trasferimenti di classe (`StudentTransferErrorsAttribute`, `detail` valorizzato), calendario e import (`code`).
- **Da chiarire:** introdurre codici applicativi per le regole più comuni (esami, voti, catalogo)?

<a id="q-18"></a>
## Q-18 🟡 Ambito del docente su `GET /class-subjects` non verificato

- **Dove:** `Application/.../Managers/InstitutionClassSubjectManager.cs` (`ListAsync`): il manager verifica il profilo docente ma passa il filtro `teacherId` del client al repository.
- **Effetto:** non è stato verificato se `InstitutionClassSubjectRepository.ListAsync` limita i risultati alle assegnazioni del docente quando il chiamante non è admin.

<a id="q-19"></a>
## Q-19 🟡 Statistiche mensili dei voti senza filtro per anno

- **Dove:** `Infrastructure/.../Repositories/GradeRepository.cs` riga 125: `GroupBy(g => g.Date.Month)`, senza filtro per anno.
- **Effetto:**
  - `monthlyAverage[9]` (ottobre) somma i voti di ottobre di **tutti gli anni**.
  - L'ordine è per anno solare (gennaio = 0), non per anno scolastico.
  - Senza voti l'endpoint risponde 404 invece di valori vuoti.

<a id="q-20"></a>
## Q-20 🟡 OpenAPI solo in Development; `openapi.yaml` non prodotto

- **Dove:** `Api/ElectronicRegisterAPI/Program.cs` righe 46–50: `MapOpenApi()` e `MapScalarApiReference()` solo se `IsDevelopment()`.
- **Decisione documentale:** non è stato scritto a mano un `docs/openapi.yaml`. Con 197 endpoint, una specifica manuale rischierebbe di divergere dal codice. È preferibile esportare quella generata: `GET /openapi/v1.json`, avviando il backend in Development.
- **Da chiarire:** la spec generata non contiene i dettagli che il framework non vede: ruoli, `If-Match`, `code` di errore, campi `multipart`. Questi restano descritti in [API.md](API.md).

<a id="q-21"></a>
## Q-21 🟡 Formato `TimeOnly` accettato in ingresso

- **Dove:** `Domain/.../DTOs/Calendar/CalendarLessonInput.cs` e `CalendarRescheduleCommand.cs` (`TimeOnly`).
- **Effetto:** System.Text.Json serializza `TimeOnly` come `"HH:mm:ss"`. Non è stato verificato se in ingresso accetta anche `"HH:mm"`. Consigliato inviare sempre `"HH:mm:ss"`.

<a id="q-22"></a>
## Q-22 🟡 Status code di creazione incoerenti

- **Dove:**
  - `Controllers/IdentityRegistrationController.cs` riga 15: 201 senza `Location`.
  - `Controllers/InstitutionCourseEditionController.cs` riga 49: 201 senza body, con `Location` che punta all'elenco.
  - `Controllers/InstitutionFormativePlanController.cs` riga 17 e seguenti: le creazioni rispondono **200**.
  - Email e calendario: le creazioni rispondono **200** con una ricevuta.
- **Effetto:** il client deve accettare `200 || 201` come successo.

<a id="q-23"></a>
## Q-23 🟡 File `.http` obsoleto

- **Dove:** `Api/ElectronicRegisterAPI/ElectronicRegisterAPI.http` riga 3: `GET /weatherforecast/`, endpoint inesistente (template di progetto). Anche `Api/ElectronicRegisterAPI/README.md` è vuoto.

<a id="q-24"></a>
## Q-24 🟡 Campo `name` degli esami ignorato silenziosamente

- **Dove:** `Domain/.../DTOs/CreateExamDto.cs` riga 8 e `UpdateExamDto.cs` riga 8: `[JsonIgnore]` su `Name`. `Application/.../Mappers/ExamCreationMapper.cs` usa il nome della materia.
- **Effetto:** un campo "Titolo esame" nella UI non avrebbe effetto. Esami diversi della stessa materia hanno lo stesso `name`: distinguerli con `description` e le date.

<a id="q-25"></a>
## Q-25 🟡 `POST /grades` restituisce 404 per studente inesistente

- **Dove:** `Controllers/InstitutionGradeController.cs` riga 34: `AddGradeAsync` restituisce `null`, quindi `NotFound()`.
- **Effetto:** un 404 su un `POST` può confondere (sembra una route sbagliata).

<a id="q-26"></a>
## Q-26 🟡 Valori enumerati non documentati nel codice

Valori che il codice non enumera in modo centrale (sono stringhe libere lette dal DB):
- `CourseAdministrationEventDto.action`;
- `CalendarHistory.operation` (oltre a quelli del controller);
- `CalendarPeriod.status`;
- `EmailAssignment.state` (oltre a `reserved` e `retired`);
- `ImportBatch.state` (oltre a `Preview` e `Completed`);
- `EmailHistory.operation`, `FormativeHistory.operation`;
- le colonne dei template CSV.

Vanno confermati dal team backend, oppure ricavati osservando le risposte reali.
