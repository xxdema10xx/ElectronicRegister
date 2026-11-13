# MySQL 8.4.13 â€” er-quality-2-mysql84

## Stato corrente dopo il completamento del codice

**Decisione finale: BLOCKED â€” implementazione e gate automatizzati presenti, certificazione runtime non eseguita.** Non Ã¨ uno stato COMPLETED o READY. Le sezioni dell'audit iniziale conservate in fondo descrivono la consegna precedente; gli stati e i risultati correnti sono quelli di questa prima parte.

Branch: `refactor-database-entities`. SHA iniziale e finale HEAD: `8e7d9ecd42c001f1fa9b30e120535ce1bb0ef6e8`. Le modifiche sono esclusivamente nel working tree: nessun nuovo SHA di release Ã¨ stato creato, nessun commit/push e nessun accesso EC2. Le modifiche non committate precedenti sono state mantenute e sviluppate. SQL, schema-contract e manifest storici non sono stati riscritti.

## 1. Correzioni implementate

- Le 22 sostituzioni sui 19 trigger sono ora parte della release distinta **er-quality-2-mysql84**. I pattern usano `REGEXP_LIKE(CONVERT(... USING utf8mb4) COLLATE utf8mb4_bin, ..., 'c')`, mantenendo negazioni e contesto. L'ancoraggio finale Ã¨ `\\z` nel testo SQL, che diventa `\z` per ICU: fine assoluta, inclusa la protezione contro newline finale. Il codice non sostituisce indiscriminatamente BINARY nei confronti ordinari.
- Le due espressioni di `Mt5cOnboardingIntegrity.cs` hanno una variante 8.4 corrispondente, con escaping C# separato da quello SQL. Il ramo 5.7 mantiene esattamente le precedenti regex binarie; non viene passato al server 8.4. Il controllo preventivo riconosce questa sola eccezione storica esplicita. Ãˆ corretta anche la verifica delle undici FK di quel classificatore: default omesso NO ACTION in 8.4, RESTRICT in 5.7, senza accettare indifferentemente entrambe le regole.
- La fixture generale Ã¨ fissata a `mysql:8.4.13@sha256:5cf1e1293ea42e077e6a22114e6773b9aae3bb8f9933a2acd5a9721518ff8f5b`. I test 5.7 con target e retention storici sono conservati; il nuovo gate 8.4 Ã¨ separato e non aggira quei vincoli.
- L'applicazione 8.4 richiede il nuovo marker anche nella readiness; un binario nuovo non puÃ² essere ready sul predecessore non aggiornato. Il gate 5.7 resta invariato. Il Dockerfile include manifest, contratto e corpi della nuova release accanto agli artefatti storici.
- `tools/mysql84-release.cjs` genera/verifica una nuova closure di **1101 file**, con sette artefatti storici ritirati esplicitamente classificati. Il contratto comprende 71 tabelle, 180 trigger, 200 FK e 355 componenti FK. Conserva il manifest precedente e ne verifica i 760 artefatti rispetto ai blob Git della revisione pubblicata.
- Il controllo preventivo rifiuta anche forme semplici di CAST AS BINARY, CONVERT USING BINARY, RLIKE e REGEXP_LIKE con argomenti binari. Non Ã¨ un parser SQL generale: le forme nuove non supportate richiedono revisione.

Il nuovo preflight Python confronta schema/inventario, DDL delle tabelle, ogni corpo di trigger, SQL mode, charset/collation, DEFINER, **ACTION_ORDER**, routine e parametri, metadati additivi e tutte le FK/componenti. Verifica account definer bloccato, plugin caching_sha2_password, grant esatti di definer/API, assenza di ruoli/grant ulteriori e requisito SSL dell'API sullo staging. Le aspettative vengono dai contratti versionati, mai apprese dal server.

## 2. Fresh install: stato e comando

**Implementata; non certificata su MySQL reale.** Il bundle viene derivato dagli input storici verificati, adattato per 8.4 e corretto prima della creazione dei trigger. Il marker nuovo viene aggiunto senza modificare quelli precedenti. La creazione richiede database vuoto e login API/reviewer giÃ  predisposti, come nel precedente generatore. Principali, grant e DEFINER restano quelli del binding esplicito.

```sh
node tools/mysql84-release.cjs
node tools/mysql84-release.cjs bundle /private/mysql84-config.json /private/NEW-bundle
python3 tools/mysql84_release.py install --config /private/mysql84-config.json --journal /private/NEW-install-journal.json --bundle /private/NEW-bundle
```

Il runner verifica i checksum e ricostruisce indipendentemente il bundle atteso: modificare insieme SQL e hash nel binding non autorizza SQL arbitrario. Le unitÃ  del bootstrap sono journaled prima dell'esecuzione; un bootstrap parziale viene rifiutato senza replay. La seconda installazione completata verifica e non riesegue SQL. Un bootstrap interrotto all'interno di un file storico richiede riconciliazione manuale: il recupero automatico dei singoli trigger descritto sotto riguarda l'upgrade incrementale, non il replay di un file bootstrap multi-DDL.

