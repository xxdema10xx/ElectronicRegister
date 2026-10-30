# Refactoring delle responsabilita - COMPLETED

Chiusura del perimetro supportato il 30 ottobre 2026. Questa sezione e le evidenze `validation/current-cutover/final` sostituiscono tutti i checkpoint storici sotto. Il working tree iniziale era modificato: le modifiche precedenti sono preservate. Nessun commit, push, reset o clean.

## Risultato consegnato

Il backend usa un solo percorso applicativo istituzionale: identita/Selection, membership/InstitutionAccess e TenantContext verificato. Exam/Grade/Submission mantengono gli engine condivisi; i controller globali, gli adapter ClaimsContext/UserRole e le relative DI sono rimossi. Il default JWT di produzione e InstitutionAccess, senza un handler globale alternativo. InstitutionEndpointGate conserva i controlli fail-closed su marker, policy, flag, contesto e write marker, e nega gli ingressi MVC non classificati.

Sono rimossi **83 sorgenti** in totale: i 60 del checkpoint precedente e altri 23. Gli ultimi sono ExamController, GradeController, ApiControllerBase; IExamManager, IExamSubmissionManager, IGradeManager; ClaimsContext, UserRole e la proiezione Domain User; IUserRepository/UserRepository/RoleMapper; ILegacyAccess/LegacyAccess/LegacyIdentityOptions/LegacyEndpointGate; IExamSubmissionProcessor/ILegacySubmissionBoundary; ITeacherRepository/TeacherRepository; ReportViolationDto, FormativeBinding e ItsClass. [Elenco verificato dei percorsi](validation/retired-components.txt): tutti distinti e assenti. Entita EF, colonne e dati storici non sono eliminati.

Il codice finale contiene **659 sorgenti backend**, 656 tipi classificati e 28 controller. [Matrice finale](LEGACY_RETIREMENT_MATRIX.json) e [inventario](RESPONSIBILITY_REFACTORING_INVENTORY.json) includono responsabilita, destinazione, azione, motivazione, chiamanti, test e stato. Le decisioni distinguono i componenti eliminati dagli engine e dai contratti di persistenza ancora necessari; nessuna eliminazione dipende dal solo conteggio di riferimenti.

## Le 21 incompatibilita risolte

Le evidenze dell'esperimento 93/114 sono state recuperate, non cancellate. Tutte le 21 identita di test originariamente fallite compaiono Passed nella TRX finale: [riconciliazione nominativa](validation/current-cutover/final/resolved-21-cases.json).

| Causa | Casi | Correzione e invariante conservata |
|---|---:|---|
| Ownership e contratto HTTP di occultamento | 12 | Otto varianti lifecycle e quattro varianti review/list/finalize ora verificano 404 per risorse non possedute, assenza dai risultati e snapshot invariato. Le richieste anonime e i ruoli senza capability restano distinti. |
| Query voti filtrate | 2 | La pagina richiesta per uno studente estraneo e vuota; voto, persone nei filtri e risorse altrui restano invisibili. Nessuna asserzione sostituita con un generico errore anticipato. |
| Principal e profili della fixture | 2 | Membership/ruoli/profili sono persistiti esplicitamente; il caso profilo mancante rimuove davvero il profilo e lo ripristina in finally. Lo studente estraneo e un account distinto realmente appartenente alla fixture. |
| Visibilita della bozza | 1 | Lo studente non scopre un exam draft: 404 e nessuna submission creata; le altre condizioni temporali/stato continuano a verificare il conflitto del workflow. |
| Dati e FK composite | 1 | Owner esplicito prima del salvataggio, anche per le risorse aggiuntive del retake. Nessuna disabilitazione dei vincoli SQL. |
| Scadenze e dispatch | 3 | Eliminato l'invio del lavoro canonico alla lane/processor legacy; scadenza proprietario e worker tecnico usano i confini canonici reali. Concorrenza e retry restano verificati. |

StudyPath e Student hanno un owner shadow con TemporaryGuidValueGenerator per le chiavi alternate. La fixture deve assegnare esplicitamente il proprio owner e impostare IsTemporary=false: controllare soltanto Guid.Empty non basta. I genitori sono salvati nell'ordine delle FK. La nuova fondazione sintetica, i suoi account e i suoi ruoli sono un manifest esplicito di test; nessuna deduzione da user.role. Non sono stati reparentati dati di altre istanze o della holding precedente.

I client FixtureExamClient/FixtureSubmissionClient sono soltanto strumenti di test: leggono IAM persistito e invocano IInstitutionExamManager, che rivalida autorizzazione e profili. Non sono adapter applicativi pubblicati. FixtureExpiryClient usa store e servizio tecnico reali. Il gruppo HTTP e passato114/114; il gruppo di client canonici171/171; worker/sicurezza235/235, prima della regressione completa finale.

## Consolidamenti effettivi e revisione semantica

| Layer/confine | Decisione verificata |
|---|---|
| Domain | TenantContext/InstitutionRole e policy pure restano canonici; ExamPrincipal richiede il contesto istituzionale completo, senza conversione o proiezione globale. Gli ID anagrafici non conferiscono membership. Port e DTO vivi restano contratti; tre proiezioni/DTO inutilizzati sono eliminati. Runtime callback gia spostati dietro port Infrastructure nel gruppo precedente. |
| Business | Un solo grading engine e una sola GradeRules per scala/voto ufficiale. ExamService conserva regole di lifecycle, domande e retake; GradeService conserva valore/coorte/ownership. Rimosso il lookup grade-existence morto. SubjectService conserva solo la precondizione di assegnazione valida usata dagli esami; vecchie regole CRUD/name/delete e dipendenza Subject inutilizzate eliminate. Nessuna nuova interfaccia architetturale. |
| Application | IInstitutionExamManager orchestra capability, profili e factory bound, riusando ExamManager/ExamSubmissionManager/GradeManager. Overload e tre interfacce globali eliminati; submit facade morto rimosso. GradeManager non contiene piu le tre query facciata globali per studente/nome/data. Account globale, membership e anagrafiche rimangono responsabilita separate. |
| Infrastructure | Repository esami/voti/submission sono creati dai factory owner-bound, compreso il work tecnico. Tolte le DI unbound di otto repository e dei tre servizi non usate dalla composizione canonica. StudentRepository ha due letture, SubjectRepository la proiezione per ID set, ClassSubjectRepository tre lookup: eliminati CRUD/enumerazioni/Exists globali morti. TeacherRepository/interfaccia sono eliminati. I lock e le verifiche transazionali vicine ai dati restano dove proteggono ownership, revision, audit e atomicita. |
| Api | Controller istituzionali delegano ai casi d'uso; JWT purpose/audience/generation e IAM live restano necessari. 30 azioni Exam/Grade globali ritirate oltre alle43 precedenti. Nessun controller base o gate interpreta ruoli globali. Gli ingressi senza marker falliscono chiusi. |
| Persistenza storica | EF User/Student/Teacher, password hash, link, membership, profili, iscrizioni, submission, voti e audit conservati. User.role e gli altri legami storici non sono un'autorita applicativa. Nessuna modifica distruttiva dello schema. |
| Verifica dei consumer | Riferimenti statici e DI sono confrontati con controller/policy/route/OpenAPI realmente scoperti, bootstrap reale, test HTTP e call path dei factory. I tipi candidati senza consumer sono stati controllati anche contro test e reflection mantenuta. L'inventario lessicale resta dichiaratamente un supporto, non una prova formale di qualsiasi consumer dinamico esterno. |

