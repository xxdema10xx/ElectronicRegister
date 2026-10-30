# ElectronicRegister — remediation dell'audit F01–F12

30 ottobre 2026. Ambito: correzioni mirate del working tree auditato, senza riavviare il refactoring generale. Le modifiche pregresse sono conservate. Nessun commit, push, reset, clean, deployment o intervento su database preesistenti alla sessione.

**Il backend non riceve automaticamente una nuova valutazione.** Il 21/30 resta il voto storico dell'audit indipendente. La correzione della dipendenza vulnerabile è verificata; la certificazione TLS del build MySQL destinato al rilascio resta un gate aperto. Docker non è disponibile: è verificato il publish da contesto pulito equivalente e il contenuto delle risorse, non l'esecuzione dell'immagine.

## Evidenze e riproducibilità

Evidenze nuove in [`remediation-evidence/`](remediation-evidence/). Le evidenze originali dell'audit e del refactoring non sono sovrascritte. I tentativi falliti sono conservati e non conteggiati come passati. Le TRX SQL e i log che possono contenere dati sintetici/credenziali di fixture rimangono nelle directory private delle sole istanze create per questo lavoro; il riepilogo pubblico contiene contatori, discovery, nomi, molteplicità e SHA256, senza connection string o password.

- [Build completa con analyzer SDK](remediation-evidence/build-final.log): zero errori; rimangono i 10 warning nullable preesistenti nei test.
- [Restore locked finale](remediation-evidence/restore-locked-final.log), [audit NuGet online](remediation-evidence/dependency-audit-final.json), [gate dipendenze](remediation-evidence/dependency-gate-final.log). Nessun aggiornamento generale di EF Core/Pomelo: cambia MySqlConnector 2.4.0→2.6.1, reso riferimento diretto Infrastructure, con propagazione dei lock.
- [Guard Roslyn](remediation-evidence/guard-final.log): 660 sorgenti, 657 tipi, zero violazioni. Il nuovo sorgente applicativo è l'eccezione tipizzata del limite credenziali.
- [Manifest canonico](../../migrations/release/manifest.json) e [verifica](remediation-evidence/release-manifest-final.log), con controllo case-sensitive dell'inventario e della normalizzazione LF/CRLF.
- [Preflight SQL finale in sola lettura](remediation-evidence/preflight-final.log), verifica delle 66 definizioni base di tabella, 174 trigger base e prerequisiti additivi.
- [Program reale con schema incompleto](remediation-evidence/release-readiness-incomplete.json): live 200, ready 503; [schema completo](remediation-evidence/release-readiness-complete.json): live/ready 200.
- [Publish finale da contesto pulito](remediation-evidence/package-publish-final.log) e [14 risorse SQL embedded verificate byte per byte, con driver pubblicato 2.6.1](remediation-evidence/package-resources-final.json). Nessun segreto runtime o bin/obj del repository viene copiato nel contesto.
- [Test Python di sicurezza/tooling](remediation-evidence/python-tests-final.log): 18 passati. È stato individuato un Python 3.13 installato; nessuna installazione o modifica di profili di sistema.

| Suite finale | Discovery / TRX | Passed | Failed / Skipped | Confronto con baseline |
|---|---:|---:|---:|---|
| Unit | 590 / 590 | 590 | 0 / 0 | Nessun caso rimosso/rinominato |
| Integration SQL/HTTP | 1502 / 1502 | 1502 | 0 / 0 | Tutti i 1458 casi precedenti presenti, 44 nuovi |

[Riconciliazione nominativa e delle molteplicità con hash TRX](remediation-evidence/test-summary-final.json). Il confronto usa multiset case-sensitive e normalizza gli escape Unicode, comprese coppie surrogate; non si basa sui soli contatori. La suite finale è eseguita su MySQL 5.7.24, nuova porta 13448 e nuova datadir `instance-1f2bcb9002e942c896d9308e0404b362`. Dopo il primo verde completo 1502/1502 sono stati rafforzati altri tre osservatori preesistenti: passati 176 casi pertinenti, poi rieseguita l'intera suite definitiva su un'ulteriore istanza nuova. Nessuna asserzione di sicurezza è stata sostituita da un errore generico.

