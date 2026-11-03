# Run #12 — certificazione Windows della CLI amministrativa

Base: `refactor-database-entities`, commit `0c7ede7d5acaef01e578691ae1bea3be622d4ea0`.
Run esaminato: <https://github.com/xxdema10xx/ElectronicRegister/actions/runs/41702712812>.
Data delle prove: 3 novembre 2026.

## Esito e confine della certificazione

I tre metodi obbligatori sono stati eseguiti realmente su Windows con la CLI amministrativa, MySQL nativo isolato e ACL NTFS. Tutti PASS, zero skipped. Sono disponibili [report JSON](windows-cli-run12-evidence/windows-cli-diagnostic.json) e TRX separati:

| Metodo in `Mt5cOnboardingTests` | Esito | Evidenza |
|---|---|---|
| `ActualAdministrativeCliVerifiesSignedIntentAndUsesOnlyOwnerInstalledChannel` | PASS | [0.trx](windows-cli-run12-evidence/0.trx) |
| `Governance_ActualCliUsesTheDedicatedManagerWithoutRequiringNotificationDelivery` | PASS | [1.trx](windows-cli-run12-evidence/1.trx) |
| `PlatformCliRefusesAuthorityConfigurationReadableByEveryoneBeforeMutation` | PASS | [2.trx](windows-cli-run12-evidence/2.trx) |

Le prove consegnate sono locali Windows, non artefatti prodotti dal job hosted. I campi GitHub della loro provenienza sono intenzionalmente vuoti. Non possono certificare un run CI: l'aggregatore confronta SHA, run ID e attempt con quelli del job corrente.

Il run #12 rimane fallito: il workflow modificato deve essere pubblicato ed eseguito in un nuovo run per ottenere la certificazione complessiva dello stesso run. Non sono stati eseguiti commit, push, deployment o tentativi di attribuire un esito al job Windows assente nel run #12. La lettura dei job GitHub conferma release fresh/upgrade, diagnostic e 12 partizioni integration riusciti, final-gate fallito.

## Requisiti e scelta del provisioning

Il percorso 8.4.13 delle fixture usa Testcontainers Linux, `sh`, `/var/lib/mysql/` e attestazione del container anche nella CLI. `windows-latest` non viene considerato un host Docker Linux. Introdurre un server nativo 8.4 richiederebbe modificare anche le verifiche applicative già certificate della CLI e della readiness.

Il job Windows usa invece il percorso nativo **MySQL 5.7.24 già supportato** da `TestMySqlTarget`, readiness e PlatformAdmin. È una prova di compatibilità Windows/CLI/ACL; la certificazione della release MySQL 8.4.13 resta affidata ai job Linux obbligatori esistenti. Il test Windows non afferma che MySQL 8.4 sia stato eseguito su Windows.

