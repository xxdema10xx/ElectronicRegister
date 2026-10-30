# ElectronicRegister — verifica indipendente finale

## Aggiornamento dopo la chiusura A01–A04

30 ottobre 2026. **A01–A04 FIXED nel perimetro verificato; decisione di rilascio ancora NOT READY.** Il 22/30 sotto resta il voto dell'audit precedente: questa remediation non si attribuisce un nuovo voto indipendente.

La [remediation conclusiva](RELEASE_GATE_REMEDIATION.md) documenta correzioni e prove: 63 controlli negativi SQL, osservazione dell'arco requester→blocker associato alle connessioni reali, revoca globale IAM in entrambe le direzioni incluse acquisizioni/replay, 14 controlli negativi del manifest. Anche il worker rivalida stato/holding sotto lock; il provisioning con definer esplicito bloccato e API senza DDL è verificato su istanza nuova.

Risultato finale: **590 Unit e 1531 Integration Passed, 0 Failed/Skipped**, discovery/TRX coincidenti per nomi e molteplicità; tutti i 1502 casi Integration precedenti sono conservati e 29 sono nuovi. [Evidenza nominativa](gate-remediation-evidence/test-summary-final.json). Sul binario finale, colonna lease_until mancante→readiness 503/preflight fallito; ripristino→200/preflight verde. Una vecchia routine 004 che restituisce 1 non basta: senza il marker della revisione 005 la nuova API restituisce 503.

Non risultano bug applicativi A01–A04 ancora aperti. Restano **gate ambientali obbligatori**: TLS moderno sul database destinato al rilascio con CA/hostname positivi e negativi; esecuzione reale Docker/Production non-root e proxy Nginx; job Actions Linux/Windows e runner SQL realmente eseguiti. Docker è indisponibile qui; il limite MAMP non è stato aggirato. [Requisiti e comandi di collaudo](DESTINATION_RELEASE_SMOKE.md). Nessun deployment, commit, push o modifica di database preesistenti.

## Verifica indipendente precedente — storico

Data: 30 ottobre 2026. **Decisione: NOT READY. Valutazione: 22/30.**

Il backend presenta miglioramenti sostanziali e verificabili rispetto all'audit da 21/30. La dichiarazione «10 FIXED, 2 MITIGATED» non basta però a certificare il rilascio: un controllo negativo riproduce readiness **200 su uno schema incompleto** e un secondo dimostra un falso positivo dell'osservatore di concorrenza. Restano inoltre aperte la verifica TLS sul database di destinazione e l'esecuzione dell'immagine Docker. La revoca globale nelle mutazioni amministrative IAM richiede una prova e una decisione esplicite.

## Perimetro ed evidenze

Esaminati audit, remediation, runbook di release, report conclusivo del refactoring, contratti istituzionali, matrice legacy e relativi risultati. I checkpoint storici PARTIAL nei documenti non sono stati confusi con la sezione corrente COMPLETED. [Inventario delle 133 evidenze pregresse, con hash](final-assessment-evidence/prior-evidence-inventory.json); [hash dei file centrali ricontrollati](final-assessment-evidence/reviewed-files.json). Le evidenze storiche includono tentativi falliti: non sono state promosse a verifiche riuscite.

La revisione semantica è concentrata sui confini richiesti, non costituisce una prova formale di ogni sorgente. I nuovi helper diagnostici sono sotto `final-assessment-evidence`; non sono stati modificati sorgenti applicativi, test preesistenti, manifest o migrazioni. Nessun commit, push o deployment. Il diff generale contiene il refactoring e la remediation pregressi e non è attribuito a questa verifica.

