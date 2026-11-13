# Verifica locale Docker Desktop — 2026-11-02

Esito: **BLOCKED — accesso Docker dal processo Codex negato**.
Questa verifica non certifica la release `er-quality-2-mysql84`.

## Riscontri effettivi

- `docker version`, `docker info`, `docker context ls` e `docker ps` sono stati
  tentati: PowerShell non riesce a risolvere il comando nel contesto corrente.
- Il PATH del processo contiene già
  `C:\Users\Lorenzo\AppData\Local\Programs\DockerDesktop\resources\bin`.
  Non è quindi dimostrato un difetto del PATH.
- `Get-Process` mostra Docker Desktop, `com.docker.backend`, `com.docker.build`
  e processi WSL in esecuzione. Il backend proviene dall'installazione per utente
  `C:\Users\Lorenzo\AppData\Local\Programs\DockerDesktop`.
- L'accesso esplicito al percorso `resources\bin\docker.exe` restituisce
  `UnauthorizedAccessException` / «Accesso negato». Anche la directory
  DockerDesktop e la lettura delle sue ACL sono negate. L'errore successivo
  «percorso non trovato» non dimostra l'assenza del file: segue il diniego.
- L'invocazione dei quattro comandi mediante percorso assoluto è stata tentata
  e non può avviare il client. Anche `docker inspect mysql84-local`, limitato
  a ID, nome, immagine, stato, porte e mount, non ha potuto avviare il client.
  Nessuna variabile d'ambiente del container o credenziale è stata stampata.
- `whoami` restituisce `desktop-quoag32\codexsandboxoffline`, appartenente a
  `CodexSandboxUsers`: il processo non viene eseguito come l'utente Lorenzo
  che possiede l'installazione Docker per utente.
- La lettura di `C:\Users\Lorenzo\.docker\contexts` è negata.
- `wsl --list --verbose` restituisce
  `Wsl/EnumerateDistros/Service/E_ACCESSDENIED`.
- L'enumerazione dei nomi delle pipe mostra `docker_engine` e
  `dockerDesktopLinuxEngine`, tra le altre. La presenza delle pipe non dimostra
  che il processo sia autorizzato a collegarsi al motore.
- Non risultano variabili `DOCKER_*` nel processo. Il contesto Docker attivo
  non è stato acquisito; non è stato cambiato.

La causa osservabile è il diniego di accesso dall'identità sandbox alla
installazione/configurazione Docker per utente e al servizio WSL. Non è possibile
separare ulteriormente ACL Windows e policy sandbox senza accesso aggiuntivo:
la stessa lettura delle ACL è negata. Non è corretto concludere che Docker
Desktop sia assente o fermo.

## Identità server e protezione dei dati

Il repository vincola `mysql:8.4.13` al digest
`sha256:5cf1e1293ea42e077e6a22114e6773b9aae3bb8f9933a2acd5a9721518ff8f5b`,
piattaforma `linux/amd64`, in `infra/staging/mysql-image.json`.
Questa è l'identità richiesta, **non una versione/digest osservata sul server**.

Nome `mysql84-local`, porta 3307, database `electronic_register`, immagine,
digest e mount del container esistente restano non verificati.
Nessuna connessione SQL è stata aperta. Non sono stati creati, modificati,
arrestati o cancellati container o volumi. Nessuna modifica Docker, ACL,
contesto, WSL, variabile persistente o installazione è stata effettuata.

Non è stato creato un container temporaneo; ID container, volume, porta e UUID
MySQL temporanei non esistono per questa esecuzione.

## Esiti richiesti

| Controllo | Esito |
|---|---|
| Presenza di processi Docker Desktop/WSL | PASS, osservata |
| Esecuzione del client e interrogazione del motore da Codex | BLOCKED |
| Inspect sicuro del container esistente | BLOCKED |
| Creazione e attestazione ambiente temporaneo | BLOCKED |
| Confronto schema atteso/reale 8.4.13 | BLOCKED |
| Metadati e correzione GUID/collation | BLOCKED |
| Fresh con protocollo e journal | BLOCKED |
| Predecessore, snapshot, upgrade, recupero e idempotenza | BLOCKED |
| Integrazione IAM/onboarding/calendar/import/email/formative/tenant | BLOCKED |
| Tre test CLI Windows con database effimero | BLOCKED |

Non sono stati ripetuti test Node/Python/C# né rigenerato il manifest in questo
tentativo: non sono cambiate sorgenti eseguibili. I risultati della precedente
sessione sono storici e non rappresentano nuove prove runtime.
La diagnosi preliminare è durata pochi minuti; nessuna durata di suite MySQL
può essere riportata.

Non è stato aggiunto un percorso locale del runner senza poterlo verificare.
Non è stato impostato `GITHUB_ACTIONS=true` né indebolito il runner CI.
Per proseguire serve una sessione il cui processo possa eseguire il client
Docker installato e usare il motore locale, con autorizzazioni stabilite
esternamente a questo processo sandbox. Non sono stati tentati aggiramenti
tramite altre identità, terminali o interfacce.

**Stato release: NON READY. Nessun commit, push, deployment o accesso EC2.**