Il risultato nominativo finale di Unit/Integration e il confronto con la baseline sono nel riepilogo finale delle verifiche. Lo scan online usa advisory effettivamente scaricati da nuget.org, non una configurazione a sorgenti vuote. Schannel nel sandbox non riusciva a creare credenziali TLS verso nuget.org: Node/OpenSSL verifica l'HTTPS upstream e inoltra i JSON attraverso un relay temporaneo limitato a loopback. Il nupkg 2.6.1 è scaricato via HTTPS verificato; NuGet controlla i lock. [Richieste upstream](remediation-evidence/nuget-relay-requests.log). Il relay non è un componente di produzione.

Le policy PowerShell bloccavano l'esecuzione dei runner `.ps1` in questa sessione. Non sono state cambiate policy, ACL di sistema o impostazioni TLS. Le stesse operazioni di provisioning sono state eseguite con comandi nativi e helper Node, con directory nuove, ACL private, porta libera, verifica SQL di versione/porta/datadir prima delle scritture e fasi apply-once. Sono state modificate soltanto le ACL delle directory private appena create. Nessun contenuto di script bloccato è stato valutato con dot-sourcing/Invoke-Expression o Bypass.

Le quattro istanze create dalla sessione (porte 13445–13448) sono state tutte arrestate dopo verifica SQL esatta di versione, porta e datadir; dati, log e TRX sono conservati. [Attestazione di arresto](remediation-evidence/shutdown-verification.json). Nessuna istanza preesistente è stata usata o arrestata. Il [manifest storico è invariato](remediation-evidence/change-scope.json); il diff Git generale comprende anche il refactoring pregresso e non viene attribuito interamente a questa remediation.

## Risultato per finding

### F01 — FIXED — Chiusura degli artefatti di release

**Causa confermata:** il manifest storico certificava un insieme precedente di sorgenti e percorsi; il gate CI lo trattava come corrente. Inoltre le SQL account non erano tutte versionabili a causa di `*.sql` in `.gitignore`, e il contesto Docker ometteva le risorse embedded esterne a src.

**Intervento e file:** il manifest storico resta invariato; `migrations/release/manifest.json` identifica `er-quality-1`, la chiusura corrente e l'ordine delle migrazioni. `tools/release-artifacts.cjs` verifica insieme dei percorsi, hash del testo con LF/CRLF equivalenti, file non ignorati, contratto di readiness e input SQL del Dockerfile. `tools/verify-project-artifacts.py` usa il nuovo verifier. `.gitignore` autorizza soltanto i quattro file account canonici. `infra/Dockerfile` e `.dockerignore` includono MT1/MT3 e consegnano bootstrap/additive/manifest in `/release/migrations`. CI source è configurata per Windows e Linux; il job MySQL usa creazione e binder di istanza nuova e `tools/Invoke-QualityIntegration.ps1`, senza dipendere da un file ignorato in docs/validation. Il percorso mysqldump è parametrico.

**Prove:** verifier canonico, 18 test Python, restore/publish da contesto pulito senza obj/bin e lettura PE delle 14 risorse attese. Il verifier distingue errori di percorso e case e controlla LF/CRLF senza alterare i byte SQL storici.

**Residui:** job GitHub Actions e runtime Linux/Docker non eseguiti da questo ambiente. La normalizzazione è verificata localmente; non viene presentata come un run Linux. La firma/provenienza dell'immagine finale resta un obbligo del successivo rilascio.

### F02 — FIXED — Linearizzazione IAM/identità e scritture Exam/Grade

**Causa confermata:** la rivalidazione transazionale valeva solo per Staff e autosave apriva la propria transazione fuori dal wrapper. La nuova prova sospende una richiesta dopo `Open`/factory e committa la revoca da un'altra connessione prima di lasciarla scrivere. Prima della correzione sono state osservate scritture riuscite di Teacher/Admin dopo la sospensione. La prima matrice di riproduzione contiene anche due setup non validi per domande obbligatorie: quei due fallimenti non sono prove della race; il setup è stato corretto conservando i risultati originali.