Le nuove prove SQL usano esclusivamente MySQL **5.7.24**, porta **13449**, datadir nuova `instance-4063ce7d0a454da090f59ba283875bf7/data`. Versione, porta e datadir sono controllate prima delle operazioni. Nessuna istanza precedente è riutilizzata. Le credenziali e gli output SQL/HTTP potenzialmente sensibili restano nella directory privata. I controlli negativi alterano soltanto questo schema di test e ripristinano la colonna prima della regressione. Al termine è stata arrestata soltanto questa istanza, dopo nuova attestazione SQL; [arresto verificato, dati conservati](final-assessment-evidence/shutdown.json). Anche il relay NuGet temporaneo è stato arrestato.

## Ricontrollo dei finding originali

| Finding | Valutazione indipendente |
|---|---|
| F01 | Chiusura attuale di 728 artefatti valida; manifest storico preservato, risorse SQL incluse. Il validatore non verifica tutti i metadati del nuovo manifest: A04. Windows eseguito, Linux/Actions non eseguiti. |
| F02 | Correzione Exam/Grade confermata nel codice: wrapper per tutti gli attori, autosave incluso, autorizzazione corrente sotto lock. La certificazione dei test resta limitata da A02; la revoca globale IAM amministrativa presenta A03. Nessun nuovo IDOR cross-tenant dimostrato. |
| F03 | Driver corretto 2.6.1, graph/lock e audit online verificati. **MITIGATED**, non chiuso il gate TLS reale. |
| F04 | Liveness e probe read-only correttamente separate, errori/permessi fail-closed. **Chiusura non confermata**: A01 riproduce schema obbligatorio incompleto con entrambi i gate verdi. |
| F05 | Tie-breaker Id e offset long prima del cast presenti; test funzionale nella regressione. Resta la normale instabilità dell'offset tra richieste su dati modificati. |
| F06 | Propagazione ampia e commit atomico separato dalla cancellazione presenti. Residui piccoli: ramo combinedRead senza ct e parametro ct ignorato da AuthorizedTransaction a favore del requestCt catturato. Nessun bypass funzionale dimostrato da questi residui. |
| F07 | Split mirato alle due collezioni, snapshot RepeatableRead locale o transazione del chiamante, lock del parent conservato. Il campione 1.600 righe dimostra il fan-out; non certifica throughput o miglioramento di latenza. |
| F08 | Quota SQL globale per UserId, prima di BCrypt, condivisa fra host. Non emerge un bypass mediante PUT, IP, proxy o ticket. Lease e finestra hanno i limiti descritti sotto. |
| F09 | Fallback sconosciuto effettivamente fail-closed, anche tecnico. **MITIGATED** coerente: hotspot typeof/SaveAsync rimane, nessuna riscrittura richiesta senza un nuovo difetto concreto. |
| F10 | Error code e metadati provider allowlist, niente messaggio SQL/credenziali al client o nel log operativo esaminato. Test sentinella mantenuti. |
| F11 | Migliore delle attese temporali precedenti, ma **chiusura non confermata**: falso positivo del vero helper dimostrato in A02. |
| F12 | `await using` della sola transazione locale; transazione ambientale non smaltita. Test di ownership mantenuto. |

## Problemi e limiti nuovi

### A01 — High — Readiness e preflight accettano una colonna obbligatoria assente

**File/metodi:** `migrations/identity-account/004_release_schema.sql`, `er_schema_ready`; `tools/release-preflight.cjs`, controllo additivo finale; `Api/Operations/OperationalPipeline.cs`, `MapOperationalEndpoints`.

**Evidenza eseguita:** [helper riproducibile](final-assessment-evidence/schema-probe.cjs), [risultato completo](final-assessment-evidence/schema-negative-control.json), [HTTP del Program reale](final-assessment-evidence/missing-quota-column.json). Partendo dallo schema completo con preflight riuscito, `ALTER TABLE identity_password_attempt DROP COLUMN lease_until` lascia `CALL er_schema_ready()` a **1**, preflight con exit **0**, `/health/live` **200**, `/health/ready` **200**. La query necessaria alla quota password fallisce con **1054 Unknown column**. La colonna è ripristinata nel finally. Il server API eseguito è Release, in modalità LocalTest confinata a loopback: questa prova certifica il comportamento della probe, non una topologia Production.

