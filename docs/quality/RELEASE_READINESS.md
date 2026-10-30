# Release er-quality-1: verifiche e configurazione

Questa procedura descrive il rilascio; la remediation non esegue deployment né modifica database preesistenti.

## Artefatti e schema

`migrations/bootstrap/manifest.json` è evidenza storica del bootstrap consolidato. Non certifica i sorgenti dopo il cutover e non va rigenerato per nascondere differenze. La chiusura corrente è `migrations/release/manifest.json`, verificata con `node tools/release-artifacts.cjs`. La normalizzazione UTF-8 CRLF→LF riguarda questo nuovo manifest; i file SQL e i manifest storici mantengono i byte originali. Percorsi, case e insieme dei file devono corrispondere esattamente.

Su database vuoto, la sequenza canonica è `migrations/bootstrap/001_schema.sql`, quindi `migrations/identity-account/001_identity_account.sql`, `002_profile_acquisition.sql`, `003_password_attempt.sql`, `004_release_schema.sql`, `005_release_gate.sql`. Le DDL MySQL 5.7 non sono transazionali: una fase fallita richiede diagnosi, senza replay automatico. Questo elenco non autorizza l'applicazione del bootstrap a un database con dati. L'upgrade di un database esistente richiede il preflight e un processo separato approvato.

Il Dockerfile include le SQL MT1/MT3 per le risorse embedded e consegna il bundle bootstrap/additive più il manifest sotto `/release/migrations`. L'API non avvia migrazioni. Un operatore usa un client MySQL separato; l'immagine API non necessita di privilegi DDL o di un client amministrativo.

## Tre verifiche distinte

- `/health/live`: processo HTTP vivo; non interroga SQL.
- `/health/ready`: identità SQL attesa e `CALL er_schema_ready()`. La routine verifica i marker er-quality-1 e er-quality-1-closure-1, 71 tabelle/180 trigger, 26 colonne additive con tipo/nullability/default, 18 parti di indice, 7 FK con colonne/riferimenti/rules e 6 corpi trigger additivi. Anche la query iniziale della API richiede il marker di revisione: la vecchia routine 004 non certifica questa release. La probe non esegue DDL/DML. Mancanza di routine, marker, prerequisito o permesso EXECUTE produce 503.
- `node tools/release-preflight.cjs <config.json>`: verifica estesa e in sola lettura dell'identità, delle 66 definizioni base di tabella, dei 174 trigger base, delle 5 tabelle e dei 6 trigger additivi e del corpo/security della routine. Va eseguita con un'identità di verifica che possa leggere i metadati, prima di abilitare il traffico. Non ripara lo schema e non sostituisce la verifica degli hash degli artefatti.

Il config del preflight contiene `host`, `port`, `database`, `user`, `passwordFile`, `caFile`, `expectedVersion`, `expectedDataDir` e facoltativamente `mysqlClient`. Non contiene password in chiaro: il file della password resta fuori dal repository e viene passato al client tramite ambiente del processo. La modalità `--isolated-test` è ammessa soltanto su loopback, database di test, porte non operative e directory `.staff-private/instance-*` della workspace; non è una modalità di release.

La routine usa SQL SECURITY DEFINER per rendere visibili i soli conteggi necessari senza attribuire TRIGGER/DDL all'account API. Il proprietario della routine deve restare disponibile e avere visibilità dei metadati. All'API si concede EXECUTE sulla routine, insieme ai soli permessi dati richiesti. Il marker non è una prova contro un amministratore SQL malevolo: il preflight verifica le definizioni; la readiness rileva prerequisiti mancanti.

## TLS e trasporto

Production/Staging richiedono VerifyFull, CA esplicita (`SslCa`), account SQL non root, AllowedHosts espliciti e proxy conosciuti. Montare CA e segreti come file runtime; non inserirli nell'immagine. Esempio del solo segmento pubblico di connessione: `SslMode=VerifyFull;SslCa=/run/secrets/mysql-ca.pem;TlsVersion=TLS 1.2`. MySqlConnector documenta [VerifyFull, SslCa e TlsVersion](https://mysqlconnector.net/connection-options/); CA e hostname sono entrambi verificati. Non utilizzare callback permissivi o protocolli TLS obsoleti per superare le verifiche.

Il binario MySQL 5.7.24 MAMP disponibile nella remediation espone per default TLS 1.0/1.1 e segnala `TLS version is invalid` con TLS 1.2. Non è quindi certificato come endpoint SQL sicuro per questa release. Occorre dimostrare una connessione VerifyFull con CA valida e TLS moderno sul build 5.7.24 destinato al rilascio, e il rifiuto di CA/hostname errati. La regressione SQL locale con SslMode=None, confinata al database di test su loopback, non soddisfa questo gate. Nessuna configurazione TLS del client di release è stata indebolita.

Il listener del container è HTTP interno dietro un proxy TLS fidato. CORS e X-Forwarded-* dipendono dalle origini/proxy configurati. Le probe operative devono raggiungere gli endpoint attraverso la topologia TLS/proxy prevista, con Host consentito; una redirezione HTTPS non è un 200 di readiness.

## Quota cambio password

La quota è per UserId globale e risiede sullo stesso primary SQL condiviso da tutte le repliche: 5 tentativi/60 secondi, prima di BCrypt, con una lease concorrente di 30 secondi e rilascio in finally. Cambiare IP, proxy, ticket o processo non azzera il budget. Una replica arrestata recupera la lease per scadenza; una pausa superiore alla lease può consentire lavoro CPU sovrapposto, mentre hash atteso/generation/lock garantiscono comunque un solo cambio credenziali valido. Non è un limite distribuito per le altre API POST.

## Verifiche della release

Restore locked, audit online completo (inclusi transitive), analyzer, Unit, guard e manifest sono gate distinti. Il job source è configurato per Windows e Linux; il job SQL manuale usa istanza nuova, MySQL 5.7.24 e guard di teardown vincolati alla sua identità. I runner richiedono .NET, Node, Python 3 e, per la regressione Windows, il binario MySQL autorizzato. Un audit offline senza sorgenti o un errore NU1900 non costituisce un risultato pulito.

Nel sandbox della remediation, Node verifica TLS verso nuget.org e un relay limitato a loopback trasporta i soli JSON degli advisory a NuGet, mentre il nuovo nupkg viene scaricato da nuget.org con HTTPS verificato. I lock vengono controllati da NuGet; nessuna opzione di ignorare i certificati viene usata. Questa soluzione ambientale non è parte del runtime o della configurazione NuGet di produzione.

## Aggiornamento A01–A04

Il manifest include la quinta additiva 005 e il contratto strutturale revisionato. 001–004 e il manifest storico restano immutati. Il validatore verifica anche schema/metadati/inventario e sequenza canonica, con 14 controlli negativi in tools/test-release-manifest.cjs.

Per il provisioning usare il bundle derivato da tools/prepare-release-sql.cjs: definer esplicito ACCOUNT LOCK, login API e reviewer distinti. Il preflight richiede schemaDefiner e ne controlla corrispondenza/esistenza. MySQL 5.7 cancella i grant specifici quando 005 sostituisce la routine: applicare sempre 99-routine-grants.sql dopo le migrazioni. Nessun grant DDL alla API.

Procedura completa per Docker, TLS moderno, privilegi, Nginx e CI: [DESTINATION_RELEASE_SMOKE.md](DESTINATION_RELEASE_SMOKE.md). Evidenze: [RELEASE_GATE_REMEDIATION.md](RELEASE_GATE_REMEDIATION.md). Non attestano un deployment o un run Docker eseguiti.