**Intervento e file:** `InstitutionExamManager.AuthorizedTransaction` applica il lock a tutti gli attori interattivi e autosave usa quel confine. `IInstitutionWriteAuthorization.LockExamAsync` rende esplicito il mutex tenant esclusivo prima delle altre acquisizioni, evitando upgrade S→X nella creazione esami. `InstitutionExamDataScope.SaveAsync` esige il contesto transazionale per ogni attore interattivo, non solo Staff.

`InstitutionWriteAuthorization.ReadLockedAsync` legge stato/ruoli/generation con una query corrente sotto lock e riusa `MembershipAuthorizationRules.Matches`. Una proiezione EF consistente in quel punto avrebbe fissato lo snapshot REPEATABLE READ prima dell'attesa sulla submission. La verifica iniziale del manager resta, ma non sostituisce questa verifica corrente.

La prima matrice estesa ha effettivamente individuato una nuova inversione con il worker: un attore interattivo poteva tenere il tenant mentre il worker teneva la submission e attendeva la FK del tenant. `LockTechnicalTenantAsync` fa acquisire al worker il parent condiviso prima della submission, mantenendo la capability opaca e senza inventare un utente/ruolo tecnico. Le due direzioni manuale/automatico tornano a osservare lo stato committato e a produrre un solo voto/audit. Nessun retry generale viene usato per nascondere deadlock.

**Generation globale separata:** `TenantContext` conserva la generation identità autenticata; `InstitutionAuthentication.ParseTenant` la trasmette. Il lock di scrittura usa `IIdentityAccountRepository.RequireGenerationAsync` sui record globali, dopo il confine IAM e prima delle letture business. Il cambio password blocca soltanto l'identità e non inverte l'ordine tenant→IAM→identità→risorsa. Le prove comprendono revoca per password dopo Open e cambio password in attesa di una scrittura autorizzata. I contesti tecnici/test non HTTP non vengono trasformati in sessioni utente inventate.

**Prove:** `QualityRevocationTests` copre update/publish, start/autosave/submit, review/finalize e modifica voto con Teacher/Student/Admin/Staff; sospensione e rimozione ruolo, entrambe le direzioni di commit, snapshot invariato quando revoca vince. La direzione scrittura-prima richiede un vero arco di attesa InnoDB, non un ritardo. `Phase3_ConcurrencyTests`, `FinalizeConcurrencyTests`, lifecycle/worker e holding sono mantenuti. Dopo la correzione di snapshot/ordine sono passati 137 casi mirati; il run completo finale comprende anche queste prove.

**Residui:** mutex tenant esclusivo per le scritture esami/voti aumenta la contesa nello stesso tenant. La correttezza è verificata; il throughput in produzione non è certificato. Una futura riduzione della granularità richiede nuove prove di ordine/snapshot; non va rimossa la rivalidazione per recuperare prestazioni.

### F03 — MITIGATED — Driver corretto; certificazione TLS di release OPEN

**Causa confermata:** MySqlConnector 2.4.0 transitivo aveva l'advisory GHSA-473q-m89c-ghf8. VerifyFull senza CA esplicita non dimostrava la mitigazione richiesta.

**Intervento e file:** riferimento diretto 2.6.1 nel csproj Infrastructure e lock rigenerati; EF Core 9.0.8 e Pomelo 9.0.0 restano nelle versioni precedenti. RuntimePolicy richiede anche `SslCa` in Staging/Production; i test di configurazione distinguono questa politica dalle connessioni locali di test.

**Prove:** restore locked valido e vulnerability audit online, con gate che rifiuta errori e advisory. Il driver aggiornato esegue la regressione SQL/HTTP reale su MySQL 5.7.24. Nessun callback permissivo, bypass di CA o impostazione TLS obsoleta del client viene introdotto.