La revisione del set residuo di componenti legacy e delle responsabilita dei cinque layer e conclusa per i workflow supportati. Motivi e obblighi per ogni componente condiviso sono nella matrice finale. Non sono stati accorpati catalogo, calendario, piani, CourseEdition, Enrollment o Biennium come se fossero lo stesso aggregate.

## Worker tecnico e storico

Una sola pipeline seleziona chiavi con scansione keyset limitata, risolve di nuovo l'owner, crea SubmissionExpiryWork opaco e repository bound, quindi usa lo stesso processor/transazione/row lock di grading. Nessun caller nullo, UserRole o InstitutionRole inventato per il worker. La dispatch crea uno scope per item, gestisce errori isolati, cancellation e retry senza starvation; audit tecnico con ActorUserId null e risorsa verificata.

Per istituzioni ordinarie sono ammessi active/suspended senza marker holding. La sola holding persistita puo essere elaborata con marker mt3a_legacy_scope e stato provisioning: e un confinamento tecnico, non un tenant utente attivato. Stato active sulla holding non la rende elaborabile. Il test dedicato verifica owner immutato, nessun profilo creato, rifiuto delle API studente/docente, rifiuto owner estraneo, dispatch concorrenti e replay con un solo voto e audit. Nessuna migrazione inventa l'istituzione di una submission storica.

Il rollout del timer usa `Multitenancy:InstitutionWorker:Enabled`; `Identity:Legacy:*` non riattiva API o worker alternativi. La configurazione di esempio non propone piu il ramo legacy.

## Contratti per il nuovo frontend

Account personale, password/revoca e acquisizione gia completati restano canonici. Catalogo/classi/anagrafiche, CourseEdition/Enrollment, piani, calendario, import, IAM/governance/audit restano disponibili ai confini istituzionali esistenti. Exam/Grade/Submission coprono lifecycle e retake, assegnazione/eligibilita, sessione, ordine domande, autosave revisionato, submit/replay, scadenza, review/finalize/override, voto ufficiale, filtri/paginazione/statistiche e violazioni.

Aggiunto GET `/api/institution/submissions/{id}/work`, solo Student proprietario: usa lo stesso mapper di redazione per risposte/bozze e nasconde punteggi provvisori, correttezza e commenti docente. Non apre GET/PUT review o finalize allo studente. Payload positivo e rifiuto delle risorse estranee sono verificati in HTTP.

Gli ingressi `/api/Exam` e `/api/Grade` sono assenti per tutti i30 metodi/route precedenti, anche con token legacy firmato, Selection e InstitutionAccess. Un JWT globale firmato e rifiutato dalle API istituzionali. Contratti dettagliati: [cutover](INSTITUTIONAL_API_AND_LEGACY_CUTOVER.md), [indice generato dai sorgenti correnti](INSTITUTIONAL_API_SURFACE.json), e OpenAPI/route matrix verificati dal bootstrap reale.

## Evidenze finali e differenze dei test

Restore locked e build completa riusciti:0errori,10warning test preesistenti. Unit **590/590**, Integration SQL/HTTP **1458/1458**, zero Failed/Skipped. Guard Roslyn e dipendenze:659sorgenti,656tipi,0violazioni. Quaranta scaffold EF identici a HEAD. git diff --check:exit0. Nessun progetto Infrastructure separato: le prove di persistenza sono nella suite Integration completa.

Discovery e TRX coincidono per nomi e molteplicita case-sensitive. Rispetto alla baseline storica585/1431: +5Unit e +27Integration. Rispetto al checkpoint iniziale di questo incarico590/1456: due nuovi fatti Integration (matrice30route/JWT globale; holding tecnica concorrente), nessun test perso o disabilitato. Sei identita Unit rinominate conservano ordinali/ruoli e verificano ora l'assenza del contratto globale ritirato. Le20 rinomine Integration della fase precedente restano spiegate e verificate. I test unitari di grading/rollback chiamano il core interno condiviso, senza aprire un nuovo ingresso applicativo; i test del boundary rifiutano repository non corrispondenti.

[Evidenze e TRX finali](validation/current-cutover/final/README.md), [confronto casi](validation/current-cutover/final/case-comparison.json). Dopo l'ultima rinomina dei soli test Unit, i cinque binari di produzione risultano identici a quelli della regressione Integration; gli Unit sono rieseguiti verdi.

MySQL reale5.7.24, nuova porta13438 e datadir `.staff-private/instance-f838f9b756714fae9968ae7728716e97/data`. Identita SQL verificata prima di provisioning/binder e arresto; bootstrap e due migrazioni additive una sola volta. Anche l'istanza diagnostica13437 e stata arrestata dopo verifica SQL. Nessuna istanza preesistente usata o modificata. ExecutionPolicy RemoteSigned senza bypass; NuGet usa configurazione accessibile nella workspace e APPDATA del solo processo. Nessuna autorizzazione Windows aggiuntiva richiesta.

## Limiti espliciti del perimetro

Il recupero password e il cambio email globali non sono esposti senza un canale verificato del destinatario e il relativo workflow di revoca. InstitutionAdmin non controlla credenziali globali; PlatformOperator non eredita privilegi didattici o reset inventati. L'acquisizione ambigua resta requires-review senza collegamenti automatici; non si inventano mapping dalla holding. Questi confini sicuri restano intenzionali, non pipeline legacy o TODO implementativi mascherati.

Lo schema account/acquisizione additivo deve essere applicato con il processo di distribuzione autorizzato prima dell'uso del codice su un altro ambiente. Questo incarico non ha applicato migrazioni a database preesistenti. Le DDL MySQL5.7 non sono trattate come transazionali.

## Checkpoint storici (non descrivono il codice finale)

## Checkpoint di implementazione successivo alla proposta — 29 ottobre 2026

**Stato corrente: RUNTIME-CONFIG implementato; guard fixture/startup corretti; mappa ampliata; dismissione legacy e verifica SQL finale ancora aperte.** Questa sezione prevale sui checkpoint storici seguenti. Il precedente verde 587/587 e 1420/1420 resta la baseline accettata del codice precedente a queste modifiche; non certifica il codice finale.

La proposta di riferimento è stata eseguita per le parti dimostrabili senza cutover o modifica dei dati. [Contratti, mappa semantica per workflow e condizioni di dismissione](INSTITUTIONAL_API_AND_LEGACY_CUTOVER.md), [indice API](INSTITUTIONAL_API_SURFACE.json) e [matrice dei componenti legacy](LEGACY_RETIREMENT_MATRIX.json) sono artefatti di questo intervento, non una nuova proposta generale.

### Risultato applicativo

- I callback CalendarFeature, FormativePlanFeature e InstitutionImportFeature sono usciti dal Domain. Quattro porte Domain separano disponibilità corrente e impostazioni pure. Infrastructure possiede l'implementazione comune RuntimeFeatureAvailability, i due adapter DI nominati e InstitutionImportRuntimeFeature. I sei manager/repository sono migrati; i controlli ai punti di accesso/commit e sotto lock conservano posizione e frequenza. Nessun caching della disponibilità, nessuna chiave o default rollout cambiati.
- InstitutionImportFeature conserva limiti, snapshot e Validate. La disponibilità corrente viene letta dalla porta runtime, mentre lo snapshot serializzato resta puro. I DTO HTTP, gli attributi input Exam, gli enum e i contratti delle route non cambiano. Cambiano i costruttori CLR interni e la composizione DI: gli eventuali consumer .NET esterni devono migrare alle porte.
- Rimossi i due record callback dal Domain, sostituiti dalle porte, e il file storico `src/Infrastructure/ElectronicRegisterAPI/Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs`: conteneva solo whitespace, nessun tipo o registrazione, ed era esterno al progetto Infrastructure. Nessun altro componente viene dichiarato morto su sola assenza di riferimenti lessicali.
- Il test startup Production ora invoca il vero Program con configurazione sintetica deliberatamente incompleta, senza leggere TEST_MYSQL_CONNECTION. Richiede TargetInvocationException da reflection e InvalidOperationException interna con messaggio esatto e stack RuntimePolicy.Read. La prima prova ha rilevato il wrapper reflection ed è stata corretta mantenendo l'asserzione stretta.
- MT4A verifica disponibilità prima delle aperture SQL e prima del try/finally del writer legacy; il preflight verifica disponibilità prima dell'host. Tre nuove prove invocano direttamente questi percorsi con fixture mai inizializzata e richiedono il fallimento della guardia, senza stack del connettore o ProgramHost.

