# Chiusura A01–A04 e gate di rilascio

30 ottobre 2026. **A01–A04: FIXED per le garanzie verificate. Decisione di rilascio: NOT READY**, perché Docker, TLS moderno della destinazione e job Actions Linux/Windows non sono stati eseguiti in questo ambiente. Non risultano bug applicativi ancora aperti fra A01–A04. Il 22/30 resta la valutazione indipendente precedente: questo intervento non assegna un nuovo voto.

Nessun refactoring generale, commit, push, reset, clean, deployment o accesso a database preesistenti. Le modifiche pregresse del working tree sono conservate. Le SQL 001–004 e il manifest storico non sono stati riscritti. Nuove evidenze in [gate-remediation-evidence](gate-remediation-evidence/); log/TRX con dati sintetici o credenziali di fixture restano nelle directory private delle nuove istanze.

## Risultati finali

| Controllo | Esito effettivo |
|---|---|
| Restore locked soluzione | [Pass](gate-remediation-evidence/restore-release-final.log) |
| Build Release completa, no-incremental, analyzer | [0 errori, 10 warning nullable preesistenti nei test](gate-remediation-evidence/build-release-final.log) |
| Unit | [590 Passed, 0 Failed/Skipped](gate-remediation-evidence/unit-release-final.log) |
| Integration SQL/HTTP, nuova istanza 5.7.24 porta 13454 | 1531 Passed, 0 Failed/Skipped; 7m33s |
| Discovery/TRX e baseline | [Multiset nominativo coincidente, 0 rimossi/rinominati, 29 aggiunti](gate-remediation-evidence/test-summary-final.json), [riepilogo](gate-remediation-evidence/reconciliation-final.log) |
| Guard Roslyn/dipendenze architetturali | [660 sorgenti, 657 tipi, 0 violazioni](gate-remediation-evidence/guard-release-final.log) |
| Tooling Python | [18 passati](gate-remediation-evidence/python-release-final.log) |
| Manifest canonico e portabilità LF/CRLF | [738 artefatti](gate-remediation-evidence/manifest-release-final.log); [14 controlli negativi](gate-remediation-evidence/manifest-controls-release-final.log) |
| Dipendenze NuGet online incluse transitive | [Soluzione](gate-remediation-evidence/dependency-audit.json), [gate](gate-remediation-evidence/dependency-audit-gate.log), [probe trasporto](gate-remediation-evidence/transport-audit.json): nessun advisory segnalato |
| Publish da contesto pulito, risorse SQL | [Publish](gate-remediation-evidence/package-publish.log), [14 risorse embedded confrontate byte per byte; driver 2.6.1](gate-remediation-evidence/package-resources.json) |
| git diff --check | [Exit 0](gate-remediation-evidence/diff-check-final.log) |
| Docker / TLS moderno / GitHub Actions | NON ESEGUITI; non sostituiti da test locali o da uno scan offline |

La riconciliazione legge le TRX reali con SHA256 e normalizza gli escape Unicode, senza limitarsi ai contatori. La baseline è quella dell'audit finale: 590 Unit e 1502 Integration. Tutti i casi esistenti restano presenti con la stessa molteplicità. I 29 nuovi casi coprono IAM/password/replay, acquisizioni reciproche, osservatore, mutante con validazione anticipata, dispatch tecnico e marker della probe.

## A01 — FIXED — Contratto additivo e versione minima della probe

**Difetto e riproduzione:** la vecchia routine contava oggetti e sole tre colonne identità. Eliminare identity_password_attempt.lease_until lasciava sia readiness 200 sia preflight exit 0, mentre la query quota falliva 1054. Le evidenze originali dell'audit restano intatte.

**Correzione:** nuovo contratto revisionato [additive-contract.json](../../migrations/release/additive-contract.json), vincolato dagli hash normalizzati delle quattro SQL originali. La nuova [005_release_gate.sql](../../migrations/identity-account/005_release_gate.sql), generata/verificata da `tools/release-additive-contract.cjs`, sostituisce la sola routine e aggiunge il marker er-quality-1-closure-1. Verifica 5 tabelle additive, 26 colonne con tipo/nullability/default/extra/collation, 18 parti di indice, 7 FK con colonne/referenti/rules, 6 trigger con timing/evento/tabella/SQL_MODE e digest del corpo. Mantiene i controlli base di presenza di 71 tabelle e 180 trigger. Sono query metadata statiche e SELECT, senza DDL/DML nella probe.