**Causa:** il controllo additivo verifica soltanto tre colonne di identity_account_state, conteggi di nomi e sette FK. Il preflight confronta le 66 tabelle e i 174 trigger base, ma per le additive delega alla stessa routine incompleta. I sei trigger aggiuntivi sono contati, non confrontati nel corpo. Non serve ipotizzare un amministratore malevolo per il controesempio: basta una deriva o un'applicazione parziale dello schema obbligatorio.

**Impatto:** instradamento di traffico a un backend con cambio password indisponibile; il gate di release non rileva il guasto. **Bloccante** per dichiarare affidabile la readiness richiesta.

**Rimedio/test necessari:** aggiungere alla probe i prerequisiti operativi di tutte le additive; confrontare nel preflight definizioni additive, indici/FK attesi e corpi dei relativi trigger. Test negativi per singola colonna obbligatoria, tabella, indice/vincolo e trigger alterato a nome invariato; 503 o preflight fallito, senza DDL della probe. **Rischio della modifica: medio**, per falsi 503 e visibilità metadata; mantenerla leggera e read-only.

### A02 — Medium — L'osservatore può attribuire al concorrente l'attesa di un'altra connessione

**File/metodo:** `tests/Integration/SqlLockEvidence.cs`, `WaitCoreAsync`; chiamanti di OrderedAsync e prove IAM.

**Evidenza eseguita:** [programma diagnostico](final-assessment-evidence/observer-probe/Program.cs), [risultato](final-assessment-evidence/observer-negative-control.json). Il programma invoca tramite reflection **il vero helper compilato**, non una copia della query. Una transazione blocca la riga del marker; una seconda connessione indipendente attende realmente il lock. Passando `Task.CompletedTask` come concorrente atteso e l'ID del blocker, l'helper ritorna successo. Il risultato registra `ObserverAccepted=true`, `ExpectedAcceptance=false`.

**Causa:** il ramo blocker filtra soltanto `b.trx_mysql_thread_id`; quello marker identifica testo SQL, non la connessione attesa. Il ritorno su COUNT>0 precede persino il controllo contender.IsCompleted. L'arco osservato esiste, ma non è necessariamente quello del task indicato.

**Impatto:** falso positivo in presenza di un terzo concorrente; non dimostra che i run attuali abbiano preso quel percorso. Le barriere e gli esiti SQL dei test mantengono valore, ma il helper non dimostra da solo l'identità dell'attesa. Il mutante senza lock esistente copre l'assenza di qualsiasi arco, non questo controesempio. **Da chiudere prima di certificare definitivamente F02/F11.**

**Rimedio/test necessari:** handshake con ID della connessione del requester e verifica dell'arco requester→blocker esatto; controllare che il task non sia già completato. Ripetere il controllo negativo con terzo waiter, mutante senza lock e rivalidazione spostata prima del lock, senza sostituire l'osservazione con sleep. **Rischio: basso/medio**, confinato all'harness ma con possibile effetto sull'interleaving.

### A03 — High — Revoca globale non rivalidata sotto lock nelle mutazioni IAM amministrative

**File/metodi:** `Infrastructure/Repositories/InstitutionMembershipMutationRepository.cs`, `MutateAsync`, `LockMembershipsAsync`, `IsCurrentAdmin`; parziale `.Acquisition.cs`, `AcquireProfileAsync`; `Domain/Authorization/MembershipAuthorizationRules.cs`, `Matches`; `Application/Managers/InstitutionMembershipAdminManager.cs`.

**Evidenza statica verificata:** l'autenticazione HTTP controlla IdentityGeneration prima di entrare nel manager. AdminAsync controlla la membership. Il repository poi acquisisce tenant/membership e ripete Matches, che confronta **SecurityGeneration della membership**, ruoli e stato, ma non la generation globale. Né MutateAsync né AcquireProfileAsync chiamano RequireGenerationAsync; il repository non dipende dal port account. La correzione Exam/Grade invece lo chiama dopo i lock IAM. Il cambio password incrementa solo la generation globale, non tutte le membership.