### Copertura e residui semantici

L'inventario passa da 719 a **724 sorgenti**: tre file eliminati, otto nuovi file applicativi. Registra hash correnti, layer, slice, tipi, consumatori lessicali e indicatori di lock/audit. La matrice enumera **80 componenti/interfacce legacy**. L'indice API copre **35 controller**. L'analisi distingue confini sostituibili, engine condivisi, account/credenziali e dati storici. Il grafo runtime è DI → adapter Infrastructure → porte Domain, con Application e repository consumatori; Infrastructure continua a dipendere solo da Domain.

Rimangono aperti l'esame esaustivo metodo per metodo di tutte le classificazioni Keep e la verifica di consumer esterni/dinamici. L'inventario lessicale non viene presentato come prova semantica esaustiva. Sono identificati come consumer dinamici reali worker, DI, provisioning PlatformAdmin e loader dei canali owner-installed. Non sono stati dismessi.

Le lacune che impediscono il ritiro immediato dell'autenticazione legacy sono **account personale completo, cambio password, amministrazione account/credenziali e acquisizione account**. Membership e inviti non equivalgono a questi casi d'uso. Il nuovo contratto li separa dall'amministrazione tenant. I filtri voto sono già disponibili nella pagina istituzionale con subjectId/studentId/date; non richiedono una seconda pipeline. Le proiezioni del catalogo legacy richiedono decisioni esplicite di copertura. UserRole non viene convertito in membership. Hash password, link identity e storico restano preservati.

### Verifiche effettive del codice finale

| Verifica | Esito |
|---|---|
| Build soluzione slnx, --no-restore | **Pass**, 0 errori, 1 warning nullable Infrastructure nella build finale; 10 warning test nelle precedenti ricompilazioni |
| Unitari completi | **585 Passed, 0 Failed, 0 Skipped**; due casi callback import spostati nella suite Infrastructure perché non appartengono più al Domain |
| Test mirati senza DB | **25 Passed, 0 Failed, 0 Skipped**: disponibilità live/DI fail-closed, serializzazione snapshot, guard fixture, configurazione e vero startup Production |
| Riferimenti progetto/import dei cinque layer | Nessuna dipendenza vietata rilevata nel controllo statico; nessun callback/configuratore runtime in Domain/Configuration |
| git diff --check | **Pass** |
| Guard Roslyn ufficiale | **Bloccato**, non passato: NETSDK1004 per assets assenti; restore rifiutato per accesso a `%APPDATA%/NuGet/NuGet.Config` |
| Nuova istanza SQL isolata | **Bloccata prima dell'ingresso nel runner**: New-InstitutionStaffTestInstance.ps1, porta richiesta 13431, PSSecurityException/ExecutionPolicy. Nessuna istanza preesistente riutilizzata |
| Suite SQL/HTTP finale e prove flag cambiato sotto lock | **Non eseguite**; nessuna dichiarazione di verde finale o equivalenza SQL |

Evidenze: [TRX e hash](validation/completion/test-summary.json), [build](validation/completion/build.log), [architettura e blocchi ambientali](validation/completion/architecture-and-environment.json). Sono conservate anche le TRX dei tentativi intermedi: uno startup fallito per il wrapper reflection; una selezione troppo ampia includeva il test HTTP StrictStagingHonorsOnlyConfiguredProxyAndRedirectsUnencryptedRequests, fallito per TEST_MYSQL_CONNECTION assente prima dell'host. Il filtro finale seleziona esattamente le prove senza DB; quel test HTTP non è dichiarato passato. Nessuna asserzione è stata rimossa per accettare errori ambientali.

Non sono stati aggirati ExecutionPolicy o ACL, eseguiti commit/push, modificati schema SQL o dati storici. Il report finale non estende il vecchio verde alla nuova implementazione. Restano necessari il run completo su nuova istanza autorizzata, il guard Roslyn e la revisione semantica residua prima della certificazione completa.

## Checkpoint storici precedenti all'implementazione

**Stato: regressione SQL/HTTP del codice attuale SUPERATA; refactoring complessivo PARZIALE.**

## Verifica attuale delle TRX host

Il 29 ottobre 2026 sono state lette le TRX effettive in `.staff-private/instance-50d37b8bb5564efb8a125521531ad5e6`, senza riesecuzioni o accesso a MySQL. I contatori XML e tutti i singoli UnitTestResult concordano:

| Suite | Totali / eseguiti / Passed | Failed | Skipped | SHA256 TRX |
|---|---:|---:|---:|---|
| Unit | 587 / 587 / 587 | 0 | 0 | `905AE164994B770C27C4951F7BFDEE6F0CEB8E3248AB9C969C18A6195C85F92D` |
| Integration | 1420 / 1420 / 1420 | 0 | 0 | `36C335D69D08684F81B9AA9D341D77EFA80A1C56959CABBB3F8327EC54196F7C` |

Entrambi i run hanno summary `Completed`; nessun risultato individuale diverso da Passed. I tre casi CLI e le due prove filesystem/ACL sono Passed nel run completo. Il confronto nominativo e delle molteplicità preserva tutti i 1418 casi integration precedenti e rileva soltanto le due nuove prove filesystem. L'ambiente Windows/MySQL 5.7.24 isolato è quello dichiarato dal proprietario dell'host; i risultati TRX sono stati verificati direttamente. Non è stato eseguito un nuovo controllo del server o dei dati.

Evidenza strutturata, tempi e hash: [responsibility-host-regression-verification.json](validation/responsibility-host-regression-verification.json). La regressione SQL/HTTP corrente è quindi superata. Non si dichiara una baseline SQL verde del commit pre-refactoring, equivalenza semantica esaustiva o completamento del refactoring.

Le sezioni successive fino al lavoro residuo conservano la cronologia dei checkpoint precedenti: i loro “non eseguito”, “bloccato” e `partial_not_certified` si riferiscono a quelle esecuzioni e sono superati, per la regressione corrente, dalla verifica sopra. I problemi semantici e architetturali restano aperti. Proposta attuale: [RESPONSIBILITY_COMPLETION_PROPOSAL.md](RESPONSIBILITY_COMPLETION_PROPOSAL.md).

## Cronologia delle verifiche precedenti

## Intervento precedente: isolamento filesystem delle tre fixture CLI

Il proprietario dell'host comunica un run reale MySQL 5.7.24 sulla porta **13429**: **1418 totali, 1415 Passed, 3 Failed, 0 Skipped**. I tre fallimenti sono in Directory.CreateDirectory nei test CLI di onboarding, governance e rifiuto ACL Everyone. Questi dati provengono dal risultato riferito dall'utente; non sono una TRX post-correzione verificata da Codex. La precedente indisponibilità del DB in Codex non viene usata per spiegare questi tre errori host.

### Diagnosi e modifica delle sole fixture

