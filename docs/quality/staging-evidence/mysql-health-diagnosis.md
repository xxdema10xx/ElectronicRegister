# MySQL healthcheck: run 41109867548

Repository `xxdema10xx/ElectronicRegister`, branch `refactor-database-entities`,
SHA `d580727dedb1d37fc8dce8aee81ccd597e031c45`. Analisi del 2026-10-30.

## Evidenze e limite della diagnosi

Il [job 124249193290](https://github.com/xxdema10xx/ElectronicRegister/actions/runs/41109867548/job/124249193290)
conferma build API/Ops, verifica source/manifest, volume-init completato, container
db avviato alle 18:48:16 UTC e timeout alle 18:51:16 UTC. Non pubblica inspect,
health output o log MySQL. "Started" non dimostra che mysqld sia rimasto attivo,
che abbia completato il bootstrap o che abbia caricato i certificati.

È stata verificata una causa bloccante concreta: l'immagine fissata per digest
è compilata con **yaSSL e non supporta TLS 1.2**. Sono stati scaricati dal registry
ufficiale il manifest e i layer dell'immagine, senza avviare container. Gli hash
SHA256 dei layer scaricati coincidono con quelli dichiarati dal manifest:

| Oggetto | SHA256 |
| --- | --- |
| Manifest linux/amd64 | `1f627f590dac0b2c4659282258ce205033adf94ebe8775a3e3a8424a87cdd746` |
| Layer contenente mysqld | `d7e648ad64aa7fc69805c62b1b656c8717d204a02f877f9aff3d1ecf3d3f186d` |
| Layer entrypoint | `4120d828ea6b1e9400b3b715c25b3d9a9f44cbaa90441aefb6b297d8f8430705` |
| `/usr/sbin/mysqld` estratto | `fa9756ecfacabca9c20db6be0567376bc99a3fb87301834859e26853ff1543ce` |

Il binario contiene simboli `yaTLSv1_client_method`,
`yaTLSv1_1_server_method`, numerosi simboli C++ `yaSSL`, il valore
`TLSv1,TLSv1.1` e la descrizione `TLS version, permitted values are TLSv1,
TLSv1.1, TLSv1.2(Only for openssl)`.
La [documentazione ufficiale MySQL](https://dev.mysql.com/doc/mysql-security-excerpt/5.7/en/ssl-libraries.html)
conferma la limitazione di yaSSL a TLS 1.0/1.1. Il compose impone TLS 1.2,
require_secure_transport e certificati dedicati; la health richiede SSL attivo.
Questa combinazione non può soddisfare la policy. Non si tratta di un semplice
timeout troppo breve né di una sonda da rendere permissiva.

Non è dimostrato quale percorso abbia seguito **quel processo**: errore di
inizializzazione/TLS, restart dopo errore, bootstrap incompleto oppure mysqld
attivo con SSL indisponibile. Il [comportamento descritto da MySQL](https://dev.mysql.com/blog-archive/ssltls-improvements-in-mysql-5-7-10/)
ammette che valori TLS non supportati disabilitino TLS. Questo è compatibile
con il sintomo, ma non sostituisce log runtime mancanti.

## Distinzione delle ipotesi

| Ipotesi | Evidenza disponibile / controllo aggiunto |
| --- | --- |
| Inizializzazione/configurazione fallita | Log db, exit code, restart count; incompatibilità yaSSL già verificata staticamente |
| Crash/OOM | State.Status, Running, Restarting, OOMKilled, ExitCode, Error e timestamp |
| Bootstrap SQL fallito | Marker started/completed/failed con riga; log SQL; lettura identity senza DDL |
| Permessi/secrets | UID/GID/mode e leggibilità dentro il container con il suo utente; nessun contenuto dei secret |
| Sola healthcheck | Ultimi esiti Docker e replay della sonda; versione, have_ssl, have_openssl, tls_version, require_secure_transport e skip_networking via socket |

`host.py` assegna owner 999 e mode 0400 ai secret DB; la directory dati viene
inizializzata separatamente. Queste impostazioni non provano un problema di
permessi nel run: non sono state allargate. Il bootstrap usa socket locale,
password validata e subshell per non alterare le variabili dell'entrypoint che
lo include. La rete temporaneamente disabilitata durante init non deve far
passare readiness. Nessuna prova giustifica modifiche alle DDL o ai certificati.

## Correzioni e stato finale

- Gate preventivo su `mysqld --no-defaults --verbose --help`, eseguito senza rete,
  non-root, readonly, senza capability. Rifiuta binari privi di TLS 1.2 prima
  della build API/Ops e della creazione del volume dati. Versione esatta 5.7.24.
- Input workflow `mysql_image` per un'immagine 5.7.24/OpenSSL auditata; il
  preparatore impone il digest SHA256. Nessuna immagine alternativa scelta senza
  verifica. Il default storico rimane riproducibile e viene rifiutato dal gate.
- `MYSQL_ROOT_HOST=localhost`: l'entrypoint estratto usa altrimenti `%` e crea
  un account root remoto. È un problema di configurazione verificato, distinto
  dall'incompatibilità TLS; il fix vale per stati nuovi, senza alterare volumi.
- Health invariata nei requisiti preesistenti, con aggiunta esplicita di
  require_secure_transport, connect timeout interno di 3 secondi e messaggio
  diagnostico. Nessun aumento dei 180 secondi o cambio della policy TLS.
- Diagnostica Actions `if: failure()` prima dello stop, in JSON sanitizzato.
  Non pubblica Config.Env, inspect completo, configurazioni o artifact grezzi.
  Maschera secret dello stato, password fixture, PEM, token e password note;
  se non riesce a leggere i secret interrompe la raccolta prima di Docker.
  Timeout ed errori di raccolta non stampano payload grezzi. Ownership sempre
  verificata prima di leggere log o eseguire comandi nei container.
- Manifest canonico aggiornato con `node tools/release-artifacts.cjs --write`;
  nessun manifest storico rigenerato.

**Il collaudo non è dichiarato risolto/verde:** è necessaria un'immagine auditata
5.7.24/OpenSSL e una nuova esecuzione Docker/TLS. La patch elimina il timeout
opaco per il candidato incompatibile e rende verificabili gli altri fallimenti.
Non viene fornito un digest sostitutivo non collaudato. I test TLS reali positivi
e negativi con driver rimangono obbligatori dopo il gate sulle capacità compilate.
MySQL senza porte pubbliche, API UID 1654 e database separato restano preservati.
Nessuna modifica Nginx/EC2, nessun commit o push, nessun run remoto avviato.

## Verifiche locali finali

- Python: **30 test passati**, inclusi rifiuto yaSSL, sanitizzazione,
  conservazione delle prove crash/health, ownership, timeout e fail-closed.
- Backend Unit: **598 passati, 0 falliti, 0 ignorati**, Release, no restore.
- Manifest: **751 artifact verificati**, LF/CRLF invarianti; verifica ufficiale
  e wrapper Python passati; **14 controlli negativi** passati.
- Sintassi shell bootstrap/health: `bash -n` passato.
- Python host/diagnostics: `py_compile` passato.
- Workflow: actionlint **v1.7.12**, passato; integrazioni shellcheck/pyflakes
  non disponibili e disabilitate, con controlli shell/Python separati sopra.
- `git diff --check`: passato.
- Docker, compose runtime, SQL/TLS reali: **non eseguiti**. Docker assente;
  WSL non installato. Non sono state alterate installazioni di sistema.

Per i test è stato usato Python 3.12.10 embedded temporaneo nel workspace,
con il percorso tools configurato per i subprocess dei test. Le prime esecuzioni
intermedie hanno rilevato quel percorso mancante e, dopo ulteriori modifiche,
hash manifest da aggiornare; il risultato finale sopra è successivo alle correzioni.