**Limite concreto:** il build MAMP 5.7.24 disponibile espone TLS 1.0/1.1. Un tentativo più forte TLS 1.2, eseguito soltanto sull'istanza privata creata per la verifica, registra `TLS version is invalid` e disabilita SSL. Il client aggiornato rifiuta la connessione. La prova nativa precedente conferma il controllo del nome su TLS vecchio, ma **non certifica TLS moderno del driver**; la prova driver positiva con CA valida non è passata. I file `tls-server-capability.log`, `tls-server-final-capability.log`, `tls-driver*.json` e i log privati conservano questa distinzione. I rifiuti dovuti all'assenza di un protocollo comune non sono presentati come prove riuscite del solo controllo CA/hostname.

**Rischio residuo/blocco:** prima del rilascio serve un build SQL 5.7.24 capace di TLS moderno, oppure una soluzione di trasporto certificata separatamente, con connessione reale VerifyFull valida e CA/hostname errati respinti. Non è stato modificato un endpoint esterno o riabilitato TLS debole in .NET. Il pacchetto vulnerabile è corretto; la release TLS non è dichiarata pronta.

### F04 — FIXED — Readiness, liveness e preflight separati

**Causa:** identità SQL corretta non implicava presenza delle nuove tabelle o dei trigger.

**Intervento e file:** migrazione additiva `004_release_schema.sql`, generata/verificata da `tools/quality-schema-contract.cjs`; routine read-only `er_schema_ready`, marker `er-quality-1` e controllo di 71 tabelle/180 trigger/colonne/FK additive. `OperationalPipeline` verifica identità, chiude il reader precedente e chiama la routine con timeout. `release-preflight.cjs` esegue la verifica estesa, senza DDL. Il runbook spiega SQL SECURITY DEFINER ed EXECUTE senza concedere DDL all'API.

**Prove:** Program reale prima/dopo le additive su nuova istanza: 503/200 della readiness con liveness sempre 200. `Quality_ReadinessRequiresReleaseMarkerAndExecutePermission` verifica marker mancante, ripristino e account SELECT senza EXECUTE. Preflight verifica definizioni base e chiusura additiva.

**Residui:** la readiness controlla prerequisiti/versione, non l'integrità completa di tutti i corpi SQL contro un amministratore malevolo. Il preflight confronta le definizioni base; la verifica degli artefatti e il processo di migrazione restano separati. Proprietario della routine e relativi privilegi devono essere configurati correttamente.

### F05 — FIXED — Ordine totale e offset dei voti

**Causa:** parità Date/ClassSubject/Value/Student non risolta e prodotto int dell'offset non validato.

**Intervento:** `GradeRepository.GetPagedAsync` aggiunge Id come tie-breaker e valida offset long prima del cast a int.

**Prove:** `Quality_GradePaginationHasTotalOrderAndRejectsOverflow`: quattro voti con la stessa tupla distribuiti su pagine di una riga, nessuna perdita/duplicazione, ordine confrontato con SQL e richiesta int.MaxValue→400.

**Residui:** offset paging non fornisce uno snapshot tra richieste HTTP diverse quando i dati cambiano. Un futuro keyset resta un'evoluzione di contratto, non parte di questa correzione.

### F06 — FIXED — Cancellazione sui confini interattivi

**Causa:** contratto IInstitutionExamManager senza ct e autorizzazione con CancellationToken.None.

**Intervento:** token opzionale nel contratto, propagazione da RequestAborted dei tre controller, Open/profili, factory bound, query/lock e Save dei repository esami/voti e collaboratori. La transazione autorizzata conserva il token della richiesta. Una volta terminata con successo l'azione atomica, TransactionManager completa il commit con token indipendente; rollback/cleanup non sono cancellati. Il worker mantiene il proprio token.

**Prove:** `Quality_CancellationBeforeRepositoryCreationNeverWrites`, build dei consumer e regressione dei workflow. Le chiamate CLR esistenti restano sorgente-compatibili tramite parametro opzionale; non si promette compatibilità binaria dei vecchi assembly.