## 3. Upgrade incrementale e recupero implementati

**Implementati; non certificati su MySQL reale.** Non viene richiamato `host.py provision`. Il nuovo runner verifica il predecessore completo prima di qualsiasi DDL. Ãˆ distinto dal vecchio journal: usa un nuovo file privato, senza aggiornare le registrazioni giÃ  applicate.

```sh
python3 tools/mysql84_release.py predecessor-preflight --config /private/mysql84-config.json
python3 tools/mysql84_release.py upgrade --config /private/mysql84-config.json --journal /private/NEW-upgrade-journal.json
python3 tools/mysql84_release.py preflight --config /private/mysql84-config.json
# Soltanto dopo un arresto parziale e la diagnosi dell'operatore:
python3 tools/mysql84_release.py recover --config /private/mysql84-config.json --journal /private/NEW-upgrade-journal.json
```

Il runner acquisisce un lock di sessione ed entra in manutenzione con `SET PERSIST offline_mode=ON`. La barriera sopravvive anche a un riavvio MySQL. Rifiuta altre sessioni amministrative, transazioni pendenti, applier di replica, eventi schedulati e, fuori dalla CI, altri database applicativi sul server. PROCESS e SHOW DATABASES sono necessari per evitare una falsa attestazione basata su metadati parzialmente visibili. Il server UUID, il database, il modo SQL globale e, sullo staging, l'identitÃ  `er_stage_control.identity`, devono corrispondere al binding.

Si usano i privilegi amministrativi giÃ  disponibili all'operatore, per esempio il root locale via socket del container dedicato. **`er_migrator` della release precedente non dispone dei privilegi di manutenzione necessari e non viene elevato automaticamente. `er_api` non riceve alcun nuovo privilegio.** L'esempio `infra/release.mysql84.example.json` descrive l'uso del socket esistente attraverso un container ID locale esplicito; non espone root via rete. In alternativa Ã¨ supportato TCP con VERIFY_IDENTITY e CA esplicita. Il solo servizio CI effimero usa loopback, porta casuale e credenziali sintetiche senza TLS.

Ogni DROP e CREATE ha un intent durabile, checksum e stato nel nuovo journal; scrittura del file e directory sincronizzate su Linux. Le clausole PRECEDES/FOLLOWS conservano l'ordine originale. Una ripresa ordinaria con stato parziale viene rifiutata. `recover` riconcilia l'intero schema atteso misto precedente/nuovo e gestisce separatamente:

- DROP commesso con acknowledgement perduto: conferma l'unica assenza prevista e procede al CREATE, senza ripetere il DROP;
- CREATE commesso con acknowledgement perduto: verifica corpo **e tutti i metadati/ordine dell'intero schema** e registra il completamento, senza ripetere il CREATE;
- stato, checksum, ordine, schema o dati divergenti: arresto con manutenzione mantenuta, senza rollback automatico.

Il fingerprint comprende ogni valore e NULL di tutte le tabelle, conservando anche righe duplicate. Soltanto il nuovo marker Ã¨ escluso dal confronto pre/post perchÃ© Ã¨ l'aggiunta prevista. Dopo i 19 trigger e il confronto dati, il marker Ã¨ inserito in modo riconciliabile e la manutenzione disattivata esplicitamente. Nessun DROP/CREATE TABLE, cambio di collation globale, rollback distruttivo o modifica delle regole IAM.

## 4. CI e validatori storici riconciliati

I cinque validatori verificano ora la provenienza dei byte superseded in Git, con rappresentazione LF/CRLF, e verificano i file correnti rispetto alla nuova closure. I sette percorsi non piÃ¹ presenti sono elencati esplicitamente nel nuovo manifest; non vengono ricreati per simulare la vecchia organizzazione. Il verificatore import controlla l'enum e tutti i sette DTO nelle nuove posizioni, mantenendo UTF-8 rigoroso, limiti e difese XML/XLSX; ignora soltanto differenze di whitespace nei due controlli testuali interessati.

Nessun manifest storico Ã¨ stato aggiornato per far coincidere gli hash. Questi controlli dimostrano provenienza e integritÃ  degli artefatti; le vecchie TRX non sono nuove prove runtime. Il vecchio `release-artifacts.cjs` continua a certificare soltanto la vecchia sorgente. Sul working tree nuovo il comando corretto Ã¨ `mysql84-release.cjs`.

Il workflow staging precedente continua a eseguire i propri test sull'esatto SHA immutabile er-quality-1. Il workflow nuovo copre la release correttiva. `verify.yml` verifica la nuova closure; il job 5.7 esclude soltanto il nuovo test dedicato 8.4, eseguito obbligatoriamente nel suo workflow separato. Nessun precedente test viene eliminato.

## 5. Test realmente eseguiti