**Interleaving non escluso dal codice:** richiesta admin autenticata con generation g → arresto prima del mutex IAM → cambio password committato con g+1 → richiesta ripresa, membership invariata → controllo IAM soddisfatto e mutazione/acquisizione ancora eseguibile. Il lock FK eventualmente incontrato dopo la rotazione non rivalida g. Le richieste nuove restano respinte dal controllo HTTP: non è una riutilizzabilità indefinita del vecchio JWT.

**Livello di prova:** assenza del controllo transazionale confermata; **exploit HTTP concorrente non eseguito in questa verifica**. Non è presentato come una race HTTP già riprodotta. È comunque una lacuna concreta rispetto alla revoca forte adottata per le altre scritture privilegiate, da risolvere o escludere con un'invariante dimostrata. **Bloccante per certificare quella garanzia globale**, non prova di IDOR né di acquisizione arbitraria di profili.

**Rimedio/test necessari:** barriera deterministica prima del mutex IAM per sospensione/ruoli e acquisizione; password-prima deve impedire mutazione e audit, IAM-prima deve bloccare la rotazione fino al commit. Rivalidare la generation sotto lo stesso ordine tenant→IAM→identità, anche per replay, se il controesempio è confermato. **Rischio: alto**, nuovi lock su percorsi amministrativi e possibili interazioni con onboarding/recipient acceptance.

### A04 — Medium — Il validatore ignora metadati essenziali del manifest di release

**File/metodo:** `tools/release-artifacts.cjs`, ramo di verifica.

**Evidenza eseguita:** [controllo negativo](final-assessment-evidence/manifest-negative-control.cjs), [risultato](final-assessment-evidence/manifest-negative-control.json). Un'intercettazione in memoria della sola lettura JSON cambia format a 999, release a wrong-release e requiredMigrations a lista vuota: il validatore reale conclude ancora «728 canonical release artifacts verified». Nessun file su disco è modificato. Il confronto valida manifest.files, non questi campi.

**Impatto:** oggi i metadati sono corretti, ma la CI non impedisce una futura lista di migrazioni errata; il tooling locale usa requiredMigrations. Gli hash dei file SQL non assicurano l'ordine dichiarato di applicazione. **Non è una migrazione mancante dimostrata nell'artefatto attuale.**

**Rimedio/test necessari:** verificare schema/versione del manifest, release, inventory e sequenza canonica delle migrazioni, con rifiuto di assenza, duplicati e ordine errato; mantenere intatta l'evidenza storica e la normalizzazione EOL. **Rischio: basso**. Correzione del gate raccomandata prima del rilascio; non richiede refactoring applicativo.

## Lock, worker e limiti della prova di assenza di race

Le scritture interattive Exam/Grade acquisiscono tenant **X**, poi membership/ruoli/security state, quindi identità globale mediante RequireGenerationAsync, poi la risorsa. `SaveAsync` richiede l'autorizzazione transazionale per tutti gli attori interattivi. Autosave è nel wrapper. La lettura IAM finale è un current locking read e non anticipa uno snapshot consistente prima dell'attesa sulla submission. I replay restano soggetti ai controlli.

Il worker acquisisce tenant **S prima della submission**, evitando l'inversione parent/submission precedentemente descritta. Non prende in prestito un ruolo o la generation di un utente. La rotazione password prende user/state e non il tenant: nei percorsi esaminati non emerge una nuova inversione diretta con questa rotazione. Le creazioni Exam non richiedono più un upgrade tenant S→X dopo aver acquisito il resource lock. Questa è una verifica di ordine, non una dimostrazione universale di assenza di deadlock tra qualunque trigger e workflow.

