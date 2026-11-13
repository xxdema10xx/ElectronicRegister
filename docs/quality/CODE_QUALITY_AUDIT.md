# ElectronicRegister — audit indipendente del backend

> Remediation successiva: implementazioni, prove e rischi residui sono documentati in [CODE_QUALITY_REMEDIATION.md](CODE_QUALITY_REMEDIATION.md). Il testo e il voto sotto restano l'evidenza storica dell'audit, non una nuova valutazione automatica.

Data: 30 ottobre 2026. Oggetto: working tree corrente, incluso codice non tracciato del cutover; confronto Git con HEAD. **Esito: non approvato per deployment incondizionato. Valutazione: 21/30.** Il cutover è effettivo, ma non equivale alla chiusura della qualità operativa e della sicurezza concorrente.

Non sono stati modificati codice applicativo, test o database; nessun deployment, commit o push. Sono stati creati questo documento e gli strumenti/evidenze sotto `docs/quality/evidence`. Build e Unit hanno aggiornato esclusivamente i normali output di compilazione. Il guard genera un inventario: il contenuto originale dell'inventario preesistente è stato ripristinato byte per byte nel `finally` del comando.

## Metodo, fonti e limiti

Consultati il report `docs/refactoring/RESPONSIBILITY_REFACTORING_REPORT.md`, l'inventario `RESPONSIBILITY_REFACTORING_INVENTORY.json`, `LEGACY_RETIREMENT_MATRIX.json`, `INSTITUTIONAL_API_SURFACE.json`, il contratto di cutover e i documenti/evidenze `validation/current-cutover/final`. La proposta di completamento è storica e dichiara esplicitamente di essere superata: le sue attività residue non sono state trattate automaticamente come problemi attuali.

La ricognizione sintattica comprende tutti i 659 sorgenti. La revisione semantica è mirata ai confini JWT/IAM/account/platform, factory e repository tenant-bound, workflow esami/submission/voti, query di directory, calendario/piani/import, worker, migrazioni additive e configurazione operativa. Non costituisce una prova formale di ogni ramo dei 659 file. Il diff Git comprende modifiche pregresse: non è attribuibile integralmente al solo ultimo intervento. Sono stati esaminati anche i cambi dei test, in particolare `ExamSecurityTests`, `Phase4_ExamLifecycleTests`, i client fixture e i confronti nominativi delle TRX.

Per i percorsi abbreviati nelle schede: `Api/`, `Application/`, `Business/`, `Domain/`, `Infrastructure/` indicano rispettivamente `Backend/ElectronicRegisterAPI/src/<layer>/ElectronicRegisterAPI[.<layer>]/`; il progetto Api si chiama `ElectronicRegisterAPI`.

### Controlli realmente eseguiti

| Controllo | Risultato ed evidenza |
|---|---|
| SDK | .NET SDK 10.0.401, runtime 10.0.12, Windows |
| Build completa e analyzer SDK | `dotnet build Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx --no-restore --no-incremental -p:RunAnalyzers=true -p:EnableNETAnalyzers=true -p:AnalysisLevel=latest`: 0 errori, **14 warning**; [log](evidence/build.log) |
| Guard Roslyn disponibile | `dotnet run --project tools/CodeOrganization --no-restore -- check .`: 659 file, 656 tipi, **0 violazioni**; [log](evidence/roslyn-guard.log) |
| Unit rieseguiti | 590 Passed, 0 Failed/Skipped; [TRX](evidence/unit/audit-unit.trx), [log](evidence/unit.log) |
| Integration storici | Riletti direttamente TRX e contatori: 1458 risultati, tutti Passed; hash coincidente con il riepilogo. **Non rieseguiti**, per non eseguire fixture che modificano il DB. [Contatori](evidence/trx-counters.json), [verifica hash](evidence/independent-verification.json) |
| Dismissioni e inventario | 83 percorsi distinti, tutti assenti; 659 sorgenti, 28 controller; nessun match nei sorgenti backend per ClaimsContext, UserRole, ILegacyAccess, LegacyEndpointGate, JwtTokenGenerator, LegacyAccess. [Evidenza](evidence/independent-verification.json) |
| Diff | Statistica e controllo whitespace in [stat](evidence/git-diff-stat.txt) e [check](evidence/git-diff-check.txt); nessun errore whitespace rilevato |
| NuGet | Primo tentativo bloccato da accesso al NuGet.Config utente. Tentativo con config locale senza sorgenti restituisce zero vulnerabilità, ma **non è uno scan valido**. Tentativo con nuget.org fallisce per TLS; [log online](evidence/nuget-online.log). La build segnala comunque NU1902 su MySqlConnector 2.4.0 e l'advisory è stato verificato direttamente |
| Complessità/duplicazione | Strumento Roslyn riproducibile incluso in [metrics-tool](evidence/metrics-tool/Program.cs); 1182 metodi con corpo, 7 gruppi di duplicati esatti di token, soglia minima 120 caratteri. [Risultati](evidence/metrics.json) |
| Manifest di bootstrap | Confronto indipendente dei byte SHA256: **66 voci mancanti o non coincidenti**; [elenco](evidence/bootstrap-manifest-check.json). Il verifier Python non è stato eseguito: `python` non disponibile nel PATH |