`OperationalPipeline.MapOperationalEndpoints` richiede inoltre il marker nuovo nella query d'identità, **indipendentemente dalla routine**: un database fermo a 004 non può ottenere 200 dal nuovo backend. `release-preflight.cjs` confronta anche tutte le definizioni additive, i sei corpi trigger e il corpo/security/access della routine, oltre alle 66 tabelle/174 trigger base. Non apprende il contratto da una destinazione potenzialmente alterata e non ripara nulla.

**Prove:** [63 controlli negativi SQL](gate-remediation-evidence/schema-matrix.json), con ripristino dello schema; casi di default per tutte le 26 colonne, tipo/nullability, ogni gruppo di indice/PK, ogni FK e ogni trigger sia assente sia alterato a nome invariato, tabella quota assente, marker, routine assente/SQL SECURITY alterata. Il preflight resta strutturalmente più rigoroso della readiness; la presenza dei FK/PK vincola anche le colonne che non possono essere eliminate indipendentemente senza altri errori di schema.

Sul **binario finale**, dopo la suite completa, il [controesempio originale](gate-remediation-evidence/final-schema-negative.json) dà routine 0, preflight fallito e [HTTP 503](gate-remediation-evidence/lease-until-final.json); dopo ripristino, preflight verde e [HTTP 200](gate-remediation-evidence/schema-final-restored.json). L'account HTTP è non root, con CRUD/EXECUTE e senza DDL. Sono conservati anche i timestamp sintetici della quota durante DROP/ADD.

La [prova prima di 005](gate-remediation-evidence/prior-gate-negative.json) attesta vecchia routine 004=1, nuova API live=200/ready=503; [dopo 005](gate-remediation-evidence/gate-complete.json), live/ready=200. Il nuovo test `Release_ReadinessRequiresGateRevisionMarker` verifica rimozione/ripristino della revisione, senza indebolire i test precedenti su marker/EXECUTE.

**Rischi residui:** DDL MySQL non transazionali; DROP/CREATE della routine elimina i grant specifici, da riapplicare col passo 99 del bundle. Il costo metadata è verificato sui dati di test entro il timeout, non certificato sotto carico di produzione. Readiness non è una difesa contro un amministratore SQL malevolo; il preflight verifica più definizioni. Nessun DDL è attribuito all'account API.

## A02 — FIXED — Arco requester → blocker associato alle operazioni

**Difetto e riproduzione:** il COUNT filtrato sul solo blocker poteva essere soddisfatto da un terzo waiter, prima di controllare contender.IsCompleted. Un Guid nel testo SQL non identificava il task.

**Correzione:** `tests/Integration/SqlLockEvidence.cs` associa i task tramite `Track`. Per i percorsi HTTP il contesto W3C univoco attraversa il vero HttpClient/Kestrel; DiagnosticListener EF fornisce l'oggetto MySqlConnection realmente aperto. I helper SQL nativi catturano esplicitamente la propria connessione. La query richiede entrambi gli ID MySQL esatti, con task ancora pendenti prima/dopo l'osservazione. Un blocker SQL nativo deve essere la connessione aperta che possiede la transazione; overload Guid e intero non associato falliscono esplicitamente.

L'associazione conserva l'oggetto connessione, non un vecchio ID fisico del pool. Se un read preliminare chiude/riapre la connessione durante la lettura delle tabelle InnoDB, si ripete l'osservazione; non si accetta l'arco di una sessione riciclata. I 150ms servono soltanto alla cache I_S 5.7. Il successo richiede un arco reale e stabile, non un ritardo.

**Prove:** `Release_ObserverRejectsThirdWaiterAndCompletedContender` mantiene una terza attesa reale e rifiuta sia un task pendente senza quell'arco, sia il task già completato, sia Task.CompletedTask non associato. Il mutante senza IAM lock già esistente resta; `Release_ValidationBeforeLockMutantCannotProveSerialization` sposta deliberatamente la validazione prima del mutex e dimostra la scrittura dopo revoca, con osservatore che rifiuta la falsa serializzazione. Sono mutanti esclusivamente di test, mai registrati in produzione.

Aggiornati i chiamanti Staff, BulkImport, Calendar, Email, FormativePlan, security state, QualityRevocation e OrderedAsync. Le loro asserzioni finali su dati/audit/commit restano. La suite completa passa con l'osservatore corretto. **Residui:** richiede privilegi di osservazione solo al test harness; non misura fairness e non prova l'assenza universale di ogni deadlock possibile.

## A03 — FIXED — Generation globale nelle mutazioni IAM