I tre test duplicavano la costruzione di una cartella sotto `migrations/mt4/private/mt5a-mamp-13306`. `PrivateTestFiles` condivideva soltanto ProtectNewDirectory, invocato **dopo** CreateDirectory: non poteva correggere un errore di accesso al parent storico.

Ora `PrivateTestFiles.CreateDirectory` possiede l'intera creazione: individua il workspace dal marker del bootstrap, crea una root **nuova per il processo/run** `.staff-private/cli-fixtures-<guid>` e una sottodirectory **nuova per ogni test** `<tipo>-<guid>`. È separata dalle datadir `instance-*`. Root e directory del test sono protette prima di scrivere configurazioni o intenti; eredità ACL disabilitata, accesso limitato a utente corrente, SYSTEM e gruppo Administrators, come nel precedente helper. Non si richiede esecuzione elevata. Sono rifiutati riuso di directory esistenti, label con componenti di percorso e attraversamento di reparse point.

Migrati esattamente i tre chiamanti:

- `Mt5cOnboardingTests.ActualAdministrativeCliVerifiesSignedIntentAndUsesOnlyOwnerInstalledChannel` → `onboarding-cli-<guid>`.
- `Mt5cOnboardingTests.Governance_ActualCliUsesTheDedicatedManagerWithoutRequiringNotificationDelivery` → `governance-cli-<guid>`.
- `Mt5cOnboardingTests.PlatformCliRefusesAuthorityConfigurationReadableByEveryoneBeforeMutation` → `operator-acl-<guid>`.

La ricerca nei sorgenti integration compilabili, esclusi bin/obj e artefatti TestResults, non trova altri usi scrivibili di `mt5a-mamp-13306`. Resta il suffisso storico in `TestMySqlTarget`, **solo come metadato di validazione del target legacy**, e la stringa negativa nel nuovo test che prova di non usare quel percorso. Gli altri writer di evidenze/backup sono distinti e non sono stati ricollocati in questo intervento. Gli artefatti TestResults già modificati dal run host sono stati preservati.

### Prova Everyone mantenuta e rafforzata

La fixture negativa ora contiene lo stesso target MySQL esplicito e un canale owner-installed valido usati dalla fixture positiva: la precedente omissione di TestDatabase e del provider avrebbe potuto far rifiutare la CLI anche per ragioni estranee alle ACL su porta 13429. Non cambia alcuna logica applicativa della CLI.

Il test aggiunge realmente `Everyone / ReadData` al solo `admin.json` della nuova fixture, verifica la presenza della ACE e mantiene gli assert originali: exit 1, stdout vuoto, nessuna connessione divulgata, nessuna Institution creata. Nel finally ripristina la DACL. Poi **lo stesso intento firmato, target e canale** devono essere accettati con la configurazione privata, con exit 0 e il risultato atteso: un errore generico di configurazione non può più far passare il test negativo. Il nuovo effetto positivo resta confinato all'aggregate sintetico già registrato nel teardown originale del test.

Una prima esecuzione locale delle nuove prove filesystem ha rilevato che riscrivere direttamente l'oggetto FileSecurity appena letto non rimuoveva Everyone in questo ambiente. `RestoreAccessRules` ora imposta esplicitamente la sezione DACL originale in un nuovo FileSecurity prima della scrittura. Può operare soltanto su file interni alla root del run corrente e rifiuta reparse point. Non riscrive owner, ACL di parent o ACL storiche. La prova successiva verifica uguaglianza della DACL/owner originali e invarianza delle ACL del parent.

### Verifiche di questo intervento

| Verifica | Risultato effettivo |
|---|---|
| Build soluzione | **Pass**, 0 errori; 10 warning nullable preesistenti ricompilati nei test |
| Suite unitaria | **587 Passed, 0 Failed, 0 Skipped** |
| Nuovi test filesystem Windows, senza fixture DB | **2 Passed, 0 Failed, 0 Skipped** dopo la correzione del ripristino DACL; prima prova 1 Passed/1 Failed conservata nelle evidenze |
| Tre test CLI/MySQL interessati | **Non eseguiti in Codex**: connessione, porta e datadir della sessione host non sono presenti nelle variabili d'ambiente di Codex |
| Regressione completa dopo la correzione | **Non eseguita**; attesi 1420 casi, includendo i due nuovi test filesystem, totale da verificare dalla TRX |
| Codice applicativo rispetto all'inventario precedente | **719 SHA256 invariati**, nessuna modifica a CLI, Business, HTTP, configurazione autorizzativa o persistenza |
| Database/SQL/ACL storiche | Nessuna modifica; in locale eseguiti soltanto build, unitari e prove filesystem su directory nuove |
| Comandi PowerShell host | Due blocchi verificati sintatticamente; non eseguiti da Codex |

Evidenze e hash delle TRX: [responsibility-cli-fixture-verification.json](validation/responsibility-cli-fixture-verification.json). Comandi mirati: [RESPONSIBILITY_CLI_FIXTURE_HOST_COMMANDS.md](RESPONSIBILITY_CLI_FIXTURE_HOST_COMMANDS.md). Nessun test è stato disabilitato o modificato per accettare UnauthorizedAccessException. Le eccezioni di accesso restano fallimenti.

**Verifica richiesta al proprietario dell'host:** eseguire i tre test e le due prove filesystem con le variabili del target test autorizzato. Solo dopo **3/3 Passed**, eseguire la regressione completa su **una nuova istanza isolata**, con nuova datadir e una porta libera diversa da 3306/13306 e dalla 13429 già utilizzata. Occorrono 0 Failed e 0 Skipped, preservando i 1418 casi precedenti. Fino alla verifica di queste prove lo stato rimane `partial_not_certified`; nessuna certificazione finale, commit o push.

## Ripresa: verifica prioritaria, nuovi refactoring sospesi

Questa sezione prevale sulle precedenti ipotesi ambientali del primo checkpoint. HEAD invariato; nessun commit o push. Le modifiche RUNTIME-CONFIG appena iniziate nella ripresa sono state ritirate prima di proseguire: **tutti i 719 sorgenti applicativi corrispondono byte per byte agli SHA256 dell'inventario precedente**. Non sono state aggiunte modifiche applicative o ai test in questa continuazione di verifica. Le decisioni di cutover legacy e gli ulteriori consolidamenti rimangono sospesi fino alla regressione richiesta.

### Diagnosi esaustiva delle TRX precedenti

Esaminate le singole cause e gli stack, non soltanto i contatori. Sette messaggi distinti corrispondono a otto cause, perché l'eccezione generica compare in due punti differenti. Tutti i **1279 Failed** sono assegnati una sola volta; nessun caso residuo non classificato. Evidenze nominative e hash delle TRX: [responsibility-integration-diagnosis.json](validation/responsibility-integration-diagnosis.json).