| Verifica | Risultato locale |
|---|---|
| Restore locked e build Release .NET | PASS; 0 errori, warning nullable preesistenti nei test |
| Unit test completi | 598 PASS, 0 ignorati |
| StagingCalendarFeatureTests | 2 PASS, contratti offline |
| Test Python tools | 77 PASS, inclusi 9 nuovi test del runner |
| Adapter MySQL 8.4 e negativi esistenti | PASS offline |
| Manifest precedente: controlli negativi | 14 PASS |
| Manifest correttivo: controlli negativi | 10 PASS |
| Bundle fresh e predecessore generati con il renderer reale | PASS offline; checksum, 22 regex, marker, grant e binding verificati; nessuna esecuzione SQL |
| 19 corpi derivati e guardie regex | PASS statico |
| Cinque verificatori istituzionali | PASS; hash storici riconciliati, nessuna nuova prova SQL/HTTP |
| CodeOrganization | 661 file, 658 tipi, 0 violazioni; inventario generato ripristinato per evitare variazioni CRLF estranee |
| Sorgenti Python e whitespace | Compilazione sintattica e diff check eseguiti |

## 6. Test automatizzati preparati, non eseguiti

`tools/mysql84_runtime.py` e il test `Mysql84ReleaseTests` con helper HTTP sono eseguibili nel workflow, ma **non sono stati eseguiti in questa sessione**. Docker restituisce accesso negato al daemon; non Ã¨ stata richiesta escalation nÃ© contattato un database esistente. Actionlint e PyYAML non sono installati localmente; il workflow installa actionlint 1.7.7 e tratta il lint come gate.

La CI predisposta verifica: installazione originale, riproduzione reale di 3995 attraverso INSERT invitation, seed e dump sintetici, drift del predecessore rifiutato prima del DDL, fault dopo DROP e dopo CREATE, perdita della connessione di migrazione e restart del servizio MySQL con verifica della barriera persistita, blocco connessione API durante l'interruzione, rifiuto replay, recupero esplicito, fingerprint dati, idempotenza, bundle alterato con checksum autoaggiornato rifiutato, fresh install diretta e contratto finale equivalente. Esegue DML reali per inviti, eventi/nonce, membership con audit di sospensione/riattivazione e aggiunta/rimozione ruolo, governance/recovery/approval, policy email, import Student/Teacher con item e conferma, piano/versione, profilo docente e calendario. Include hash validi, corti/lunghi, minuscoli, non hex, Unicode, NULL, immutabilitÃ  e tentativi cross-tenant.

Il test .NET invoca il vero classificatore embedded mediante reflection, esegue le regex tramite MySqlConnector e avvia il vero Program/Kestrel con l'account API a privilegi minimi per login, scelta tenant, letture di modulo, mutation membership, policy email, creazione piano e import Student/Teacher. La CI richiede una TRX con un test selezionato ed effettivamente passato; zero test o test ignorati non valgono come successo.

**Limite di copertura da conservare esplicito:** le whitelist DELETE sono protette da `VERSION()='5.7.24'`. Su 8.4 il ramo permissivo Ã¨ intenzionalmente irraggiungibile; la prima guardia membership impedisce l'esecuzione del secondo DELETE trigger. I test verificano cancellazioni reali rifiutate con dati presenti, ordine dei trigger e pattern ICU separatamente. Non viene disabilitata la prima guardia per produrre una copertura artificiale. Il report runtime non deve dichiarare che ogni espressione interna DELETE Ã¨ stata attraversata. La suite dedicata non sostituisce tutte le regressioni storiche di concorrenza, firma piattaforma, HTTP governance/onboarding e completamento lezioni: queste restano ulteriori verifiche prima di una certificazione completa.

## 7. Diff dei file e artefatti

Modificati 13 file tracciati: workflow `verify.yml` e `staging-package.yml`; `OperationalPipeline.cs`; `Mt5cOnboardingIntegrity.cs`; `infra/Dockerfile`; `IntegrationFixture.cs`; `local_test_db.py`; i cinque `verify-institution-*.py`; `verify-project-artifacts.py`.

Aggiunti/sviluppati: directory `migrations/mysql84-remediation` (manifest, contratto, corpi e inventario iniziale); `mysql84-release.cjs`, `mysql84-remediation.cjs`, `mysql84_release.py`, `mysql84_runtime.py`, `historical_artifacts.py`; test Node e Python dedicati; `Mysql84ReleaseTests.cs` e `Mysql84ReleaseHttp.cs`; esempio di binding; workflow MySQL 8.4; questo report. Per il diff testuale usare `git diff` per i tracciati e leggere anche gli artefatti nuovi, che rimangono non tracciati in assenza di commit.

`migrations/bootstrap/001_schema.sql`, `migrations/bootstrap/schema-contract.json`, `migrations/release/manifest.json`, migrazioni identity-account e manifest storici non hanno diff. `infra/staging/ops.py` e `host.py` rimangono il runner precedente: non vengono presentati come capaci di aggiornare uno staging esistente. Il runner correttivo Ã¨ il comando operativo separato sopra.

## 8. Esecuzione del workflow e prossimo rilascio