L'archivio ZIP proviene dall'[archivio ufficiale MySQL](https://downloads.mysql.com/archives/community/). URL, SHA256 dell'archivio e SHA256 dei quattro eseguibili sono fissati in [windows-cli-mysql.json](../../tools/windows-cli-mysql.json). Il download verificato ha SHA256 `B851100272E5D6D741E650E59989DCCCF518A75478E25B5947B1552FB47E4F11`. Le prove consegnate usano gli eseguibili estratti da questo archivio, non quelli MAMP già installati. Una prima esecuzione MAMP era passata anch'essa; non viene usata come prova del pacchetto hosted.

Il nuovo job `windows-cli`:

1. Usa `windows-latest`, .NET 10 e le stesse revisioni fissate delle Actions già adottate.
2. Scarica il ZIP, ne verifica SHA256, estrae in `RUNNER_TEMP` e verifica gli hash degli eseguibili prima dell'inizializzazione.
3. Ripristina con `--locked-mode` e compila in Debug, configurazione richiesta dagli attuali `dotnet run --no-build` della CLI nei test.
4. Crea una directory UUID nuova sotto `.staff-private/windows-cli-*`; nessuna discovery o adozione di istanze preesistenti.
5. Riusa `local_test_db.Setup`: inizializzazione senza rete TCP, password casuale impostata attraverso un canale shared-memory casuale, riavvio autenticato su loopback e porta effimera non riservata.
6. Attesta PID, percorso eseguibile, listener unico loopback, UUID server, versione, datadir esatto e sql_mode. Ripete l'attestazione prima delle installazioni e di ogni metodo.
7. Installa in un database inizialmente vuoto lo schema e le migration già definite nel manifest storico. Sulle sole copie SQL in memoria lega i predicati root di teardown alla porta nuova. Nessun file SQL storico viene riscritto. `er_schema_ready()` deve restituire 1.
8. Esegue separatamente i tre fatti, usando il vero PlatformAdmin, firme, canale owner-installed e manager di governance già previsti dai test.
9. Raccoglie ACL sanificate e TRX, elimina password e configurazioni private con la directory creata e termina esclusivamente i processi avviati dal proprio oggetto `Popen`. Un errore di cleanup blocca la certificazione.

Non vengono installati servizi Windows né modificati Docker, `mysql84-local`, EC2, produzione, configurazioni globali di istanze esistenti o migration storiche. MySQL 5.7 viene esposto solo su loopback durante questo job effimero; non è una proposta di versione per produzione.

## Prove ACL e protezione dei segreti

`PrivateTestFiles` conserva il controllo di owner e ACL; consente al runner di raggruppare soltanto i propri file sotto una directory privata esplicita nel workspace. I grant ammessi sono il SID dell'utente corrente, SYSTEM (`S-1-5-18`) e Administrators (`S-1-5-32-544`). Il report contiene owner, regole e SDDL delle tre configurazioni e del loro parent privato, senza contenuto delle configurazioni.

Il controllo negativo registra tre osservazioni reali in `aclNegative`:

- `private-before`: configurazione privata;
- `broad-refused-without-mutation`: aggiunto `S-1-1-0` (Everyone) con ReadData; CLI exit 1, stdout vuoto e zero istituzioni create;
- `private-restored-and-accepted`: ripristinato il DACL originale; stessa operazione firmata accettata e una istituzione presente.

Il test verifica gli effetti SQL e il comportamento della CLI prima di registrare le fasi. L'aggregatore richiede le tre fasi esatte, grant Everyone soltanto nella fase negativa e SDDL iniziale/finale identici. Il TRX deve confermare l'esecuzione riuscita dell'intero metodo, incluse queste asserzioni.

Credenziali, richieste firmate, inbox e script SQL privati non vengono caricati. Il runner oscura la password effimera nei diagnostici e nei TRX se un framework la emette. Il workflow carica soltanto il report e i tre TRX; il datadir e i file di autorità vengono rimossi.

## Aggregazione e regressioni

`final_ci_diagnostic.py` richiede il job `windows-cli` riuscito e una sola suite Windows valida. Il `BLOCKED` statico è sostituito dalla verifica dell'evidenza; report mancante o invalido produce ancora un gate bloccante. Restano immutati i gate Linux e il significato del predecessore `er-quality-1`.

`windows_cli_evidence.py` verifica tutte le nove righe obbligatorie, nessuna riga duplicata o opzionale, provenienza dello stesso run/attempt/SHA, piattaforma Windows, identità isolata e pacchetto fissato. Apre i TRX effettivi e verifica hash, risultato Completed, contatori coerenti e Passed. Ogni TRX deve contenere esattamente uno dei tre metodi previsti, ricavato da TestDefinitions; l'unione deve contenere ogni metodo una sola volta. Nessuna giustificazione di skip può certificare questi test.

Le nove regressioni in `test_windows_cli_evidence.py` coprono: job assente/skipped/failed/cancelled; report assente/incompleto; righe FAIL/SKIPPED/BLOCKED/opzionali; TRX fallito/skipped/duplicato/mancante/alterato; duplicazione di metodi o report; ACL Everyone o principal dichiarati trusted impropriamente; prova negativa ACL incompleta e ripristino divergente; piattaforma errata, provenienza di altro run e path traversal. Sono fixture sintetiche del parser, separate dalle prove reali di Windows consegnate.

## Verifiche e limitazioni

Sono passati: build della soluzione; tre test Windows reali (3/3, zero skipped); validazione indipendente dei report/TRX; suite Python completa (115/115); unitari (598/598); configurazione fixture (4/4); actionlint 1.7.7; verifica manifest correttivo; tutti i sei controlli Node della release; `git diff --check`; controllo della presenza dei quattro nuovi file di tooling nel manifest correttivo; CodeOrganization (661 file, 658 tipi, zero violazioni). I controlli Node comprendono 14 controlli negativi del manifest storico e 12 controlli negativi/drift della chiusura correttiva. Le verifiche Node sono statiche e non vengono presentate come nuove prove runtime MySQL 8.4.

Il manifest rigenerato appartiene esclusivamente a `er-quality-2-mysql84`. Manifest storico, migration e artefatti `er-quality-1` non vengono modificati. I file nuovi rimangono non committati e sono inclusi dalla discovery dei file addabili del generatore di manifest. Il documento preesistente `github-actions-run11-campus-race.md` non è stato modificato.

Il sistema operativo locale non è l'immagine hosted `windows-latest`: disponibilità dei runtime nativi, download e comportamento dell'immagine hosted dovranno essere confermati dal nuovo run. Fallimenti di setup/upload, report assenti, test non eseguiti e cleanup fallito continueranno a impedire il final-gate. Nessun esito hosted futuro è dichiarato PASS sulla base di queste prove locali.