**Difetto e riproduzione:** autenticarne g prima del manager non impediva a MutateAsync/AcquireProfileAsync di committare dopo una rotazione g+1. La riproduzione deterministica usa una barriera prima del mutex e una versione test-only che omette precisamente RequireGenerationAsync: sospensione, aggiunta/rimozione ruolo, acquisizione e replay completano dopo la rotazione. Non è una riproduzione con sleep o sola ipotesi statica.

**Correzione:** `InstitutionMembershipMutationRepository` dipende dal port account e rivalida la generation autenticata sotto transazione, dopo tenant/IAM e prima di risorsa/ledger, anche nei replay. Contesti amministrativi senza IdentityGeneration falliscono chiusi. La membership resta rivalidata e l'hash idempotente non incorpora la generation, così un nuovo token autorizzato può riprovare lo stesso intento.

L'acquisizione blocca anche l'identità del destinatario: ora legge il suo ID con current locking read e ordina **entrambi** i lock globali per Guid canonico. Il semplice actor→target avrebbe creato una possibile inversione fra acquisizioni reciproche in due istituzioni. Nessuna nuova dipendenza inversa da identità a tenant; il cambio password continua a bloccare soltanto l'identità. `RequireLocked` confronta anche IdentityGeneration.

**Prove:** `Release_PasswordRotationLinearizesAdministrativeIam` copre entrambe le direzioni per sospensione, add/remove ruolo, acquisizione, replay ruolo e replay acquisizione. Password-prima: 403 della richiesta già entrata e snapshot IAM/ledger invariato; richieste successive col JWT vecchio: 401. IAM-prima: arco SQL esatto della rotazione in attesa, commit IAM, poi password, senza duplicazione del replay. `Release_ReciprocalAcquisitionsOrderBothGlobalIdentities` esercita due tenant/attori invertiti e prova attesa sul primo lock globale, poi entrambi i commit. I 6 mutanti di omissione dimostrano l'effetto della proprietà rimossa; non sono correzioni cosmetiche di test.

I produttori dei contesti diretti nelle fixture ora forniscono la generation persistita alla creazione, preservando quella di contesti già emessi. Il caso utente inesistente continua ad attendersi UnauthorizedAccessException; è stato corretto il lookup dell'helper, non l'asserzione. **Residui:** maggiore contesa dell'identità è intenzionale; throughput multi-tenant non misurato. Le garanzie provate riguardano questi percorsi, non una prova formale dell'intero sistema.

## A04 — FIXED — Schema e metadati del manifest

**Difetto e riproduzione:** format=999, release errata e requiredMigrations vuoto erano accettati perché veniva confrontato soltanto files.

**Correzione:** `tools/release-artifacts.cjs` verifica l'insieme esatto dei campi, format/release/hashEncoding/historicalEvidence/bootstrapInventory e sequenza canonica bootstrap+001–005. Rifiuta SQL inattese, duplicati, omissioni, ordine errato, file/hash mancanti e campi estranei. L'inventario bootstrap è anche riconciliato con le definizioni reali di tabelle/trigger/FK/indici. Restano i controlli case-sensitive, ignore/path e LF/CRLF. Il manifest storico non cambia.

**Prove:** [14 controlli negativi sul verifier reale](gate-remediation-evidence/manifest-controls-release-final.log), senza riscrivere il JSON su disco. L'[intercettazione originale dell'audit](gate-remediation-evidence/original-manifest-negative.log) ora termina con errore sui metadati. Verifier positivo e 18 test Python passati. Il manifest corrente è aggiornato per i soli cambi concreti di questa slice e include contratto, 005, tool, CI e configurazioni pubbliche; nessun aggiornamento indiscriminato del manifest storico.

**Residui:** un manifest non è una firma della supply chain. Il suo identificativo rimane er-quality-1; il prerequisito SQL della revisione è esplicito nel contratto e nel backend. Nessun risultato Actions Linux è dedotto dal test LF/CRLF locale.

## Worker, definer e privilegio minimo

Il contratto istituzionale esclude lavoro tecnico su tenant archived e holding portata active; la sospensione ordinaria consente invece la chiusura tecnica delle submission scadute. Non era sufficiente la risoluzione della capability prima della transazione. `InstitutionExamDataScope.LockTechnicalTenantAsync` rivalida ora lo stato/marker con current locking read dopo tenant S e prima della submission, senza introdurre un attore utente.