**Residui:** il test esplicito copre cancellazione anticipata; la regressione verifica rollback/timeouts e lock. Non è stato misurato il tempo di rilascio di ogni query sotto disconnessione di rete reale. Commit già iniziato può comunque riuscire dopo la disconnessione del client: usare gli identificativi idempotenti per riconciliare l'esito.

### F07 — FIXED — Fan-out rimosso preservando snapshot e lock

**Causa confermata:** collezioni sorelle Questions/Options e ExamStudents nello stesso join.

**Intervento:** `ExamRepository.GetByIdAsync` usa split soltanto quando entrambe le collezioni sono richieste e crea uno snapshot RepeatableRead se manca una transazione; riusa la transazione del chiamante. Il percorso bound `GetByIdForUpdateAsync` mantiene il lock iniziale del parent e legge le collezioni separate nella stessa transazione.

**Prove:** `Quality_ExamFanoutMeasurementPreservesShapeAndSnapshot`: 20×2×40 produce realmente 1.600 righe nel join MySQL; confronto delle due forme nello stesso snapshot e controllo del risultato canonico HTTP. Un primo cold-run osserva circa 37,6 ms single e 48,0 ms split: **non è dimostrato un miglioramento di latenza**. La modifica evita la moltiplicazione delle righe/memoria; comporta più round trip. I test di lifecycle/lock/replay sono parte della regressione finale.

**Residui:** misura illustrativa a una ripetizione, senza carico di produzione o percentili. La proiezione ordinaria senza entrambe le collezioni rimane single query. La scelta non autorizza a impostare split query globalmente sulle letture IAM.

### F08 — FIXED — Quota account distribuita per PUT password

**Causa:** limite globale limitato a POST/per-IP, cambio password PUT con BCrypt escluso.

**Intervento e file:** `003_password_attempt.sql`, metodi Begin/EndPasswordAttemptAsync di IdentityAccountRepository, orchestration try/finally di IdentityAccountManager, IdentityRateLimitException e mapping 429/Retry-After. La chiave è UserId globale, non JWT jti/IP/istanza. Cinque tentativi per 60 secondi; lease per account condivisa sul primary SQL prima del lavoro BCrypt, rilascio con token non cancellabile. Nessun servizio esterno aggiunto.

**Prove:** `SharedQuotaSerializesReplicasAndPersistsAcrossScopes` e `Quality_PasswordPutBudgetIsSharedByTwoHostsAndCannotBeSpoofedByProxyHeaders`: concorrenza reale, cinque tentativi, sesto 429, due host e header proxy variabili. I test dei proxy fidati restano in Mt6RuntimeTests.

**Residui:** una lease scade dopo 30 secondi per recuperare un processo morto; una pausa molto lunga può sovrapporre lavoro CPU. Hash atteso/generation/lock impediscono doppio commit delle credenziali. Tutte le repliche devono scrivere sullo stesso primary, non su database indipendenti.

### F09 — MITIGATED — Fallback chiuso; hotspot conservato consapevolmente

**Causa confermata:** Query<T> concedeva Owned<T> per qualunque tipo sconosciuto al dispatcher.

**Intervento:** Subject è esplicito, gli altri tipi non previsti sono respinti. Il confronto dei bound principal include anche security/identity generation. SaveAsync mantiene la whitelist tecnica e il controllo owner, rafforzati dalla rivalidazione per tutti gli attori.

**Prove:** `Quality_UnknownEntityCannotUseTenantOnlyExamFallback`, matrice ruoli/ownership/revoca/worker e guard architetturale.

**Residui/decisione:** non è stato riscritto il dispatcher typeof né spostata tutta la policy di scrittura fuori dalla persistenza. È un hotspot reale, ma una grande estrazione dell'engine in questa remediation aumenterebbe il rischio senza essere necessaria a chiudere il fallback. Nessuna dichiarazione di completa eliminazione della complessità o dei cloni semantici.