Il workflow si chiama **MySQL 8.4 corrective release and upgrade**, file `.github/workflows/mysql84-remediation-candidate.yml` (nome del file precedente conservato). Scatta su push al branch, PR verso il branch e workflow_dispatch. Richiede checkout completo per la provenienza storica, servizio MySQL fissato al digest staging, .NET 10 e client MySQL. Non usa segreti AWS o EC2. Dopo la pubblicazione delle modifiche da parte dell'utente, eseguirlo da Actions sul branch corretto; non Ã¨ stato eseguito alcun push o dispatch da questa sessione.

Gli artefatti attesi sono `predicates.out`, snapshot sintetico, fingerprint iniziale, nuovi journal, `runtime-result.json`, `fresh-result.json`, `http-result.json` e `mysql84-release.trx`. L'assenza di una prova, un gate fallito o una TRX incompleta mantengono BLOCKED. La vecchia pipeline staging Ã¨ selezionata esplicitamente sul predecessore, non sulla release correttiva.

Prima di EC2: completare e valutare questi run, coprire le regressioni ulteriori indicate, verificare backup e finestra operativa e pubblicare un artefatto nuovo con identificatore distinto. Preparare una copia privata dell'esempio di binding con UUID, container ID, stage ID e host pattern reali, senza cambiare quelli esistenti; usare il root **locale** giÃ  previsto per la manutenzione. Eseguire il preflight predecessore, poi il nuovo `upgrade` con journal nuovo, quindi il nuovo preflight e soltanto dopo avviare il binario correttivo. Non sostituire revisioni/hash/journal di er-quality-1 e non tentare un `provision` come upgrade.

## 9. Decisione e rischi residui

**BLOCKED.** Sono presenti implementazioni concrete per installazione, upgrade, recupero e CI. Mancano prove runtime del nuovo codice in questo ambiente e il primo run CI potrebbe evidenziare problemi che richiedono ulteriori correzioni. Non Ã¨ autorizzato il deployment sulla base dei soli test offline.

Restano da eseguire su server reale il restart predisposto durante la manutenzione persistita e tutta la matrice di regressione storica su 8.4, operazioni HTTP di governance/onboarding con firma reale e calendario docente completo. La fault injection predisposta interrompe la procedura e ricrea la connessione; dopo il DROP viene anche riavviato soltanto il servizio effimero creato dalla CI, mantenendo volume e UUID. Il recupero di bootstrap multi-DDL parziale resta manuale e fail-closed. L'uso di offline_mode Ã¨ limitato al server dedicato e ripristinato soltanto dopo verifica: un fallimento mantiene intenzionalmente il servizio non scrivibile.

| ID | IncompatibilitÃ  / gate | Correzione | Test reale in questa sessione | Stato corrente |
|---|---|---|---|---|
| M84-01 | 19 trigger, 22 regex binarie | Release separata, conversione testo, bin/c, fine assoluta | Non eseguito | IMPLEMENTATO, gate runtime aperto |
| M84-02 | Due regex e default FK embedded | Variante 8.4; ramo 5.7 conservato | Non eseguito | IMPLEMENTATO, gate runtime aperto |
| M84-03/04 | Suite e immagine target | Digest esatto e gate SQL/HTTP dedicato | Non eseguito | IMPLEMENTATO, regressione completa da verificare |
| M84-05 | Upgrade e intervallo DROP/CREATE | Journal per intent, ordine, barriera persistita, recover | Non eseguito; 9 unit test offline | IMPLEMENTATO, non certificato |
| M84-06 | Newline, ASCII, case, NULL | Ancoraggio assoluto e matrici ICU/MySqlConnector | Non eseguito | IMPLEMENTATO, non certificato |
| M84-07/08 | Contratti/metadati 8.4 | Preflight completo, 200 FK e 355 componenti | Non eseguito | Gate runtime aperto |
| M84-09 | Cinque verificatori storici | Provenienza Git, closure nuova e percorsi aggiornati | N/A; cinque verificatori offline PASS | RISOLTO STATICAMENTE |
| M84-10 | Regressione e release readiness | CI eseguibile, marker nuovo e gate negativi | Non eseguito | BLOCKED |

---

# Audit iniziale conservato â€” stato precedente, superato dalle sezioni sopra

Data: 2026-11-02. Stato: **INCOMPLETO â€” DEPLOYMENT BLOCCATO**.

Questa consegna contiene inventario statico, corpi candidati per tutti i 19 trigger e sonde CI isolate. Non contiene ancora una release installabile/aggiornabile certificata. Non esiste evidenza runtime prodotta in questa sessione per MySQL. Il criterio di completamento richiesto non Ã¨ raggiunto.

## Revisioni e preservazione

- Branch iniziale e finale: `refactor-database-entities`.
- SHA iniziale: `8e7d9ecd42c001f1fa9b30e120535ce1bb0ef6e8`.
- SHA finale HEAD: `8e7d9ecd42c001f1fa9b30e120535ce1bb0ef6e8`; le aggiunte sono nel working tree, senza commit. Lo SHA non identifica queste aggiunte.
- Working tree inizialmente pulito. Nessun commit, push, accesso EC2, intervento Nginx, connessione a database o modifica di volumi.
- `migrations/release/manifest.json`, bootstrap SQL, schema-contract e migrazioni storiche rimangono invariati. Il controllo canonico originale verifica ancora 760 artefatti, con normalizzazione LF/CRLF prevista dal contratto.
- Nessun privilegio aggiunto a `er_api`, nessun DEFINER sostituito e nessun journal applicato modificato.