I 14 warning sono 10 warning nullable nei test e 4 NU1902, non 14 vulnerabilità distinte. Gli analyzer sono quelli SDK e dei pacchetti già presenti: non è stato eseguito SonarQube. Nessuna percentuale di coverage, mutation score, scansione container o misura di latenza/EXPLAIN è stata prodotta. La metrica Roslyn conta costrutti di diramazione, ternari e operatori logici: è un **indicatore sintattico**, non McCabe certificato né cognitive complexity Sonar. Non misura duplicazione semantica o cloni parziali.

## Risultati che bloccano la chiusura del deployment

### F01 — High — Manifest verificato dalla CI incompatibile con il cutover corrente

- **File/metodo:** `migrations/bootstrap/manifest.json`, ciclo top-level di `tools/verify-project-artifacts.py`; job `source` di `.github/workflows/verify.yml`.
- **Evidenza:** il verifier confronta SHA256 dei byte e usa `resolve(strict=True)`. Il controllo indipendente trova 66 discrepanze. Il manifest contiene ancora `Application/DependencyInjection/AddApplicationManagers.cs`, mentre la composizione corrente è in `ServiceCollectionExtensions.cs`; differiscono anche Program e controller modificati. Il job CI esegue il verifier prima della build. Non è una semplice differenza di nomi dei test. Il conteggio include possibili differenze EOL: il percorso ritirato rimane un errore anche normalizzando EOL.
- **Impatto:** la chiusura degli artefatti non è certificata per questa versione; il gate CI configurato non può validare questo insieme di file. Il verde del guard Roslyn non controlla questo manifest.
- **Soluzione consigliata:** mantenere immutato il manifest storico se costituisce evidenza storica; introdurre un manifest di release corrente con chiusura SQL/config/tool esplicita e far verificare quello dalla CI. Evitare il semplice aggiornamento indiscriminato degli hash senza revisione.
- **Test necessari:** verifier completo su checkout pulito Linux e Windows, file presenti, hash/EOL/ignore validi e bootstrap corrente su istanza nuova.
- **Rischio della modifica:** medio; un manifest aggiornato male può occultare file di migrazione mancanti. **Bloccante per la pipeline di rilascio attuale.**

### F02 — High — Rivalidazione IAM transazionale degli esami limitata agli attori con ruolo Staff

- **File/metodo:** `Application/Managers/InstitutionExamManager.cs`, `Open` e `StaffTransaction.ExecuteAsync` (circa righe 39–77); `Infrastructure/Repositories/InstitutionExamDataScope.cs`, `SaveAsync` (riga 108); `Infrastructure/Repositories/InstitutionWriteAuthorization.cs`, `LockAsync`.
- **Evidenza:** `Open` rivalida il tenant prima della transazione, ma avvolge `transactions` con il lock IAM soltanto se `t.Roles.Contains(Staff)`. `SaveAsync` esige `RequireLocked` soltanto per Staff. Per Teacher/Student/InstitutionAdmin senza Staff, i lock su Exam/Submission proteggono la risorsa, non la membership/security generation. `InstitutionMembershipMutationRepository.MutateAsync` può nel frattempo sospendere la membership o rimuovere un ruolo. Le scritture scuola usano invece `InstitutionSchoolQueries.WriteAuthorizedAsync` e `authorization.LockAsync` per ogni attore.
- **Impatto:** una richiesta già autorizzata, sospesa prima di acquisire il lock della risorsa, può scrivere dopo il commit di una revoca IAM. Un caso concreto da riprodurre è Update di una bozza docente: fermare il flusso dopo `Open`, revocare Teacher, rilasciare il flusso; non vi è una rivalidazione della generation nel percorso di salvataggio. Non è dimostrato un accesso cross-tenant e non è stato eseguito un exploit SQL durante questo audit.
- **Soluzione consigliata:** definire un unico punto di linearizzazione per le scritture interattive e applicare lock/rivalidazione IAM anche agli altri ruoli prima di leggere lo snapshot business; mantenere separato il capability tecnico del worker. Uniformare l'ordine dei lock, evitando upgrade share→exclusive non necessari.
- **Test necessari:** interleaving deterministici revoca/sospensione contro update/publish/start/autosave/review/finalize e voto manuale per Teacher, Student e Admin; entrambe le sequenze di acquisizione, controllo dati e audit invariati quando la revoca vince. Valutare separatamente revoca globale della password: `TenantContext` non trasporta identity generation e il lock membership non risolve quel caso.
- **Rischio della modifica:** alto; cambia contesa, ordine dei lock e probabilità di deadlock. **Bloccante per certificare la revoca concorrente; osservazione statica da chiudere con riproduzione e correzione o un contratto esplicito accettato.** La revoca sulle richieste successive è già controllata e non va confusa con questa finestra.