### F10 — FIXED — Diagnostica strutturata senza messaggi sensibili

**Causa:** solo nome del tipo, indistinguibilità degli errori provider e rollback che poteva mascherare il timeout originario.

**Intervento:** GlobalExceptionHandler registra codici allowlist DB_DEADLOCK/DB_LOCK_TIMEOUT/DB_SCHEMA_MISSING/DB_PERMISSION/REQUEST_FAILURE, numero provider/SQLSTATE e posizione CLR senza messaggio, SQL, parametri o percorsi sorgente. La readiness registra una fase/codice sicuro. TransactionManager conserva l'eccezione originaria se il rollback fallisce perché la connessione è già Broken/Closed.

**Prove:** `QualitySafeLoggingTests`, test HTTP con segreto sentinella e test worker di hard-timeout/rollback/recovery. Il vecchio test sul testo esatto del log è aggiornato al nuovo codice diagnostico, mantenendo integralmente i divieti di leakage e le verifiche degli header.

**Residui:** non si promette uno stack trace completo; maggiore dettaglio richiede un sink riservato e una revisione specifica della redazione. Le metriche non aggiungono label utente/tenant/query.

### F11 — FIXED — Attese SQL osservate e controllo del mutante

**Causa:** !task.IsCompleted dopo 100–250 ms non dimostrava l'arrivo del concorrente al lock.

**Intervento:** SqlLockEvidence richiede INNODB_LOCK_WAITS con transazioni distinte e marker della risorsa o ID del blocker. Timeout è solo watchdog. Polling ogni 150 ms evita che la cache delle tabelle InnoDB di MySQL 5.7 resti su una fotografia vuota; non costituisce la prova. Aggiornati Staff, BulkImport, Calendar, Email, FormativePlan, security state e Phase3.OrderedAsync. Quest'ultimo osserva il blocker reale e non presume che il primo comando contendibile resti un ReaderExecuting sulla submission.

**Prove:** direzioni write/revoke, manuale/worker e review/finalize; `Quality_LockEvidenceRejectsMissingIamLockMutant` inietta una versione test-only senza lock, committa la revoca e richiede che l'osservatore rifiuti l'assenza dell'arco di attesa. La scrittura del mutante dimostra l'effetto della proprietà rimossa; quel mutante non è registrato in produzione.

**Residui:** privilegi di osservazione InnoDB richiesti solo all'account di test. Questi test non sono benchmark di fairness né prova di assenza di qualunque possibile deadlock del sistema.

### F12 — FIXED — Ownership della transazione autosave

**Causa:** transazione locale senza DisposeAsync deterministico.

**Intervento:** await using sulla sola transazione creata localmente da ExamSubmissionRepository; nessuno smaltimento della transazione ambientale. Nel percorso HTTP autosave è ora anche soggetto al confine IAM esterno.

**Prove:** `Quality_AutosaveDisposesOnlyItsOwnedTransaction`: salvataggio locale, replay revisionato, CurrentTransaction nullo dopo entrambi, transazione del chiamante ancora posseduta dal chiamante e rollback osservato sul DB.

**Residui:** nessuna garanzia sul riuso di una connessione resa Broken da un guasto di rete; è il provider a rimuoverla dal pool.

## Rischi e gate che rimangono

Il rilascio richiede ancora la prova TLS moderna sul build SQL di destinazione e l'esecuzione reale dell'immagine Docker nella topologia HTTPS/proxy certificata. Non sono stati aggirati per ottenere test verdi. Le funzionalità corrette sono verificabili indipendentemente mediante le evidenze e il [runbook di release](RELEASE_READINESS.md).

La granularità del lock tenant e la lease credenziali hanno trade-off espliciti; F09 mantiene debito di complessità. I warning nullable nei test preesistenti restano visibili. Nessuna percentuale di coverage, risultato SonarQube o scansione dell'immagine viene inventata. Un audit indipendente successivo deve riesaminare le correzioni e decidere un eventuale nuovo voto.