| Casi Failed | Evidenza | Causa osservata e limite della diagnosi |
|---:|---|---|
| 1025 | Messaggio esplicito Docker/TEST_MYSQL_CONNECTION; fixture o assert di disponibilità | La fixture non inizializza il contesto; i workflow non partono. Non prova l'assenza di difetti SQL. |
| 94 | `Explicit isolated target required`, inizializzazione MT5B | Variabile di connessione esplicita assente, eccezione prima di SQL. |
| 62 | `Mt5cOnboardingTests.InitializeAsync:65` | L'operatore `?? throw` sulla variabile assente genera l'eccezione generica; non è un errore di onboarding eseguito. |
| 6 | `Mt6RuntimeTests.Start:104-106` | Lo stesso controllo impedisce la creazione dell'host; nessuna richiesta HTTP dei sei workflow viene verificata. |
| 53 | `Mt5eIamHttpTests.InitializeAsync:55` | `Explicit isolated database required`, prima di SQL/HTTP. |
| 36 | `Mt5aMembershipAdminTests.InitializeAsync:60-61` | `Explicit test database required`, prima del bootstrap dell'host. |
| 2 | MT4A `InformationSchemaHasCompositeRestrictConstraints` e `LegacyEfWritersStillReceiveTriggerDerivedOwners` | Eccezione MySQL host non autorizzato. La fixture indisponibile espone `ConnectionString = ""`; il primo metodo apre direttamente il connettore, il secondo arriva alla stessa apertura nel cleanup `finally`, mascherando il fallimento precedente di ScopeFactory. Sono difetti dei guard delle fixture: il connettore tenta un target implicito. Non vengono classificati come semplice indisponibilità Docker né come regressioni funzionali dimostrate. |
| 1 | `Mt3cPreflightTests.DefaultRolloutNeverOpensSchoolRoutesWithoutAuthorization`; `RuntimePolicy.Read:40` | Il test passa la connessione vuota della fixture all'host LocalTest. La configurazione viene correttamente rifiutata prima delle richieste. Manca il guard della fixture; il comportamento delle route non è verificato da questo esito. |

I **57 Skipped** riportano tutti esplicitamente fixture indisponibile per assenza di Docker/TEST_MYSQL_CONNECTION. Le **82 prove Passed** coprono parser, validazioni di configurazione/connessione, worker disabilitati e validazione token; non certificano operazioni MySQL. Il confronto nominativo, inclusa la molteplicità degli esiti, tra TRX baseline e precedente finale rileva **0 differenze**. Questo confronto diagnostico non dimostra equivalenza su un database funzionante.

È stato inoltre individuato un **Passed non probante**: `Mt6RuntimeTests.RealProgramRefusesIncompleteProductionConfigurationAtStartup` usa `ThrowsAnyAsync<Exception>` e chiama Start, che solleva già per l'assenza della variabile di connessione. L'esito verde, anche nel sottoinsieme ripetuto, non dimostra che Program abbia rifiutato la configurazione Production. Non viene conteggiato come prova del confine di startup. I 82 Passed si distribuiscono in 25 casi CSV, 12 XLSX, 20 parser calendario, 7 portabilità/validazione target, 2 worker disabilitati, 2 token Microsoft e 14 runtime (compreso questo caso debole). Anche la qualità di questa asserzione è una verifica residua, senza modifiche ai test nella ripresa.

Non è stata rieseguita la suite completa senza connessione esplicita: riprodurrebbe anche le aperture implicite appena diagnosticate. I due percorsi MT4A e il preflight richiedono una successiva correzione dei guard dei test prima di autorizzare altre esecuzioni complete in assenza del target. In questa verifica non sono stati modificati o disabilitati test per ottenere esiti verdi.

### Runner MySQL e blocco preciso

È presente anche il runner **PowerShell nativo** `tools/New-InstitutionStaffTestInstance.ps1`, sfuggito alla prima analisi. Non richiede Python. Il binario `C:\MAMP\bin\mysql\bin\mysqld.exe --no-defaults --version` risponde **5.7.24**. Il runner rifiuta porte 3306/13306 e porte occupate e crea una datadir nuova con GUID e ACL private sotto `.staff-private`.

Tentativo effettivo: `& .\tools\New-InstitutionStaffTestInstance.ps1 -MySqlBin 'C:\MAMP\bin\mysql\bin' -Port 13429`. Esito: **PSSecurityException, esecuzione degli script disabilitata**, prima dell'ingresso nel corpo. `Get-ExecutionPolicy` restituisce **Restricted**; gli scope espliciti sono Undefined. Nessuna nuova istanza è stata creata. Nessun database o processo preesistente, nessuna porta 3306/13306 e nessuna datadir storica sono stati adottati, interrogati o modificati durante questa ripresa. Il solo `mysqld --version` non avvia un server.

Non è stato usato alcun bypass di ExecutionPolicy, scriptblock ricavato dal runner o modifica a NuGet.Config; nessun interprete installato. Il limite attuale è l'autorizzazione dell'host all'esecuzione dei runner, non la mancanza di Python. La sola presenza dei binari non consente di dichiarare eseguito il bootstrap.

Comandi esatti, senza aggiramenti: [RESPONSIBILITY_INTEGRATION_HOST_COMMANDS.md](RESPONSIBILITY_INTEGRATION_HOST_COMMANDS.md). Preparano il run corrente su nuova porta/datadir e il commit iniziale esportato in una nuova cartella su una seconda istanza distinta. Includono identità processo/listener/SQL, binding dei soli guard di teardown test, credenziali della sola istanza appena creata, ambiente ripristinato, log privati, rifiuto di failure/skipped e confronto delle TRX. I due blocchi PowerShell hanno superato il parsing sintattico; **non sono stati eseguiti sull'host**. Non usare il wrapper `test-local.ps1`, che è vincolato alla 13306.

### Verifiche realmente ripetute

| Verifica della ripresa | Esito |
|---|---|
| SHA256 sorgenti rispetto all'inventario precedente | 719 corrispondenze, 0 differenze |
| Build soluzione `--no-restore` | 0 errori, 1 warning preesistente ricompilato |
| Suite unitaria intera | **587 Passed, 0 Failed, 0 Skipped** |
| Sottoinsieme integration sicuro | **82 Passed, 0 Failed, 0 Skipped**, 38 metodi; selezione esplicita delle 82 prove già Passed nella baseline, filtro conservato localmente |
| Suite HTTP/MySQL completa | **Non eseguita in questa ripresa** |
| Baseline SQL pre-refactoring su istanza funzionante | **Non disponibile/non eseguita** |
| Assenza di regressioni SQL, revoca al commit, transazioni, concorrenza e HTTP con DB | **Non dimostrata** |

Le TRX della ripresa sono in `validation/responsibility-resume-unit` e `validation/responsibility-resume-safe-integration`; gli hash sono nel JSON diagnostico. Il sottoinsieme eseguibile non sostituisce la regressione completa. Restano da eseguire entrambi i run MySQL/HTTP documentati; i difetti dei guard test diagnosticati sono aperti e non sono stati nascosti sotto una generica etichetta ambientale. Stato invariato: `partial_not_certified`.

Non sono soddisfatti tutti i criteri di accettazione. Non viene dichiarato il completamento verificato dell'intero backend. L'inventario strutturale copre i cinque layer; le classificazioni semantiche riguardano i 24 gruppi espliciti del JSON. Non costituiscono una prova esaustiva dell'assenza di codice morto o di altre duplicazioni.

## Baseline effettiva

- Data: 29 ottobre 2026; branch `refactor-database-entities`.
- HEAD iniziale e finale: `f12a0e1ac6b95507bc0ac571600c797fd9ededfc`.
- Working tree iniziale pulita. Nessuna modifica locale preesistente; nessun commit, push, reset, clean o checkout eseguito.
- SDK 10.0.401; soluzione `Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx`.
- Build iniziale `--no-restore`: 0 errori, 11 warning nullable già presenti.
- Baseline unitari nuova: **559 passed, 0 failed, 0 skipped**.
- Baseline integration nuova: **82 passed, 1279 failed, 57 skipped; 1418 totali**.
- La fixture segnala Docker non disponibile su `npipe://./pipe/docker_engine` e `TEST_MYSQL_CONNECTION` non impostata. Nessuna connessione a database è stata configurata o aggiunta durante il lavoro.

I numeri dei report storici non sono stati adottati come baseline. Le TRX di questa sessione sono conservate localmente sotto `docs/refactoring/validation/responsibility-*`; hash, confronto nominativo degli esiti e controlli di preservazione sono in [responsibility-verification.json](validation/responsibility-verification.json).