## Inventario e limiti dell'audit

`migrations/mysql84-remediation/inventory.json` registra 1049 file tracciati esaminati automaticamente e 2651 righe segnalate, con percorso, numero di riga, categorie, estratto e SHA256 della riga. Ãˆ una scansione lessicale, non una prova che ciascun risultato sia SQL eseguibile. Gli estratti sono limitati a 240 caratteri; consultare il file originale per la riga completa. Le esclusioni riguardano TestResults, directory evidence, node_modules e packages.lock. Il file elenca esattamente gli input della scansione iniziale; le nuove aggiunte sono verificate separatamente.

La scansione copre SQL, C#, strumenti Node/Python/PowerShell, configurazioni JSON, progetti e workflow. Cerca regex, BINARY/collation, SQL mode rimossi, vecchi metadati, provider e lock. Non costituisce una verifica esaustiva di tutti i possibili identificatori riservati, della semantica JSON/data o di tutte le conversioni binarie dinamiche.

Revisione mirata: `tools/mysql84-contract.cjs`, `prepare-release-sql.cjs`, `release-preflight.cjs`, `release-additive-contract.cjs`, `test-mysql84.cjs`, `release-artifacts.cjs`, `quality-schema-contract.cjs`; manifest e contratti bootstrap/additivi; `infra/staging/ops.py`, `host.py`, `mysql-image.json`; workflow verify/staging-package; fixture IntegrationFixture e Mt5bSecurityStateTests; query embedded segnalate nell'inventario.

### Codice attivo, storico e condizionale

Il manifest canonico prescrive bootstrap piÃ¹ cinque migrazioni identity-account. I 19 trigger dell'inventario iniziale sono tutti presenti nel contratto bootstrap corrente: non sono soltanto copie archiviate. La release completa attende 71 tabelle e 180 trigger, di cui 174 nel bootstrap.

Le migrazioni dei singoli moduli e candidate MT5 R1/R2/R3 conservano copie storiche. Non appartengono alla sequenza canonica di bootstrap; possono comunque essere richiamate da strumenti o test dedicati. Non sono state modificate.

I trigger INSERT/UPDATE su eventi, inviti, nonce, import e piani formativi appartengono ai percorsi applicativi ordinari. I DELETE con whitelist dei codici istituzione sono condizionali e alcuni servono pulizia/contratti di cancellazione: la presenza di una regex non prova che ogni DELETE la raggiunga. Non Ã¨ stata prodotta copertura runtime di questi rami.

Le classi `Persistence/Migrations/*Integrity.cs` contengono ulteriori query di verifica. La loro presenza non dimostra esecuzione allo startup: il percorso di invocazione completo resta da certificare. In particolare `Mt5cOnboardingIntegrity.cs:23` contiene due ulteriori `NOT REGEXP BINARY` su token/channel hash, oltre ai 19 trigger.

## IncompatibilitÃ  confermate e candidate

### M84-01 â€” argomenti binari nelle regex ICU