`Release_WorkerDispatchRevalidatesTenantStateUnderLock` copre archive/suspended/holding-active in entrambi gli ordini. Transizione-prima: niente scrittura/audit su archived o holding active, dispatch permesso su suspended. Worker-prima: la transizione attende sul parent, il worker committa una sola chiusura, poi la transizione procede. Owner e capability tecniche non sono riscritti; l'engine rimane condiviso.

[prepare-release-sql.cjs](../../tools/prepare-release-sql.cjs) genera un artefatto derivato, senza eseguire SQL, con principal espliciti, hash degli input/output e owner ACCOUNT LOCK. Binding dei definer storici e grant preliminari/finali sono separati dagli originali. Il preflight richiede il definer atteso e ne verifica esistenza e corrispondenza su routine e trigger. Il contratto additivo e il manifest sono inclusi anche nel bundle Docker.

Su una seconda istanza nuova, [bootstrap derivato senza er_test](gate-remediation-evidence/principal-bootstrap.json) e [verifiche dei principal](gate-remediation-evidence/principal-checks.json) dimostrano: tutti i 180 trigger con definer esplicito bloccato; DML rappresentativo API riuscito; preflight con reviewer non root; API CREATE/ALTER/DROP negati; readiness 503 senza EXECUTE, senza visibilità metadata del definer o con definer assente; ripristino 200. Nessun grant DDL all'API. I permessi metadata più ampi del reviewer sono confinati all'identità operativa di verifica, non al servizio.

## Riproducibilità, tentativi e limiti ambientali

Cinque istanze nuove con porte 13450–13454 e datadir UUID distinte; identità SQL verificata prima delle scritture e prima dell'arresto. [Tutte arrestate, dati/TRX conservati](gate-remediation-evidence/shutdown-final.json); anche il relay temporaneo è arrestato. I risultati finali provengono esclusivamente dall'ultima, senza riuso di database precedenti. I helper/provisioner e le prove negative sono conservati nelle evidenze. Il runner pubblico di CI continua a creare una nuova istanza e ad applicare le SQL canoniche.

I tentativi diagnostici non sono nascosti: errore iniziale del generatore su literal SQL vuoto, sintassi ALTER DEFAULT timestamp 5.7 e ripristino esplicito NULL sono corretti sulla sola istanza diagnostica; la migrazione finale è poi applicata una sola volta da zero. Un primo run su binari precedenti a una build fallita non è prova finale. La prima suite completa aveva [un errore nell'helper di fixture](gate-remediation-evidence/first-full-classification.json); la successiva è passata 1530/1530. Dopo il controllo indipendente della revisione minima è stata aggiunta un'ulteriore prova e ripetuta l'intera suite su nuova istanza: 1531/1531. Non sono state eliminate o allentate asserzioni per ottenere il verde.

Audit NuGet online tramite relay loopback con upstream HTTPS validato, [richieste reali](gate-remediation-evidence/nuget-relay-requests.log); nessuna opzione di ignorare CA. Non è una configurazione runtime. La regressione locale SQL usa il trasporto confinato di test, non certifica TLS Production. Il client/grafo resta MySqlConnector 2.6.1 senza aggiornare indiscriminatamente EF/Pomelo.

Il tool read-only `ReleaseTransportProbe` è [compilato senza warning](gate-remediation-evidence/transport-build-final.log), con [restore locked](gate-remediation-evidence/transport-locked.log). Richiede prima una connessione TLS 1.2 positiva e schema ready; poi negativi CA/hostname con errore di autenticazione TLS. Un errore DNS/socket non diventa una prova del certificato. **La connessione moderna non è stata eseguita qui**, né è stato aggirato il limite TLS MAMP.

La CI ora include controlli negativi del manifest, guard, build/audit della probe e controllo immediato degli exit code nativi nel job SQL. Sono verificati gli equivalenti Windows disponibili localmente. Docker resta indisponibile: publish e risorse non sono presentati come docker build/run. Per il collaudo reale, privilegi/definer, Docker non-root, readiness Production, TLS e reverse proxy Nginx: [comandi e requisiti](DESTINATION_RELEASE_SMOKE.md), [runbook aggiornato](RELEASE_READINESS.md).

**Residui bloccanti di release:** prova TLS moderna positiva e rifiuto CA/hostname dalla destinazione reale; build/run Docker con startup Production, UID, mounts e probe attraverso Nginx; job source realmente eseguiti su Linux/Windows e job SQL del runner dedicato. Nessun deployment EC2 è avviato o autorizzato da questo documento. F09, misure di carico e warning dei test restano miglioramenti separati già dichiarati, non nuovi bug A01–A04 aperti.