## Inventario e dipendenze

L'[inventario JSON](RESPONSIBILITY_REFACTORING_INVENTORY.json) contiene percorsi, SHA256 e numero di righe dei **719 sorgenti** attuali, project reference, 116 righe di registrazione DI, 347 attributi lessicali dei controller, riferimenti `UserRole` nel backend e nei sorgenti test/tool. Attributi e registrazioni non equivalgono a discovery HTTP/DI eseguita a runtime.

| Layer | Sorgenti attuali | Esame delle responsabilità |
|---|---:|---|
| Domain | 419 | Membership, capabilities, principal, profili, modelli accademici, policy, DTO e disponibilità runtime |
| Business | 15 | Regole esami/voti, servizi legacy, import, generazione email e builder formativo |
| Application | 41 | Autenticazione, adapter istituzionali, engine condivisi, catalogo, piani, import, calendario e worker |
| Infrastructure | 172 | Repository, scope, ownership, IAM transazionale, persistenza e adapter tecnici |
| Api | 72 | Controller, marker/gate, worker e contratti legacy/istituzionali; nessun sorgente Api modificato |

Le dipendenze restano Domain → nessuno; Business → Domain; Application → Business/Domain; Infrastructure → Domain; Api → tutti. Nessun project reference o registrazione DI modificati. La regola IAM pura ora appartiene al Domain, accessibile sia all'orchestrazione sia alla persistenza senza introdurre Infrastructure → Business. Nessuna nuova interfaccia, factory, mediator o servizio di sola delega.

Consultati la baseline strutturale, README, setup DB, design multi-tenancy, modello CourseEdition/lifecycle, contratti di amministrazione, classi, trasferimento studenti e importazioni; riesaminati i punti di composizione e i percorsi formativi/calendario. I report storici rimangono evidenze dei loro checkpoint e non sono stati riscritti.

## Consolidamenti realizzati

### Membership: un solo predicato di validità

`MembershipAuthorizationRules` è stato spostato da `Business.Services` a `Domain.Authorization`. `IsSelectable` conserva la regola originale. `Matches` ammette un risultato nullable e restituisce false quando manca la membership; richiede identità, tenant, membership, stati attivi, generazione presente/valida e uguaglianza esatta dei ruoli, senza duplicati né valori sconosciuti.

Il predicato è ora usato da InstitutionAuthManager e da otto componenti persistenti: InstitutionWriteAuthorization, InstitutionCalendarRepository, InstitutionEmailRepository, InstitutionFormativePlanRepository, InstitutionImportRepository, InstitutionAdministrationPersistence, InstitutionOnboardingRepository, InstitutionMembershipMutationRepository. Sono rimossi gli otto predicati locali equivalenti ma incompleti in modo non uniforme.

I controlli di InstitutionAdmin restano espliciti dove richiesti. Nessuna capability viene concessa dalla sola corrispondenza della membership. Query, ordine delle chiamate, lock SQL, lock IAM-first e rivalidazioni prima/durante il commit rimangono nei rispettivi workflow.

**Hardening intenzionale:** alcuni predicati precedenti non confrontavano esplicitamente gli identificativi del risultato restituito dal repository oppure non rifiutavano ruoli duplicati. Ora anche questi risultati incoerenti sono respinti. Non è una modifica alla matrice dei privilegi; i 15 insiemi validi di ruoli sono verificati dai nuovi test. L'equivalenza dei workflow persistenti reali richiede comunque la regressione SQL ancora bloccata.

### Esami: eliminazione della proiezione istituzionale in un ruolo globale

Rimosso `ExamPrincipal.VisibilityCaller`, che costruiva un nuovo ClaimsContext traducendo indirettamente un principal istituzionale in UserRole per redigere DTO. SubmissionVisibility ed ExamSubmissionMapper ricevono ora solo `studentView`.

Il core di ExamSubmissionProcessor riceve separatamente `actorUserId` e `studentView`. La submission istituzionale continua a verificare principal Student, tenant e repository bound prima della transazione. L'audit conserva l'identità originale; il worker mantiene attore nullo e le sue verifiche tecniche dedicate.

`LegacyCaller` recupera esclusivamente il ClaimsContext originale di un principal legacy; qualsiasi principal istituzionale, inclusi InstitutionAdmin e Staff, provoca eccezione. Il solo chiamante produttivo è il ramo legacy di ExamManager.SubmitAsync. Nessun percorso istituzionale usa questa proprietà per autorizzare o redigere risultati.

### Submit e replay: una sola lettura del risultato ufficiale

Tre blocchi ripetuti in ExamSubmissionProcessor sono stati consolidati in `ReadResultAsync`: submit iniziale, replay già consegnato e gara persa su TryBeginSubmit. Il metodo rimane nella stessa transazione, rilegge la submission, mantiene il fallback precedente e carica l'eventuale Grade ufficiale. Un GradeId senza Grade continua a produrre errore; SuggestedGrade non diventa un voto ufficiale. Il numero e l'ordine delle query per ciascun ramo restano quelli precedenti.

### Scala del voto ufficiale

`Domain.Policies.GradeRules` possiede intervallo 1–10 e sufficienza 6. GradeService delega la validazione; processor, finalizzazione, redazione DTO e guardie della conversione usano la medesima decisione `IsPassing`. La formula decimale di conversione, l'arrotondamento, il limite 5.99 e PassingPercent dell'esame rimangono invariati. PassingPercent e voto ufficiale sono concetti distinti.

## Matrice delle decisioni

Le lettere A–E seguono la classificazione richiesta. B indica una migrazione/consolidamento effettuato; D un contratto legacy con consumatori identificati; E una rimozione o ricollocazione non dimostrata sicura. Nessuna intera entità storica è stata classificata A sulla sola base di una ricerca testuale.