Il problema 3995 su `mt5d_event_insert` Ã¨ confermato dalla segnalazione iniziale, non riprodotto in questa sessione. Il manuale [MySQL 8.4, regular expressions](https://dev.mysql.com/doc/refman/8.4/en/regexp.html) specifica il rifiuto degli argomenti binari e la propagazione NULL. Tutti i 19 corpi canonici contengono la stessa categoria vietata: 22 espressioni complessive.

`tools/mysql84-remediation.cjs` deriva una candidata separata identificata `er-quality-2-mysql84-candidate`, stato obbligatorio `BLOCKED_RUNTIME_AND_UPGRADE`. `candidate.json` contiene corpi completi, hash prima/dopo, righe sorgente, SQL mode e charset/collation originali, hash del manifest predecessore e immagine MySQL fissata per digest. Il bootstrap derivato viene controllato in memoria e fingerprintato; non viene installato nÃ© esportato come bundle operativo.

Sostituzioni proposte:

```sql
BINARY value REGEXP 'pattern'
-- candidata
REGEXP_LIKE(CONVERT(value USING utf8mb4) COLLATE utf8mb4_bin, 'pattern', 'c')

value NOT REGEXP BINARY 'pattern'
-- candidata
NOT REGEXP_LIKE(CONVERT(value USING utf8mb4) COLLATE utf8mb4_bin, 'pattern', 'c')
```

I confronti BINARY non regex rimangono identici. Pattern, negazioni e condizioni circostanti sono conservati byte per byte nei corpi candidati; i test verificano questa proprietÃ . Le classi esadecimali elencano caratteri ASCII; `c` richiede matching sensibile al caso. Non vengono introdotti COALESCE o controlli IS NULL: il valore NULL deve continuare a propagarsi, mantenendo le guardie originali. Le colonne token/hash/nonce interessate sono dichiarate ASCII con collation binaria; non sono dati binari arbitrari. La conversione non certifica da sola l'equivalenza di tutti i casi ICU.

**Rischio aperto:** gli ancoraggi `$`, i terminatori di riga e la conversione Unicode richiedono verifica reale. Il workflow include un caso negativo con newline finale e deve fallire se ICU lo accetta. Non si dichiara la candidata semanticamente equivalente o pronta finchÃ© questo controllo e i trigger reali non sono verificati; potrebbe essere necessario un ancoraggio assoluto, con corretto escaping SQL. Non sono state cambiate aspettative per nascondere questo rischio.

### Trigger con correzioni candidate

Tutte le righe si riferiscono a `migrations/bootstrap/001_schema.sql`, dichiarazione CREATE TRIGGER. Nessuno di questi trigger Ã¨ stato modificato in un database.

| Trigger | Riga | Espressioni sostituite |
|---|---:|---:|
| email_event_insert | 2857 | 1 |
| fpe_insert | 3251 | 1 |
| import_batch_insert | 2982 | 2 |
| import_event_insert | 3009 | 1 |
| mt5b_membership_delete_contract | 1505 | 1 |
| mt5b_membership_security_delete | 1534 | 1 |
| mt5b_security_delete | 1800 | 1 |
| mt5c_event_delete | 1870 | 1 |
| mt5c_event_insert | 1891 | 1 |
| mt5c_invitation_delete | 1391 | 1 |
| mt5c_invitation_insert | 1412 | 2 |
| mt5c_invitation_update | 1438 | 2 |
| mt5c_nonce_delete | 2074 | 1 |
| mt5c_nonce_insert | 2095 | 1 |
| mt5c_provisioning_delete | 1924 | 1 |
| mt5d_event_delete | 1585 | 1 |
| mt5d_event_insert | 1607 | 1 |
| mt5gov_approval_delete | 2024 | 1 |
| mt5gov_recovery_delete | 1974 | 1 |

### M84-02 â€” SQL embedded ancora incompatibile

`Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/Migrations/Mt5cOnboardingIntegrity.cs:23`: token_hash e channel_hash usano `NOT REGEXP BINARY`. Il problema Ã¨ della stessa categoria documentata, condizionato all'esecuzione della query. Non corretto nella closure storica; deve entrare nella nuova closure applicativa. Sono necessari record validi e non validi e verifica con l'account effettivo. `IdentityAccountIntegrity.cs:35` usa invece regex non binaria, non dimostra errore 3995.

### M84-03 â€” test/tooling 5.7 non universalmente compatibili

`tests/Integration/Mt5bSecurityStateTests.cs:28` e `tools/local_test_db.py:22` conservano `NO_AUTO_CREATE_USER`; il test C# a riga 335 contiene anche `BINARY code REGEXP`. Non eseguibili invariati contro MySQL 8.4 nei rami interessati. Il percorso staging ha giÃ  un adapter distinto; non Ã¨ lecito dedurne che tutta la suite usi quell'adapter. Aggiornamento della nuova variante dei test ancora da realizzare, conservando gli scenari precedenti.

### M84-04 â€” versione fixture diversa dal target

`tests/Integration/IntegrationFixture.cs:33` usa `mysql:8.4.3`, senza il digest staging. Non Ã¨ un errore SQL confermato, ma una lacuna di certificazione: una suite verde su quella fixture non prova 8.4.13.

### CompatibilitÃ  giÃ  gestita nel percorso staging

- `mysql84-contract.cjs:2,24-25`: eliminazione mirata SQL mode obsoleto e quoting di ROW_NUMBER; nessun errore nuovo riprodotto. Le qualificazioni NEW.row_number rimangono legali.
- `prepare-release-sql.cjs:32`: il ramo 8.4 usa SHOW_ROUTINE; mysql.proc resta nel ramo 5.7. Non classificato come errore sul ramo staging 8.4.
- Il contratto distingue esplicitamente FK DELETE RESTRICT da UPDATE NO ACTION e verifica le colonne/identitÃ  FK. I test offline coprono 200 FK standard. Non viene normalizzato il database osservato per farlo coincidere.
- Preflight confronta DDL, metadati, DEFINER, SQL mode, routine e inventario della release **precedente**. Le differenze di formattazione giÃ  gestite restano coperte dai test negativi originali.
- Il provider usa `ServerVersion.AutoDetect` (`ServiceCollectionExtensions.cs:44`); build/restore riescono. Questo non certifica l'esecuzione delle query MySqlConnector/Pomelo su 8.4.13.
- Le query FOR UPDATE sono presenti nei percorsi IAM, email, calendario e altri repository, come documentato nell'inventario. Non Ã¨ stata eseguita una prova di concorrenza, deadlock o isolamento.

JSON, date, privilegi minimi/visibilitÃ  INFORMATION_SCHEMA, charset delle routine e foreign key non standard rimangono gate runtime. Nessun difetto esecutivo ulteriore dimostrato. Non si classifica una deprecazione di BINARY nei confronti ordinari come incompatibilitÃ  3995.

## Upgrade: blocco architetturale e procedura richiesta

`infra/staging/ops.py:122-150` implementa provisioning su schema vuoto e journal dei file SQL. `Journal.apply` (righe 102-119 circa) scrive started prima del batch e rifiuta successivamente passi parziali o hash divergenti. Non implementa un aggiornamento tra due manifest. `host.py` non espone un comando upgrade. Ripetere provision non aggiorna lo staging esistente.

In MySQL le singole istruzioni CREATE/DROP TRIGGER sono atomic DDL, ma la coppia DROP + CREATE non forma una transazione annullabile. Un errore dopo DROP puÃ² lasciare il trigger assente; non basta BEGIN/ROLLBACK. Riferimento: [MySQL atomic DDL](https://dev.mysql.com/doc/refman/8.4/en/atomic-ddl.html). Il runner incrementale **non Ã¨ implementato in questa consegna**.

Requisiti per completarlo prima del prossimo rilascio:

1. Closure immutabile separata per `er-quality-1` e nuova closure completa, inclusi tooling e query embedded. Validazione del vecchio schema con il vecchio contratto esatto, binding di server/database, journal originale completo, nessun drift ammesso.
2. Impedire scritture applicative durante l'intero intervallo DDL, incluse connessioni/job giÃ  attivi. Un semplice GET_LOCK non esclude DML di client che non cooperano. Non ampliare privilegi API.
3. Acquisire lock dell'operazione e snapshot verificabile di dati sintetici nei test. Registrare separatamente ogni DROP/CREATE con intent persistito e hash, SQL mode, charset, collation, ordine e DEFINER originali. Non usare DROP IF EXISTS per mascherare drift.
4. Verificare prima e dopo ciascun oggetto. Se un passo Ã¨ started o lo stato osservato Ã¨ inatteso: arresto fail-closed, mantenimento del blocco scritture, nessun replay automatico. Recupero esplicito solo dopo riconciliazione stato atteso/osservato; nessun rollback di dati.
5. Nuovo preflight completo e confronto fingerprint dati prima di abilitare il traffico. Journal completato non deve ripetere DDL.
6. Fresh install e upgrade devono convergere sul medesimo schema supportato, preservando storie distinte. Aggiornare pacchetto, Dockerfile, ops/host e workflow soltanto con test delle due strade.

Non Ã¨ stato modificato il vecchio preflight per accettare i nuovi corpi: continuerebbe correttamente a rifiutarli. La nuova candidata non Ã¨ accettata dal vecchio sistema di packaging. Questo Ã¨ un blocco, non una procedura di upgrade pronta.

## Validazione effettivamente eseguita

| Comando/area | Esito in questa sessione |
|---|---|
| dotnet restore soluzione --locked-mode | PASS |
| dotnet build soluzione -c Release --no-restore | PASS, 10 warning nullable nei test di integrazione, 0 errori |
| dotnet test tests/Unit -c Release | 598 PASS, 0 ignorati |
| StagingCalendarFeatureTests, --no-build --no-restore | 2 PASS; contratti HTTP/MVC offline, non HTTP su MySQL |
| python -m unittest discover -s tools -p test_*.py | 68 PASS |
| node tools/test-mysql84.cjs | PASS offline, inclusi controlli negativi; runtime non testato |
| node tools/test-release-manifest.cjs | 14 controlli negativi PASS |
| node tools/release-artifacts.cjs | 760 artefatti canonici PASS |
| node tools/quality-schema-contract.cjs | PASS, 71 tabelle/180 trigger attesi |
| node tools/mysql84-remediation.cjs | Derivazione candidata riproducibile, deployment BLOCKED |
| node tools/test-mysql84-remediation.cjs | 19 corpi candidati verificati staticamente |
| CodeOrganization restore + check | 661 file, 658 tipi, 0 violazioni; inventario rigenerato dal tool poi ripristinato per evitare variazioni CRLF estranee |
| git diff --check | PASS sui file tracciati; controllo whitespace separato delle aggiunte |
| verify-institution-{import,email,formative-plan,staff}.py | FAIL, digest mismatch .dockerignore |
| verify-institution-calendar.py | FAIL, digest mismatch .gitignore |

I cinque fallimenti dei verificatori di modulo riguardano file non modificati durante questa consegna. Non sono stati rigenerati i manifest per nasconderli. La causa precisa dei disallineamenti storici richiede ulteriore riconciliazione, distinta dalla validazione canonica LF/CRLF che passa.

### Test non eseguiti e prove mancanti

`docker version`: client 29.9.2 disponibile; errore di accesso a `.docker/config.json` e `permission denied` sul pipe `docker_engine`. Non si Ã¨ tentata escalation nÃ© avviato/alterato Docker Desktop.

Non eseguiti: bootstrap storico/candidato su MySQL reale, riproduzione locale 3995, trigger INSERT/UPDATE/DELETE con contesti realistici, effetti collaterali, rollback dati, immutabilitÃ , cross-tenant, membership/sospensione/revoca, onboarding/governance, email, import, piani/versioni, calendario dipendente dai profili, HTTP con database, least privilege, upgrade/dati/fingerprint, idempotenza, interruzione/recupero e rifiuto schema iniziale divergente. La suite database generale non Ã¨ stata avviata per evitare un esito con test ignorati o una connessione locale preconfigurata a un database esistente.

Actionlint non disponibile localmente; workflow nuovo non validato con actionlint. Il resto dei test precedenti non Ã¨ stato disabilitato o modificato.

### Test preparati per CI

`.github/workflows/mysql84-remediation-candidate.yml` Ã¨ manuale, isolato, senza credenziali AWS e senza porte MySQL pubblicate. Usa esattamente il digest `infra/staging/mysql-image.json`, esegue gli attuali controlli storici, controlla la candidata e interroga ICU reale.

`node tools/test-mysql84-remediation.cjs --sql` produce casi di predicato per tutti i pattern distinti: valore valido, corto, lungo, non esadecimale, minuscolo, Unicode, newline finale, NULL con confronto null-safe. La CI richiede versione esatta e tutti risultati attesi. Nessuna esecuzione CI Ã¨ avvenuta in questa sessione. Queste sonde non attraversano trigger: anche un loro PASS non chiude i gate richiesti. La configurazione root senza password riguarda esclusivamente il servizio effimero senza porte pubblicate; non Ã¨ un modello per staging.

## Istruzioni operative per il prossimo rilascio EC2

**Non distribuire questa candidata. Non eseguire provision sul database staging esistente come tentativo di upgrade.**

Prima della pubblicazione: completare runner incrementale e nuovo contratto, correggere query embedded e variante test 8.4, riconciliare i verificatori storici, eseguire actionlint e tutte le regressioni reali richieste con il digest fissato. La sonda newline potrebbe evidenziare un'ulteriore modifica necessaria agli ancoraggi.

Simulare in CI una nuova installazione della release originale, seed coerente, snapshot/fingerprint, upgrade, contratti finali, tutti i percorsi modificati, conservazione account/tenant/membership e dati, seconda esecuzione senza DDL, fault injection tra DROP/CREATE con recupero controllato e rifiuto del predecessore alterato. Eseguire anche installazione diretta nuova e confronto degli schemi finali. Non ripristinare automaticamente dati come rollback.

Solo con queste prove: pubblicare una versione distinta, mantenere immutabile l'artefatto della release precedente, predisporre backup verificato e finestra di assenza scritture; usare il futuro comando upgrade esplicito, verificare journal/preflight e ripristinare il traffico dopo verifica completa. Non ci sono comandi EC2 pronti in questa consegna perchÃ© quel comando sicuro ancora non esiste.

## Tabella finale

| ID | IncompatibilitÃ  | SeveritÃ  | Correzione | Test reale | Stato |
|---|---|---|---|---|---|
| M84-01 | Regex binarie nei 19 trigger canonici | Alta | 22 sostituzioni candidate, fuori dalla release storica | Non eseguito; incidente iniziale segnalato dall'utente | PARZIALE |
| M84-02 | Regex binarie embedded Mt5cOnboardingIntegrity:23 | Alta | Nuova variante applicativa ancora necessaria | Non eseguito | APERTO |
| M84-03 | Mode 5.7 e regex binarie in test/tooling dedicati | Alta per certificazione | Adapter staging giÃ  presente; suite generale da aggiornare | Non eseguito | APERTO |
| M84-04 | Fixture mysql:8.4.3 diversa dal target | Media | Nuovo workflow sonde usa digest 8.4.13; fixture generale invariata | CI non eseguita | PARZIALE |
| M84-05 | Upgrade incrementale assente; intervallo DROP/CREATE | Critica per release | Specifica fail-closed documentata; runner da implementare | Non eseguito | BLOCCANTE |
| M84-06 | Ancoraggi ICU/newline e invarianti ASCII/NULL | Alta potenziale | Sonde positive/negative preparate | Non eseguito | BLOCCANTE |
| M84-07 | Mode rimosso, ROW_NUMBER, mysql.proc nel ramo 8.4 | Alta se non adattati | GiÃ  gestiti dall'adapter storico staging | Solo offline | GESTITO STATICAMENTE |
| M84-08 | Metadati FK/collation/header trigger su 8.4 | Alta per preflight | Contratti precedenti e controlli negativi conservati | Solo offline | GESTITO STATICAMENTE |
| M84-09 | Cinque verificatori storici falliscono sugli hash | Alta per gate release | Nessuna riscrittura automatica; riconciliazione necessaria | N/A, fallimento statico riprodotto | APERTO |
| M84-10 | Privilegi, JSON/date, lock, tenant e regressioni di modulo | Da determinare | Nessun difetto aggiuntivo dimostrato; test ancora necessari | Non eseguito | NON CERTIFICATO |