Rimane un confine da non sovrastimare: ResolveAsync controlla stato istituzione/holding **prima** della transazione del processor; LockTechnicalTenantAsync blocca l'ID, senza ripetere il predicato di ammissibilità dello stato. La capability conserva owner e IDs, e le query tecniche li filtrano; non è dimostrato un bypass utente. Non è però dimostrata la semantica di un'archiviazione/attivazione della holding committata fra Resolve e lock. Servono interleaving mirati se la garanzia richiesta è «nessuna scrittura tecnica dopo tale transizione», anziché autorizzazione al momento della risoluzione. **Rischio residuo Medium, non nuovo exploit confermato.** Anche RequireLocked omette il confronto IdentityGeneration, mentre Matches del data scope lo include: nessuna sostituzione del principal raggiungibile dall'HTTP è stata dimostrata.

## Database e TLS: requisiti esatti di release

Il graph pubblicato e i lock risolvono MySqlConnector **2.6.1**; Pomelo **9.0.0** ed EF Core **9.0.8** restano invariati. [Graph](final-assessment-evidence/mysqlconnector-graph.json), [restore locked](final-assessment-evidence/restore.log), [audit online](final-assessment-evidence/dependency-audit.json), [gate](final-assessment-evidence/dependency-gate.log). La consultazione corrente dell'[advisory del maintainer](https://github.com/advisories/GHSA-473q-m89c-ghf8) conferma versioni affette <2.6.1 e patch 2.6.1; la CA esplicita è inoltre coerente con la mitigazione documentata.

L'audit ha realmente scaricato index/base/update da nuget.org con HTTPS validato da Node: [richieste upstream](final-assessment-evidence/nuget-relay-requests.log). Il relay è limitato a loopback, solo per questa verifica; nessun bypass dei certificati. Un primo tentativo con sources vuote non è stato usato come evidenza: l'esito valido è quello con auditSources esplicito e gate riuscito. Nessuna vulnerabilità NuGet segnalata da questo audit non implica assenza di vulnerabilità di OS, runtime o server SQL.

Per il database destinato alla produzione occorrono:

1. Build MySQL compatibile **5.7.24**, InnoDB, charset/collation e definizioni del contratto; primary condiviso dalle repliche per IAM, credenziali e quota. Versione/hostname/porta/datadir/database devono corrispondere alle attese della probe. Il solo numero di versione non certifica il supporto TLS del build.
2. Endpoint con **TLS moderno compatibile con .NET 10/Linux**: almeno TLS 1.2, certificato valido per il nome DNS realmente configurato, catena verso la CA esplicita e CA montata leggibile dall'utente del container. VerifyFull e SslCa sono richiesti da RuntimePolicy; nessun downgrade a Preferred/Required/None o callback permissivo. La [documentazione MySqlConnector](https://mysqlconnector.net/connection-options/) distingue VerifyFull, verifica hostname e configurazione dei protocolli.
3. Prova positiva reale dal container con driver 2.6.1, più CA errata, hostname errato, CA assente/non leggibile e server senza protocollo comune respinti. Questi esiti non possono essere dedotti dal fallimento di tutti gli handshake.
4. Bootstrap e quattro additive applicati una sola volta con identità amministrativa separata. DDL 5.7 non transazionali: diagnosi dopo errore, nessuna migrazione HTTP o replay automatico. Dopo A01, estendere il preflight prima di usarlo come certificazione della chiusura.
5. Account API non root con soli privilegi dati necessari e EXECUTE su er_schema_ready, senza CREATE/ALTER/DROP/TRIGGER/CREATE ROUTINE/GRANT. Definer della routine persistente con SELECT sul marker e visibilità dei metadata necessari; per i trigger MySQL richiede TRIGGER al soggetto che ne legge i metadata ([manuale 5.7](https://dev.mysql.com/doc/mysql-infoschema-excerpt/5.7/en/information-schema-triggers-table.html)). Il minimo completo dei grant del definer e l'insieme di CRUD per tutti i workflow devono essere provati con l'identità reale, non dedotti dal run root dei test.

**Attenzione ai definer storici:** il bootstrap contiene trigger con `DEFINER=root@localhost` ed `er_test@localhost`; i test creano tali account. Il preflight normalizza il definer nel confronto dei corpi e non verifica esistenza/grant di ogni proprietario. Il pacchetto SQL non è quindi autonomamente certificato per un server che non possieda questi principal. Occorre un processo esplicito e verificato di provisioning/binding dei definer di release, senza concedere quei privilegi all'API e senza modificare alla cieca gli hash storici. Non è stata eseguita una simulazione di produzione con questi principal assenti.

Il MySQL MAMP locale è compatibile sul piano SQL, ma la precedente prova TLS moderna ha `have_ssl=DISABLED`; valid, wrong-host e wrong-ca falliscono tutte con 1042. Questi risultati sono stati ricontrollati nei file originali e **non dimostrano** il rifiuto selettivo del certificato errato. La prova nativa su TLS precedente non sostituisce la prova .NET/container. **Gate TLS di release OPEN.**

## Password, readiness e autorizzazione del probe

La quota BeginPasswordAttemptAsync usa PK UserId, transazione e FOR UPDATE, finestra 5/60s basata sull'orologio SQL e lease 30s. Il contatore persiste fra scope/host e non usa X-Forwarded-For. EndPasswordAttemptAsync cancella solo la propria lease: un finally vecchio non cancella quella successiva. Una sospensione oltre 30s può sovrapporre lavoro BCrypt; hash atteso/generation/lock proteggono il commit, non un limite assoluto alla CPU concorrente. Il fixed window consente il normale burst vicino al confine; non è una finestra mobile. Tutte le repliche devono usare lo stesso primary. Non emerge un bypass evidente del budget account nel codice esaminato.

La routine ha corpo statico SELECT senza input, SQL dinamico o DDL/DML. READS SQL DATA da solo non è la garanzia: è stato letto il corpo effettivo. Liveness non interroga SQL; readiness usa identità attesa, timeout, CALL e risposta generica. Mancanza di EXECUTE/routine/marker produce 503, senza nomi schema, percorsi o errori SQL al client; i log espongono codici allowlist. L'account API non necessita di visibilità globale InnoDB: tale privilegio è soltanto dell'osservatore di test. Queste proprietà positive non eliminano A01.

## Docker e CI: stato effettivo

**Docker NON ESEGUITO:** nessun comando docker/podman e nessun binario nei percorsi standard controllati; [attestazione](final-assessment-evidence/docker.json). Nessuna immagine costruita o avviata. Non sono verificati utente effettivo del processo Linux, accessibilità CA/segreti, DNS/TLS SQL, startup Production, proxy HTTPS/CORS e probe attraverso la topologia finale.

La lettura del Dockerfile conferma build multi-stage, restore locked, publish Release, `USER $APP_UID`, listener interno 8080 e SQL MT1/MT3 nel contesto. Il bundle bootstrap/additive/manifest è copiato sotto /release/migrations; l'API non è un migratore. La .dockerignore esclude bin/obj, appsettings e formati comuni di segreti. Non c'è HEALTHCHECK Docker: le probe devono essere configurate e provate nell'orchestrazione. Il nome USER nel Dockerfile è intento verificabile, non una misura dell'UID in esecuzione.

È stato ripetuto **publish nativo da contesto sorgente pulito equivalente**, non docker build: [restore packaging](final-assessment-evidence/package-restore.log), [publish](final-assessment-evidence/package-publish.log), [14 risorse embedded con confronto byte per byte e .deps 2.6.1](final-assessment-evidence/package-resources.json). Non sostituisce le prove Linux/container.

La CI source dichiara matrice ubuntu/windows, manifest, unittest Python, restore locked, audit transitive con gate fail-closed, build Release e Unit. Gli equivalenti eseguibili Windows sono stati lanciati localmente. Normalizzazione LF/CRLF e percorsi case-sensitive verificati dal tool; **nessun run Linux o GitHub Actions è stato eseguito/certificato**. Il job SQL è manuale su runner Windows dedicato e crea una nuova istanza; la regressione locale è equivalente come suite/database, non esecuzione di quel workflow.

Residui CI: il job source non invoca esplicitamente il guard CodeOrganization, sebbene eseguito qui; manca un job di build/smoke Docker/TLS. Nel blocco pwsh mysql, il comando node del manifest non ha un controllo immediato LASTEXITCODE come restore/build: rafforzare il comportamento fail-closed dei comandi nativi. Il job source separato continua comunque a fallire sul manifest, quindi non si afferma che l'intero workflow accetti già un manifest errato. A04 riguarda invece metadati effettivamente ignorati dal verifier.

## Build, test e valutazione

[Build Release con analyzer](final-assessment-evidence/build.log): **0 errori, 10 warning nullable preesistenti nei test**. [Unit](final-assessment-evidence/unit.log): **590 Passed, 0 Failed, 0 Skipped**. [Tooling Python](final-assessment-evidence/python.log): **18 passati**. [Guard Roslyn](final-assessment-evidence/guard.log): **660 file, 657 tipi, 0 violazioni**. [Manifest](final-assessment-evidence/manifest.log): **728 artefatti**. [git diff --check](final-assessment-evidence/diff-check.log): exit 0; gli avvisi EOL del working tree non sono errori whitespace.

Nuova regressione SQL/HTTP **1502 Passed, 0 Failed, 0 Skipped**, durata 7m02s. Discovery/TRX coincidono per tutti i **590 Unit e 1502 Integration**, senza nomi rimossi, rinominati o molteplicità cambiate rispetto alla remediation: [riepilogo finale](final-assessment-evidence/test-summary-final.json), [riconciliazione](final-assessment-evidence/reconciliation.log). La riconciliazione usa multiset case-sensitive, nomi normalizzati Unicode, molteplicità e hash delle TRX; confronta la baseline finale della remediation, non solo il conteggio dichiarato. Comprende i test QualityRevocation, worker/finalize, quota su due host, marker/EXECUTE, fallback e ownership autosave. Le TRX SQL complete sono nella nuova directory privata. Nessuna percentuale coverage, scansione immagine, SonarQube o benchmark di carico è dedotta dai Passed.

| Dimensione | Voto /30 | Motivazione |
|---|---:|---|
| Architettura | 25 | Confini e capability tecniche preservati, fallback chiuso; data scope complesso ma nessun motivo concreto per una nuova riscrittura. |
| Sicurezza | 21 | Revoca Exam/Grade migliorata; controllo globale IAM da dimostrare, TLS release non certificato. |
| Manutenibilità | 24 | Diagnostica e ownership migliorate; dispatcher, piccoli residui ct e warning test restano. |
| Persistenza | 22 | Lock/snapshot e SQL reale verificati, ma gate additivo incompleto e definer di release da validare. |
| Affidabilità | 18 | Nuovi controlli negativi falliscono; Docker/Linux e topologia release non provati. |

**Media con pesi uguali: 22/30.** La variazione limitata rispetto a 21/30 riconosce le correzioni effettive, senza compensare un gate operativo falso positivo con il numero dei test verdi.

Per passare a una nuova decisione di release occorre chiudere A01 e A02, riprodurre/escludere rigorosamente A03, rafforzare A04, verificare definer/privilegi della destinazione e superare TLS moderno e smoke test Docker/Production con probe reali. Ottimizzazione della contesa tenant, benchmark rappresentativi, maggiore pulizia del data scope e warning nullable sono miglioramenti separati; non giustificano un nuovo refactoring generale. **L'esito corrente resta NOT READY.**