| Componente precedente | Componente canonico | Responsabilità | Decisione | Chiamanti migrati | Evidenze |
|---|---|---|---|---|---|
| Predicati IAM nei repository e regola Business | Domain.Authorization.MembershipAuthorizationRules | Validità della membership | B / Consolidate | AuthManager istituzionale + 8 componenti persistenti | Diff e MembershipAuthorizationTests |
| ExamPrincipal.VisibilityCaller | SubmissionVisibility(studentView) | Redazione dei risultati | B / Migrate + Remove proprietà | ExamManager, ExamSubmissionManager, processor e mapper | Nuovi test submit/replay e rifiuto proiezione |
| Tre blocchi risultato submit | ReadResultAsync | Voto ufficiale e risultato idempotente | B / Consolidate | Tre rami del processor | ExamSubmissionProcessorTests |
| Soglie ripetute del voto | GradeRules | Scala ufficiale | B / Consolidate | Due servizi Business e tre componenti Application | Conversione e replay sotto soglia |
| UserRole/Auth/User legacy | InstitutionAuthManager per InstitutionAccess | Identità e credenziali | D / Blocked rimozione | Nessun cutover client | React Auth/login, Auth/me, Users; Web login.js/ApiRequests.js |
| Student legacy | InstitutionStudentManager per richieste tenant | Anagrafica studente | D / Blocked rimozione | Nessuna traduzione JWT | React Student/bylastname/update; Web LoadMainPage.js |
| Teacher legacy | InstitutionTeacherManager per richieste tenant | Anagrafica docente | D / Blocked rimozione | Nessun cutover | React Teacher/update/delete |
| Subject legacy | InstitutionSubjectManager per richieste tenant | Materie e visibilità | D / Blocked rimozione | Nessun cutover | React Subject/byname/create/update |
| GradeController legacy | GradeManager già condiviso | Voti e filtri | D / Keep contratto HTTP | Engine già condiviso | React e Web GradesPage.js |
| InstitutionExamManager e engine storici | Engine esami/submission/voti bound | Orchestrazione con contesto | C / Keep | Già deleganti | Open(), factory, scope e StaffTransaction |
| Teacher/Student e profili istituzionali | Anagrafica + associazione membership | Persona e accesso | C / Keep | Non duplicati | ProfileResolver e join tenant dei profili |
| Biennium/StudyArea/StudyPath legacy | Catalogo istituzionale per nuove richieste | Catalogo e associazioni | E / Blocked dismissione | Non migrati | Contratti senza revision/ledger vs amministrazione istituzionale |
| Class CRUD e CourseEdition assignment | Workflow separati | Gruppo e attribuzione a edizione | C / Keep | Nessuna fusione | Contratti Class e modello Course |
| ClassSubjectRepository e amministrazione | Query bound + mutazioni amministrative | Assegnazioni e uso didattico | C / Keep | Scope già espliciti | InstitutionExamManager e SchoolQueries |
| Biennium e CourseEdition/Enrollment | Modelli distinti | Intervallo, offerta e iscrizione | C / Keep | Non equivalenti | ITS_ACADEMY_COURSE_MODEL |
| Student update e trasferimento | StudentTransfer workflow | Intenzione, revisione e storico trasferimento | C / Keep | Separati | STUDENT_CLASS_TRANSFER |
| Piani e catalogo/calendario | Versione formativa e binding | Snapshot pubblicabile | C / Keep | Separati | FormativeDraftBuilder, PlanRules, repository |
| Import anagrafico e calendario | Parser comune, regole diverse | Parsing, staging e commit | C / Keep | Parser già condiviso | InstitutionImportFileReader e interfacce import/calendar |
| Calendario e piani | Calendar workflow | Pubblicazione, sostituzioni, monte ore | C / Keep | IAM comune migrato | CalendarManager, TeachingRules e repository partial |
| Lane worker legacy/istituzionale | Processor comune | Scansione e work tecnico | C / Keep | Core già condiviso | Store esclude legacy scope; work bound |
| Audit didattico/IAM/catalogo/platform | Ledger per aggregate | Storia append-only | C / Keep | IAM comune migrato dove applicabile | Schemi e attribuzioni differenti |
| InstitutionPermissions/CalendarPermissions | Policy pure + accesso risorsa scoped | Capabilities | C / Keep | Nessuna matrice privilegi modificata | Test Staff/Teacher/Student/Admin |
| Feature callback nel Domain | Contratti attuali | Configurazione runtime | E / Follow-up | Non ricollocati | Rivalidazioni rollout e test feature |
| PlatformOperator e InstitutionAdmin | Autorità separate | Firma piattaforma e membership tenant | C / Keep | Nessun bridging | PlatformAuthority, governance e onboarding |

## UserRole e InstitutionRole

Il JSON contiene **44 occorrenze lessicali in 18 sorgenti backend**, inclusi commenti, e 50 riferimenti nei sorgenti test/tool esaminati. Non sono conteggi di privilegi né prova di necessità: la necessità è dimostrata dai contratti sotto.

| Utilizzo | Semantica e trattamento |
|---|---|
| User.Role, RoleMapper, UserRepository | Campo globale `user.role`, stringhe admin/teacher/student. Non è la tabella dei ruoli membership. Persistenza preservata. |
| ClaimsContext, IJwtTokenGenerator/JwtTokenGenerator | Contratto JWT legacy con ruolo globale e vecchi profile claim. Mantenuto per i client identificati. |
| AuthManager, UserService, IUserService | Registrazione/account/password legacy; non sostituiti dalle sole operazioni IAM di membership. |
| ApiControllerBase e LegacyEndpointGate | Parsing claim e gate allowlist server-side. Il gate respinge claim istituzionali/Selection nel percorso legacy. |
| StudentManager/SubjectManager | Restrizioni storiche di visibilità e assegnazione; non convertibili in tenant senza membership verificata. |
| ExamPrincipal | Adapter d'ingresso legacy verso engine comune. Eliminata la proiezione inversa istituzionale; LegacyCaller è impossibile per un tenant. |
| ExamSubmissionProcessor | Solo l'entry point legacy legge UserRole.Student per determinare visibilità; il core condiviso non dipende da ClaimsContext. |
| LegacyAccess | Guard di migrazione allowlist, holding configurata, membership/stati e coerenza dei claim; non emette InstitutionAccess. Il confronto ruolo legacy/ruolo holding non è una conversione di autorizzazione. |
| Test e worker | Test legacy conservati; worker tecnico usa attore nullo/work bound e non genera un ruolo globale. |
| DI/serializzazione | Enum e stringhe JWT restano invariati; DI continua a registrare entrambi i confini espliciti. Nessuna policy istituzionale aggiunge un fallback globale. |

`InstitutionRole` conserva esattamente InstitutionAdmin=0, Teacher=1, Student=2, Staff=3. Anche UserRole conserva i valori originali. PlatformOperator resta separato. Nessuna attribuzione di membership, migrazione di dati o associazione di profilo è stata dedotta da nomi o ordinali.

Consumatori concreti: `Frontend/ElectronicRegisterReact/ElectronicRegisterApp.jsx` chiama login (circa riga 492), registrazione (540), Grade/paged (688), Student (699), Teacher (701), Users (1242), RegisterForAdmin (1252), updatepassword (1446/1467), Auth/me (1599). Il frontend Web usa Auth/login in `Authentication/assets/js/login.js`, Auth/me in `Dashboard/assets/js/ApiRequests.js` e Grade/paged in `GradesPage.js`. Nessun frontend modificato.

Nel piano precedente si richiedeva di migrare entrambi i client; con la loro sostituzione prevista, il requisito riguarda invece i nuovi frontend: login Selection → selezione membership → InstitutionAccess, identificare l'istituzione esplicita delle operazioni e migrare gli account/profile link con procedura verificata. Vanno trattati separatamente password/global identity e amministrazione membership; poi disabilitare emissione/consumo JWT legacy, verificare gli altri consumatori/tool e progettare l'eventuale ritiro di `user.role` e dei link storici. Non basta una rinomina dell'enum o un adapter che inventi il tenant.

## Percorsi end-to-end e separazioni

- Autenticazione istituzionale: IdentityController → InstitutionAuthManager → password/Microsoft identity link + MembershipAuthorizationRepository → Selection/InstitutionAccess. La selezione non concede da sola accesso didattico.
- Autorizzazione: handler/policy → TenantContext → AuthManager/SchoolAuthorization → regola IAM comune → query/profili e controlli risorsa. Commit sensibili conservano lock e verifica dello stato corrente.
- Anagrafiche: controller Student/Teacher/Subject istituzionali → rispettivi manager → repository scoped/SchoolQueries → tabelle possedute e profili. Le pipeline legacy hanno contratti client ancora attivi.
- Classi/catalogo/ClassSubject/Campus: controller dedicati → manager coesi → regole Domain → repository e AdministrationPersistence → ownership, revision e ledger. Non unificati con query di supporto agli esami.
- CourseEdition/Enrollment: controller → InstitutionCourseManager → CourseStructureRepository partial → edizioni/iscrizioni/lifecycle e ledger; distinto da Biennium e dalla classe corrente dello studente.
- Trasferimenti: controller → TransferManager → TransferPolicy/TransferRepository → lock IAM-first e studente, revision, intenzione e snapshot storici.
- Esami/Grade: controller istituzionali → InstitutionExamManager → engine condivisi + Business rules → repository factory/scope → stesse tabelle con tenant e verifiche risorsa. Non sono due algoritmi di grading indipendenti.
- Submission: controller → engine/processor → grading Business → repository bound → lock, revision, Grade e audit. Review/finalize è un caso d'uso distinto dal submit automatico.
- Piani formativi: controller → FormativePlanManager → draft builder/regole → repository → versioni immutabili pubblicate, binding e audit.
- Import Student/Teacher: controller → ImportManager → parser condiviso/regole categoria/email generator → sessione repository → staging, digest, prenotazioni, commit idempotente e anagrafiche.
- Calendario/sostituzioni/ore: controller → CalendarManager → CalendarPermissions/CalendarRules/TeachingRules → repository partial → lesson, periodi, pubblicazione e ledger. Ore previste ed erogate non sono una copia del piano formativo.
- Worker: timer → selezione legacy o store istituzionale bounded → InstitutionExpiryService/work bound → processor condiviso → transazione e audit tecnico. Le scansioni hanno confini distinti.
- Audit/governance: manager specifici → repository/autorità platform → ledger dedicati. Non vengono sintetizzati eventi storici né riusati tipi di audit incompatibili.