### F03 — Medium — Driver SQL con advisory noto; VerifyFull da solo non dimostra mitigazione

- **File/metodo:** `Infrastructure/ElectronicRegisterAPI.Infrastructure.csproj` e relativi `packages.lock.json` (dipendenza transitiva MySqlConnector 2.4.0), `Api/Operations/RuntimePolicy.cs`, `Read`.
- **Evidenza:** NU1902 nella build corrente per [GHSA-473q-m89c-ghf8](https://github.com/advisories/GHSA-473q-m89c-ghf8). L'advisory del maintainer indica versioni precedenti a 2.6.1 e una possibile divulgazione password con VerifyCA/VerifyFull senza CA configurata, in particolari negoziazioni TLS. `Read` richiede VerifyFull e password non vuota, ma non impone `SslCa`; il test `StrictStagingRequiresVerifiedTlsNonRootIdentityAndClosedRollout` accetta proprio una stringa senza CA. Non è provato che il server reale usi la negoziazione vulnerabile; una CA validata può mitigare.
- **Impatto:** dipendenza vulnerabile distribuita con il backend; sotto le condizioni dell'advisory un MITM può ottenere la password SQL. Il job `check-dependency-audit.py` rifiuta qualsiasi advisory, quindi lo scan CI completo non può essere sostituito dal tentativo offline senza sorgenti.
- **Soluzione consigliata:** aggiornare il driver a una versione corretta compatibile con Pomelo e MySQL 5.7.24 e rigenerare lock con review; fino ad allora provare e imporre una mitigazione TLS conforme all'advisory nella configurazione di release.
- **Test necessari:** scan NuGet online riuscito su dipendenze transitive, connessione reale 5.7.24 con CA valida, hostname errato e certificato non attendibile, verificando il rifiuto prima della trasmissione credenziali.
- **Rischio della modifica:** medio; handshake TLS/provider e lock dependency possono cambiare. **Bloccante finché non risolto o mitigato con evidenza; esposizione condizionale, non furto credenziali osservato.**

## Difetti funzionali e miglioramenti prioritari

### F04 — Medium — Readiness verde anche con migrazioni richieste mancanti

- **File/metodo:** `Api/Operations/OperationalPipeline.cs`, `MapOperationalEndpoints`, handler `/health/ready`; `Infrastructure/Repositories/IdentityAccountRepository.cs`, `GenerationAsync`.
- **Evidenza:** la readiness interroga soltanto VERSION, hostname, port, datadir e DATABASE. Non legge schema version, tabelle o trigger. Il callback JWT interroga sempre `identity_account_state`, aggiunta dalle nuove SQL; un DB con identità corretta ma senza quella tabella passa il controllo di readiness e fallisce le richieste autenticate. Program non applica automaticamente queste migrazioni.
- **Impatto:** instradamento del traffico verso un processo incapace di autenticare o eseguire workflow; bootstrap incompleto non rilevato.
- **Soluzione consigliata:** marker di schema/release validato dal provisioning e verificato da readiness con query limitata; distinguere liveness da idoneità applicativa, senza applicare DDL dalla probe.
- **Test necessari:** stesso server/schema nominale con una migrazione o trigger obbligatorio mancante → 503; migrazioni complete → 200; permessi SQL insufficienti → 503 senza dettagli sensibili.
- **Rischio della modifica:** medio; probe troppo rigide possono rendere indisponibile un rollout graduale. **Gate operativo di release da chiudere** tramite correzione o preflight esterno obbligatorio e attestato.

### F05 — Medium — Paginazione voti senza ordine totale e con overflow dell'offset

- **File/metodo:** `Infrastructure/Repositories/GradeRepository.cs`, `GetPagedAsync`; `Application/Managers/GradeManager.cs`, `GetPagedAsync`.
- **Evidenza:** ordinamento Date, ClassSubjectId, Value, StudentId senza tie-breaker Id. Voti distinti possono condividere tutte queste colonne. `Skip((pageNumber - 1) * pageSize)` calcola in int; il manager limita pageSize a 100 ma non pageNumber. Ad esempio pageNumber=2147483647, pageSize=100 produce overflow. Directory membership e piani hanno invece già il controllo dell'offset in long.
- **Impatto:** pagine instabili con duplicazioni/omissioni; richieste estreme possono produrre offset errato o errore del provider. Non è un bypass tenant.
- **Soluzione consigliata:** `ThenBy(Id)` e validazione checked/long dell'offset; per elenchi grandi valutare keyset.
- **Test necessari:** più voti con stessa tupla ordinata distribuiti su pagine, nessun duplicato/omissione; estremi di int, pagine fuori range e righe concorrenti con contratto dichiarato.
- **Rischio della modifica:** basso; cambia l'ordine relativo delle parità. Non blocca da solo il deployment.

### F06 — Medium — CancellationToken perso sull'intera superficie esami/voti interattiva

- **File/metodo:** `Domain/Interfaces/Managers/IInstitutionExamManager.cs`; `Application/Managers/InstitutionExamManager.cs`, `Open`; `Api/Controllers/InstitutionExamController.cs`; `Infrastructure/Repositories/ExamRepository.cs` e `GradeRepository.cs`.
- **Evidenza:** le firme del manager non accettano ct; `Open` chiama `RequireAsync(CancellationToken.None)`; query e lock di repository non ricevono RequestAborted. Il percorso tecnico dispone invece di un ct dedicato. Il problema è nel contratto e non solo in una chiamata dimenticata.
- **Impatto:** client disconnessi continuano a consumare query, connessioni e attese sui lock; sotto contesa il carico sopravvive alla richiesta. Non implica che un commit debba essere interrotto in qualsiasi punto.
- **Soluzione consigliata:** propagare ct dal controller attraverso manager/factory/repository e definire il punto oltre il quale il completamento atomico ha priorità sulla cancellazione; cleanup indipendente da ct come già in TransactionManager.
- **Test necessari:** annullamento prima del lock, durante attesa e durante lettura; rollback e riuso del pool senza modifiche parziali.
- **Rischio della modifica:** medio; aggiunge percorsi di eccezione e rollback. Prioritario, non blocco autonomo.

### F07 — Medium — Caricamento esame con collezioni sorelle moltiplica le righe SQL

- **File/metodo:** `Infrastructure/Repositories/ExamRepository.cs`, `GetByIdAsync` e `GetByIdForUpdateAsync`.
- **Evidenza:** Include Questions→QuestionOptions insieme a Include ExamStudents nello stesso albero; nessun AsSplitQuery o proiezione separata nel metodo. La configurazione DbContext non imposta una query splitting behavior globale. Nel percorso di update la struttura completa viene caricata sotto transazione/row lock.
- **Impatto:** moltiplicazione delle righe domande/opzioni per studenti assegnati; tempo e memoria aumentano e allungano il lock. È un rischio strutturale verificato, non una latenza misurata o un N+1 dimostrato.
- **Soluzione consigliata:** proiettare/leggere separatamente le collezioni preservando il lock iniziale e la coerenza richiesta; evitare un cambio indiscriminato a split query sui percorsi autorizzativi.
- **Test necessari:** budget query, righe e memoria con esami grandi, confronto SQL 5.7.24 e concorrenza Start/Close/Update dopo la modifica.
- **Rischio della modifica:** medio; letture multiple possono cambiare snapshot e atomicità. Ottimizzazione prioritaria prima di carichi elevati.

### F08 — Medium — Cambio password escluso dal limite delle operazioni sensibili

- **File/metodo:** `Api/Operations/OperationalPipeline.cs`, `AddOperationalServices`; `Api/Controllers/IdentityAccountController.cs`, `ChangePassword`; `Application/Managers/IdentityAccountManager.cs`, `ChangePasswordAsync`.
- **Evidenza:** il GlobalLimiter limita soltanto POST sotto `/api`; cambio password usa PUT e verifica BCrypt della password corrente, poi della nuova e ne calcola l'hash. Non ha un limiter dedicato. Un ticket Selection valido consente ripetuti tentativi falliti senza limite applicativo in questo percorso.
- **Impatto:** consumo CPU e tentativi ripetuti sulla password con ticket compromesso. Il requisito di ticket valido limita l'attaccante; non è un endpoint anonimo di brute force.
- **Soluzione consigliata:** policy dedicata al cambio credenziali con partizione account/IP e controllo della concorrenza; definire comportamento multi-replica. Il rate limiter in memoria è per processo.
- **Test necessari:** PUT ripetute → 429 al budget stabilito, account distinti isolati, proxy fidato e rotazione ticket non aggirabile per account.
- **Rischio della modifica:** basso/medio; evitare denial of service involontario a utenti dietro NAT. Miglioramento di sicurezza prioritario.

### F09 — Medium — Responsabilità di dominio e policy di scrittura concentrate nel generico data scope

- **File/metodo:** `Infrastructure/Repositories/InstitutionExamDataScope.cs`, `Query` (riga 79), `SaveAsync` (riga 108).
- **Evidenza:** `Query<T>` usa una catena di confronti typeof e cast, con fallback `Owned<T>()`; `SaveAsync` contiene policy Staff, lifecycle degli esami, attribuzione audit, compatibilità Grade/Submission e whitelist del worker. Proxy di diramazione rispettivamente 40 e 67; `ReadWorkbook` arriva a 63 e RuntimePolicy.Read a 52. Questi numeri sono indicatori sintattici definiti nel tool, non misure Sonar. Un nuovo tipo non riconosciuto ricade nel solo filtro tenant.
- **Impatto:** revisione di autorizzazioni e invarianti difficile; aggiungere una nuova entità o workflow richiede modificare un dispatcher centrale. Il fallback è un rischio di evoluzione, **non prova di un IDOR attuale**.
- **Soluzione consigliata:** estrarre predicati nominati e policy pure già coerenti con Domain; mantenere il controllo finale di owner e capability tecnico vicino al DbContext. Rendere espliciti i tipi supportati e il rifiuto degli altri. Non introdurre automaticamente una nuova interfaccia per ogni helper.
- **Test necessari:** matrice ruoli multi-ruolo × operazione × owner, entità non prevista rifiutata, mutation test mirati su guard tenant/worker e parità dei predicati SQL.
- **Rischio della modifica:** alto; è codice che protegge la sicurezza dei dati. Miglioramento architetturale da eseguire per slice, non requisito di riscrittura prima del rilascio.

### F10 — Low — Diagnostica degli errori insufficientemente discriminante

- **File/metodo:** `Api/ExceptionHandling/GlobalExceptionHandler.cs`, `TryHandleAsync`; `Api/Operations/OperationalPipeline.cs`, `AddOperationalServices`.
- **Evidenza:** il logger dell'exception middleware è disabilitato e il gestore registra solo il nome del tipo per i 500. Un errore SQL perde numero/SQLSTATE e stack; la readiness cattura ogni eccezione e restituisce 503 senza un motivo operativo interno distinto. La correlazione identifica la richiesta, ma non la causa.
- **Impatto:** migrazione mancante, deadlock, timeout e bug vengono confusi in produzione; recupero e diagnosi più lenti. La redazione di query/token/password è corretta e va preservata.
- **Soluzione consigliata:** codici di errore interni e metadati sicuri (fase, numero provider, fingerprint dello stack) con sink controllato; nessuna esposizione al client di SQL o messaggi sensibili.
- **Test necessari:** errore sintetico di schema/timeout/deadlock distinguibile nei log, test di assenza di token, password, body e parametri SQL.
- **Rischio della modifica:** medio per riservatezza; richiede allowlist dei campi. Opzionale rispetto ai blocchi di release.

### F11 — Medium — Test di concorrenza possono passare senza dimostrare l'arrivo al lock

- **File/metodo:** `tests/Integration/InstitutionStaffAtomicityTests.cs`, `StaffRole_RevocationWaitsForAuthorizedWriteAndThenBlocksOldToken`; `InstitutionCalendarConcurrencyTests.cs`, `Calendar_IamRevocationWaitsForAuthorizedCommitThenRejectsOldToken`; `InstitutionBulkImportAtomicityTests.cs`, `BulkImport_StaffRevocationWaitsForCommitAndThenBlocksOldToken`.
- **Evidenza:** dopo aver aperto la scrittura, il test attende una durata e verifica `!revoke.IsCompleted`, poi rilascia la barriera e attende il successo. Una revoca non ancora schedulata o ancora nella pipeline HTTP soddisfa la stessa asserzione senza aver tentato il lock SQL. Il confronto finale prova gli esiti, ma non sempre l'ordine bloccante desiderato.
- **Impatto:** falsi positivi della proprietà di serializzazione sotto runner lento; la race F02 non è esclusa dai test Staff e non emerge dalla sola conta dei Passed.
- **Soluzione consigliata:** handshake deterministico del secondo concorrente subito prima del comando contendibile, timeout soltanto come watchdog e osservazione esplicita dell'ordine dei commit. Evitare che il test si limiti a replicare la condizione della funzione sotto test.
- **Test necessari:** il test deve fallire rimuovendo il lock IAM o spostando la rivalidazione prima del lock; esecuzioni ripetute con scheduler rallentato e suite per gli altri ruoli.
- **Rischio della modifica:** medio, limitato all'harness; observer troppo invasivi possono alterare l'interleaving. Priorità alta per rendere probante la chiusura di F02.

### F12 — Low — Transazione autosave locale senza rilascio esplicito

- **File/metodo:** `Infrastructure/Repositories/ExamSubmissionRepository.cs`, `SaveAnswersAsync` (riga 178).
- **Evidenza:** quando non esiste una transazione ambientale, `tx` viene creata con BeginTransactionAsync e riceve CommitAsync/RollbackAsync, ma non DisposeAsync, `await using` o un finally di rilascio.
- **Impatto:** ownership e rilascio della risorsa dipendono dal provider e dal successivo smaltimento del DbContext; non è stato dimostrato un esaurimento del pool. È distinto dalla correttezza del commit, che viene gestito.
- **Soluzione consigliata:** `await using` della sola transazione posseduta dal metodo, preservando l'ownership di quella ambientale e cleanup non cancellabile.
- **Test necessari:** autosave ripetuti nello stesso scope, revision vecchia, deadline superata ed eccezione; verificare che non resti una transazione attiva e che la connessione sia riutilizzabile.
- **Rischio della modifica:** basso; non smaltire la transazione del chiamante. Miglioramento opzionale.

## Architettura, SOLID e pulizia del legacy

Il grafo csproj è coerente: Domain senza riferimenti di progetto; Business→Domain; Application→Business/Domain; Infrastructure→Domain; Api come composition root dei quattro. Non emerge un ciclo. Nullable è abilitato nei cinque progetti. Il guard conferma organizzazione e dipendenze, non SRP o correttezza autorizzativa.

Le separazioni account/membership/profilo e calendario/piano/corso sono motivate dal dominio. Factory bound e capability opaca `SubmissionExpiryWork` sono astrazioni utili: non sono state classificate come debito solo per il numero di interfacce. La costruzione manuale degli engine in `InstitutionExamManager.Open` è una composition locale comprensibile, ma aumenta il numero di dipendenze e lascia contratti privi di ct. Il dispatcher F09 e repository molto estesi (FormativePlan, Email, Calendar e Onboarding) restano i principali punti di accoppiamento. Spostare ogni lock in Application peggiorerebbe la protezione transazionale; le policy pure e l'I/O vanno separati con criterio.

I 7 gruppi di duplicati di token includono forwarding CRUD di controller, `Enabled` Course/Exam, `Description` in FormativePlanRules/InstitutionAcademicRules, `Access` in Calendar/FormativePlan e validazione Page in Email/FormativePlan. Non sono 7 bug: i wrapper CRUD possono restare separati. La duplicazione delle verifiche autorizzative va confrontata semanticamente prima di consolidarla; F02 mostra che un conteggio di cloni non scopre da solo le asimmetrie importanti.

I controller globali ritirati sono assenti; DI applicativa non registra più il vecchio percorso e Program sceglie InstitutionAccess. Non sono stati trovati i simboli legacy elencati nella verifica indipendente. `InstitutionEndpointGate` è globale MVC e nega gli ingressi non classificati; la corsia identity è intenzionalmente distinta per login/account. Il guard non copre automaticamente nuovi minimal endpoint: gli endpoint operativi sono definiti esplicitamente.

EF `User`, `Student`, `Teacher`, le colonne storiche, i link e le migrazioni/backfill **non sono codice morto solo perché storici**. Registrazione scrive ancora `user.role='student'` per soddisfare la colonna storica, senza concedere membership. Le prove di dismissione di route con quattro chiamanti sono nelle TRX storiche; non sono state rieseguite qui. Non si certifica l'assenza di consumatori esterni al repository.

## Sicurezza: verifiche positive e confini non dimostrati

- JWT: issuer/audience distinti Selection/Access, purpose e typ, HS256 allowlist, scadenza obbligatoria, skew zero, duplicati JSON rifiutati, claim globali role/student/teacher respinti; generation identità verificata live. Non è stato trovato un algoritmo alternativo accettato nel codice letto.
- IAM: `MembershipAuthorizationRepository` legge in un solo statement no-tracking membership, istituzione, ruoli e generation. `MembershipAuthorizationRules.Matches` è condiviso. Il gate HTTP non sostituisce il controllo manager; i manager canonici rivalidano. La finestra transazionale F02 resta distinta.
- Tenant/ownership: repository esami costruiti dalla factory verificano il contesto corrente; query Exam/Submission/Grade applicano owner e predicati di profilo/assegnazione. Non è stato identificato un IDOR cross-tenant riproducibile dal solo codice esaminato. Le opzioni unbound dei repository restano interne e disponibili alle fixture: non risultano registrate come corsia HTTP di produzione.
- Account: cambio password richiede password corrente e generation e usa update condizionale dell'hash sotto lock; incrementa generation e scrive audit nella transazione. L'acquisizione storica esige admin corrente, ownership, revisione attesa e legame storico sullo stesso user; mismatch produce `requires-review`, non un link basato sulla sola email. Il ledger ha hash di richiesta, FK composite e trigger append-only.
- Microsoft: `MicrosoftIdentityLink` usa tid/oid e binding esplicito, non email o preferred_username. PlatformAuthority verifica azione, operatore/chiave, finestre temporali, RSA-PSS con chiave almeno 3072, revoche e unicità delle identità delle chiavi; non converte ruoli utente in autorità platform. La persistenza nonce/idempotenza dipende dai repository di governance, non dalla sola firma.

Nessun Critical confermato. Non sono state eseguite prove offensive sul server, rotazioni reali di segreti o modifiche IAM. Le assenze di exploit osservati non certificano tutti i rami o consumer.

## Persistenza, migrazioni e integrità

Le nuove SQL `migrations/identity-account/` sono additive: due tabelle identità e ledger acquisizione, FK RESTRICT, trigger SIGNAL/append-only e controlli espliciti. Non si affidano a CHECK, che non offrirebbe la stessa garanzia nel target 5.7. La sintassi letta non richiede CTE, window functions o SKIP LOCKED. La compatibilità eseguita con MySQL 5.7.24 è attestata dalla TRX storica verificata, non da un nuovo run SQL. Le DDL MySQL non devono essere considerate transazionali; script apply-once richiedono preflight e recupero dopo applicazione parziale.

I lock Exam/Submission, revisione autosave e idempotenza del submit sono presenti; `TransactionManager` fa rollback con token indipendente dalla cancellazione. Membership mutation usa mutex tenant-first e ordine deterministico delle membership. Questi lock serializzano anche operazioni indipendenti nello stesso tenant: occorre misurare contesa prima di ridurli, non eliminarli perché apparentemente costosi. Non è stato misurato né escluso un deadlock di carico; non è stato trovato un retry generale in TransactionManager. Un retry indiscriminato di callback con effetti esterni sarebbe scorretto.

Il worker seleziona batch limitati 1..50 con keyset deadline/id, risolve nuovamente owner e crea repository tecnici bound. La holding storica è confinata dal marker persistito e dallo stato provisioning; non viene dedotta dall'utente o convertita in tenant interattivo. È una corsia di manutenzione supportata, non il vecchio percorso autorizzativo pubblico.

Le query di directory proiettano DTO e ruoli con AsSingleQuery; il caricamento studenti per set e dizionari evita lookup per elemento nei metodi esaminati. Non è stato dimostrato un N+1 sistematico. La query fan-out F07 e il controllo Grade ufficiale con AnyAsync dentro il ciclo di `SaveAsync` meritano un budget query su batch: il secondo non autorizza ad affermare un N+1 generalizzato senza misurazione. Indici composite di tenant e FK sono presenti nei mapping/migrazioni; la loro selettività e gli execution plan su dati reali non sono stati misurati. Nessun risultato EXPLAIN è inventato.

## Qualità C# e test

Il codice usa prevalentemente async e `await using` per comandi/reader/transazioni; la lacuna di ownership del tx autosave è descritta in F12. `ExamManager` accetta un timeProvider non-nullable con default `null!` e usa ancora DateTime.UtcNow in più workflow: la factory corrente lo fornisce, quindi non è un crash di produzione dimostrato. La testabilità temporale non è uniforme e va tenuta presente nel valutare i test di scadenza.

I 10 warning nullable sono nei test (`OpenScoreDoubleCountTests` e `Phase2B_AdditionalTests`), non nei cinque layer applicativi. Non vanno soppressi per ottenere una build artificiosamente pulita. GlobalExceptionHandler uniforma opportunamente gli errori HTTP; F10 descrive il costo diagnostico della redazione attuale. Le whitelist di body e le validazioni input sono presenti, senza che questo dimostri un limite di complessità per ogni payload annidato.

Il cutover cambia realmente i trasporti dei test: `FixtureTenant` legge membership/ruoli/generation persistiti e i client chiamano IInstitutionExamManager. Non è un fake che restituisce sempre successo. Tuttavia quei test non attraversano firma JWT, model binding, gate MVC o challenge HTTP: sono prove applicative/SQL, da sommare alle prove HTTP, non da chiamare tutte end-to-end. Alcuni test dipendono da SeedData, nomi CLR/ActivatorUtilities e accessor mutabile di fixture; refactoring interni possono romperli senza regressione funzionale.

Nel diff esaminato, il cambio UnauthorizedAccessException→KeyNotFoundException per risorsa estranea è coerente con l'occultamento dell'owner; gli snapshot e le asserzioni del lifecycle non sono stati sostituiti tutti con errori generici. Il confronto storico elenca rinomine e nuovi casi e le hash TRX sono autentiche rispetto al riepilogo. Questo sostiene la continuità dei workflow, ma **non dimostra equivalenza completa né copertura sostanziale percentuale**: manca una baseline SQL verde pre-refactoring verificata in questo audit, e le prove F02/F11 restano necessarie. Il caso `Student_CannotSeeCorrectOptions` include anche un controllo positivo docente: riduce il rischio di un test vacuamente verde su lista vuota.

Non è stata misurata coverage: Unit non include un collector di coverage nel progetto letto. Restano da aggiungere test mirati a paginazione con parità/overflow, readiness con schema incompleto, rate limit PUT e revoche concorrenti degli altri ruoli. Sono invarianti specifiche del codice attuale, non un invito ad aumentare indiscriminatamente il numero di test.

## Prontezza operativa

RuntimePolicy rifiuta configurazioni incomplete, root SQL, TLS diverso da VerifyFull, AllowedHosts wildcard e forwarding globale; LocalTest è vincolato a loopback, porta/database dedicati. CORS usa origini esplicite e non abilita credentials indiscriminatamente. ForwardedHeaders usa proxy noti con limite 1; HTTPS/HSTS sono attivi nei modi non locali. Va provato il listener reale dietro il proxy della release: i test locali non certificano quella topologia. Il valore `Deployment:RolloutApproved` è un flag amministrativo, non un sistema di approvazione esterno.

I segreti arrivano da configurazione runtime/key-per-file e appsettings non vengono pubblicati dal progetto Api. Non sono stati stampati o cercati valori segreti negli archivi privati: nessuna secret scan completa è dichiarata. L'immagine usa utente non root e staging di build separato. Docker non è disponibile nel PATH, quindi immagine e avvio container non sono stati eseguiti; i tag base `10.0` sono mobili e non attestano da soli la provenienza della singola release.

`infra/Dockerfile` e `.dockerignore` includono soltanto src e props/global.json; il csproj Infrastructure incorpora invece SQL esterne da `migrations/mt1` e `migrations/mt3`. Nel contesto container quelle risorse non sono incluse. Program non esegue quei migratori all'avvio, quindi non è dimostrato un errore dell'API su DB già provisionato; **non si deve però attribuire al container la stessa capacità di migrazione dell'assembly compilato dalla root**. Verificare risorse nel publish e produrre un artefatto migratore separato se richiesto dal runbook. Nessuna migrazione automatica o infrastruttura esterna è stata avviata.

Il bootstrap consolidato e le nuove additive devono essere parte dello stesso piano di release certificato: F01 e F04 impediscono di dedurre questa chiusura dalla sola build. Verificare restore locked online e dependency gate dopo F03, poi eseguire la regressione SQL/HTTP su nuova istanza solo nel ciclo autorizzato di remediation.

## Valutazione severa in trentesimi

Ogni asse è espresso su 30; il voto complessivo è la media aritmetica, non una misura automatica né una probabilità di sicurezza.

| Asse | Voto | Motivazione |
|---|---:|---|
| Architettura | 24/30 | Grafo corretto, confini istituzionali effettivi e factory motivate; dispatcher di policy e repository molto coesi solo per macro-area |
| Sicurezza | 21/30 | JWT e tenant binding solidi nei percorsi letti; revoca concorrente asimmetrica, advisory TLS e limite credenziali incompleto |
| Manutenibilità | 21/30 | Contratti chiari in molte slice; hotspot di complessità, ct assente su esami, diagnostica ridotta e alcune convenzioni fixture fragili |
| Persistenza | 22/30 | Lock, revision, ledger e FK reali; paginazione difettosa, query fan-out e prestazioni/contesa non misurate |
| Affidabilità e rilascio | 17/30 | Unit verificati e TRX Integration autentiche; manifest non coerente, schema non verificato dalla readiness e immagine non validata |
| **Complessivo** | **21/30** | Media 21/30; qualità discreta, chiusura operativa non raggiunta |

**Ordine di uscita:** chiudere F01 e F03 nel gate di release; riprodurre e risolvere/contrattualizzare F02 con prove deterministiche; rendere obbligatorio il controllo schema di F04. F05–F12 sono remediation mirate, con priorità ai difetti funzionali e alle prove di sicurezza; complessità, duplicati di forwarding, logging e contratti temporali non giustificano da soli una riscrittura o un blocco indefinito. Nessuna approvazione al deployment è implicita nel presente audit.

## Remediation successiva — 30 ottobre 2026

Il [report di remediation](CODE_QUALITY_REMEDIATION.md) documenta implementazione, prove e rischi per ogni finding. F01, F02, F04, F05, F06, F07, F08, F10, F11 e F12 risultano FIXED nel perimetro verificato. F03 è MITIGATED: driver 2.6.1 e audit online corretti, ma resta aperta la prova TLS moderna positiva sul build SQL destinato alla release. F09 è MITIGATED: fallback generico chiuso, dispatcher e complessità residua conservati senza riscrivere l'engine.

Le race IAM sono state riprodotte prima della correzione. Le prove successive comprendono entrambi gli ordini di commit, generation globale, worker, revoca ruolo/sospensione e un mutante privo del lock. La readiness distingue schema incompleto e completo sul Program reale. Il manifest storico resta invariato; esiste una chiusura di release separata.

Questa sezione non assegna un nuovo voto e non converte la remediation in audit indipendente. Il runtime Docker e la topologia TLS di release richiedono ancora verifica nell'ambiente appropriato; non è stato eseguito deployment.