Questi percorsi sono ricostruzioni statiche. L'esecuzione end-to-end delle loro operazioni SQL non è stata dimostrata dall'ambiente corrente.

## Contratti, configurazione e persistenza

Nessuna route, attributo HTTP, policy endpoint, payload JSON, flag, configurazione di rollout o DI modificati. `InstitutionPermissions` rimane una policy pura relativamente piccola: membership, capabilities e proprietà della risorsa restano tre responsabilità distinte; dividerla ulteriormente non risolve una violazione dimostrata.

**Rotture CLR interne al backend:** MembershipAuthorizationRules cambia assembly/namespace da Business a Domain; `ExamPrincipal.VisibilityCaller` viene rimosso e sostituito dal contratto più stretto LegacyCaller. Tutti i chiamanti compilati nel repository sono migrati. Eventuali consumer .NET esterni devono ricompilare/aggiornare i riferimenti. Nessuna garanzia dichiarata sull'assenza di consumer esterni.

I callback di CalendarFeature/FormativePlanFeature/InstitutionImportFeature rimangono nel Domain come debito architetturale esplicito. I DTO condivisi sono usati anche da Infrastructure: spostarli unilateralmente in Application creerebbe una dipendenza inversa. Separare configurazione runtime e dati richiede un intervento dedicato e prove delle rivalidazioni sotto lock. Gli attributi JsonIgnore di Create/UpdateExamDto restano parte dei contratti di input, non una ragione per cancellare regole o indebolire validazione.

Non sono state eseguite modifiche SQL, migrazioni o operazioni su dati. I 40 file generati dichiarati nel manifest non hanno differenze Git. Ownership, FK, indici, query, transazioni, audit append-only e idempotenza non sono stati riscritti. Nessuna garanzia nuova viene attribuita a CHECK MySQL 5.7 o al rollback DDL.

## Verifica autonoma aggiornata — 29 ottobre 2026

Questa nuova esecuzione sul working tree corrente sostituisce i risultati di verifica storici riportati sotto: **Unit 585/585 e Integration SQL/HTTP completa 1431/1431, zero Failed e zero Skipped**. Discovery e TRX coincidono per nomi e molteplicità, senza conteggi attesi fissati ai run precedenti. Nessun progetto test Infrastructure separato è presente. Build soluzione riuscita (incrementale, 0 errori/0 warning); guard architetturale Roslyn eseguito: 724 file, 721 tipi, 0 violazioni.

ExecutionPolicy effettiva `RemoteSigned`: runner PowerShell avviato normalmente, senza modifiche alla policy. È stata usata esclusivamente una datadir creata in questa sessione, attestata via SQL come MySQL 5.7.24 sulla porta 13432; nessuna modifica a istanze preesistenti. Dopo il primo errore di bind su 13431, la stessa nuova datadir è stata avviata su porta libera, senza reinizializzazione o replay del bootstrap. Il restore NuGet locked è riuscito usando configurazione locale e APPDATA nella workspace per i soli processi di verifica. Non è stato modificato il NuGet.Config utente inaccessibile.

L'istanza nuova è stata arrestata a fine verifica, conservando datadir, log e TRX reali. Vedere [report riproducibile e dettagli ambientali](validation/autonomous/README.md) e [risultati verificati con hash](validation/autonomous/test-summary.json). Nessun commit o push. Il risultato attesta la regressione del codice corrente, non una baseline SQL pre-refactoring né il completamento dei lavori architetturali residui.

## Verifiche storiche eseguite e limiti (superate dal nuovo run sopra)

| Verifica | Risultato |
|---|---|
| Build soluzione iniziale | Pass, 0 errori, 11 warning |
| Build dopo ogni gruppo codice | Pass dopo le correzioni di compilazione; log locali conservati |
| Build finale incrementale | Pass, 0 errori, 10 warning ricompilati nei test; la build completa precedente aveva gli 11 warning noti |
| Unitari baseline/finali | 559 → 587 passed, 0 failed/skipped |
| Integration baseline/finali | 82 passed / 1279 failed / 57 skipped in entrambe |
| Confronto nominativo e molteplicità esiti TRX | 0 casi precedenti rimossi/cambiati; 28 nuovi casi unitari passed |
| Scaffold, csproj, SQL e Api | Nessun file modificato nei rispettivi controlli Git |
| git diff --check | Pass |
| Guard Roslyn organizzazione | Non eseguito: assets mancanti; restore bloccato dall'accesso negato a `%APPDATA%/NuGet/NuGet.Config`, anche indicando configurazione locale |

I 28 nuovi casi sono: 15 combinazioni non vuote di ruoli con variazioni di identità/generazione/stato e dati malformati; membership assente; quattro rifiuti di proiezione istituzionale; conservazione dell'identità legacy; due submit istituzionali manual/expired con audit e redazione; quattro replay con/senza Grade ufficiale; repository bound non corrispondente. I test precedenti conservano le asserzioni. Un test Staff è migrato dalla proprietà eliminata al nuovo confine LegacyCaller mantenendo il rifiuto della conversione legacy.

Una prova unitaria passata eseguita accidentalmente dopo una build intermedia fallita non è usata come evidenza della migrazione IAM: i risultati riportati provengono dalla build riuscita e dalle TRX finali 587. Nessun test è stato disabilitato o eliminato.

## Lavoro residuo e proposta ordinata

La regressione SQL/HTTP del codice attuale è superata. Restano aperti la dismissione del legacy, i componenti architetturali sospesi e l'analisi semantica residua. Il guard Roslyn e la qualità dei guard fixture/startup restano verifiche da completare. Una baseline SQL verde pre-refactoring non è disponibile e non viene dedotta dal run corrente.

La [proposta concreta di completamento](RESPONSIBILITY_COMPLETION_PROPOSAL.md) definisce sette fasi con dipendenze, risultati e criteri di uscita: contratto nuovi frontend; consumatori; mapping identità/dati; ritiro legacy; runtime/DTO; analisi semantica; verifiche finali. I vecchi frontend saranno sostituiti: le chiamate censite indicano l'impatto del ritiro e non impongono compatibilità permanente. Non occorre migrare i vecchi client per principio; occorre rendere pronti i nuovi e risolvere gli eventuali consumatori ancora supportati.

Il refactoring complessivo rimane parziale. Nessun codice applicativo o test è stato modificato in questo aggiornamento; nessun test rieseguito, connessione MySQL, modifica alle istanze o ai dati, commit o push.
