# Account istituzionali: modello, procedure e limiti

Note di consegna del 2026-11-05. Valgono per il branch `refactor-database-entities` da `5fd2a85` in poi, su MySQL 8.4.13.

## Modello

- **Nessuna registrazione libera.** `POST /api/identity/accounts` non esiste più e risponde 404.
- **Creazione dell'istituto.** Il proprietario della piattaforma crea l'istituto e **un solo** InstitutionAdmin iniziale con lo script `tools/OwnerProvisioning`.
- **Gestione autonoma.** Dopo la creazione l'istituto si gestisce da solo con `POST /api/institution/accounts`:
  - InstitutionAdmin crea account di ogni ruolo del proprio istituto, compresi altri InstitutionAdmin, senza limiti di numero;
  - Staff crea solo account `Student` o `Teacher`, con un solo ruolo;
  - resta soltanto il blocco sulla rimozione dell'ultimo admin.
- **Perimetro.** L'istituto viene sempre dal token, mai dalla richiesta. Un account appartiene al proprio istituto e il suo token non apre altri istituti.
- **Password temporanea.**
  - È generata dal sistema e mostrata una sola volta a chi crea l'account; non viene mai salvata in chiaro, scritta nei log o nell'audit.
  - Finché non viene cambiata, il server consente solo `PUT /api/identity/account/password`. Il login restituisce `passwordChangeRequired: true`.
  - Dopo il cambio, i token emessi in precedenza non valgono più.

## Script del proprietario (`tools/OwnerProvisioning`)

Lo script non fa parte dell'immagine Docker dell'API. Va eseguito da un computer del proprietario con l'SDK .NET 10.

```bash
# Connessione: variabile d'ambiente oppure --connection-file con un file FUORI da qualsiasi repository git.
export ER_OWNER_MYSQL_CONNECTION='Server=...;Port=...;Database=...;User ID=...;Password=...;SslMode=VerifyFull;'
# UUID del server di destinazione, letto separatamente: SELECT @@server_uuid;
dotnet run --project tools/OwnerProvisioning -c Release -- create-institution \
  --code CODICE_ISTITUTO --name "Nome istituto" --admin-email admin@istituto.example \
  --expected-server-uuid <uuid> [--reason "motivo"]
dotnet run --project tools/OwnerProvisioning -c Release -- reset-password \
  --email utente@istituto.example --expected-server-uuid <uuid> [--reason "motivo"]
unset ER_OWNER_MYSQL_CONNECTION
```

**Controlli prima di scrivere:**
- `@@server_uuid` deve coincidere con quello atteso;
- MySQL deve essere 8.4.x;
- deve essere presente la release di schema `er-quality-2-mysql84`.

In caso contrario lo script esce con codice 3 senza modificare nulla.

**`create-institution`**
- È idempotente: con lo stesso codice e la stessa email risponde `already-provisioned` e **non mostra più la password**.
- Un codice già usato con un altro admin, o un'email già registrata, provoca exit 4 senza modifiche.

**`reset-password`**
- Vale per qualsiasi ruolo.
- Imposta una nuova password temporanea con cambio obbligatorio e incrementa la generation dell'identità, revocando tutti i token emessi prima.
- Va usato anche **quando chi ha creato un account ne perde la risposta**: ripetere la creazione con lo stesso `eventId` restituisce solo gli id (`replayed: true`), senza password.

**Audit:** `institution_onboarding_event`, con `actor_kind='Operator'`, `operator_id='owner-script'` e le azioni `OwnerProvision` e `OwnerPasswordReset`.

## Endpoint `POST /api/institution/accounts`

| Campo | Significato |
|---|---|
| `eventId` | Obbligatorio, chiave di idempotenza. Con lo stesso `eventId` e la stessa richiesta si ottiene `200` con gli stessi id e senza password. Con lo stesso `eventId` e una richiesta diversa si ottiene `409` |
| `email` | Obbligatorio |
| `roles` | Obbligatorio. Per lo Staff solo `["Student"]` o `["Teacher"]` |
| `reason` | Obbligatorio |
| `studentId` / `teacherId` | Facoltativi: collegano una scheda esistente dello stesso istituto |
| `firstName`, `lastName`, `classId` | Creano una scheda nuova. `classId` è facoltativo e vale solo per gli studenti |

**Risposte:**

| Codice | Quando |
|---|---|
| `201` | Account creato: `userId`, `membershipId`, `roles`, `studentId`/`teacherId`, `temporaryPassword`, con `Cache-Control: no-store` |
| `400` | Campi sconosciuti (per esempio `institutionId` o un `role` fuori dall'array), o richiesta non valida |
| `403` | Ruolo dell'attore non autorizzato, compreso lo Staff che chiede Staff o InstitutionAdmin |
| `404` | Scheda di un altro istituto o inesistente, oppure endpoint spento |
| `409 "Indirizzo email non disponibile"` | Risposta unica se l'indirizzo è già un account (qui o in un altro istituto) o è riservato. Il costo BCrypt è identico a quello di una creazione |
| `409` | La scheda è già collegata a un account |

## Configurazione necessaria

L'endpoint è **spento** se manca uno di questi valori:

| Chiave | Valore | Note |
|---|---|---|
| `Multitenancy:SchoolApi:Enabled` | `true` | Marker della superficie scuola, controllato dal gate |
| `Multitenancy:MembershipWriteApi:Enabled` | `true` | Controllato esplicitamente dall'endpoint (altrimenti 404) |
| `Deployment:RolloutApproved` | `true` | Obbligatorio in Staging e Production con qualsiasi flag `Multitenancy` attivo (`RuntimePolicy.cs:52`) |

**`RolloutApproved` è una scelta esplicita.** Dal 2026-11-06 `infra/release.runtime.example.json` riporta `"RolloutApproved": true`.
- Con i flag `Multitenancy` attivi e `RolloutApproved: false`, l'API in Production **non si avvia**: `RuntimePolicy` termina con `Runtime security configuration is incomplete or unsafe.`. Lo dimostra il caso negativo del flusso di produzione (`infra/staging/production_flow.py`).
- Chi prepara la configurazione revisionata deve decidere consapevolmente:
  - `true` significa "rilascio approvato con questi flag";
  - `false` impedisce l'avvio finché qualunque flag è attivo.
- Nello stesso esempio `Readiness.ExpectedVersion` resta `5.7.24` (INC-010, Lotto 3). Va impostato a `8.4.13` nella configurazione reale, altrimenti `/health/ready` risponde 503.

**Connessione dello script del proprietario con TLS.** Va usata solo la verifica completa: `SslMode=VerifyFull;SslCa=<ca del server MySQL>;TlsVersion=TLS 1.2`, con un utente non root abilitato alla DML sullo schema. È provata in CI con il migratore dello staging:
- una CA sbagliata produce l'uscita 5 senza scritture;
- `SslMode=None` viene rifiutato dal server (`require_secure_transport`).

## Procedura per caricare una classe

1. **Importare le schede senza riservare l'indirizzo.** Usare `POST /api/institution/imports/preview` con `EmailMode=None` e poi la conferma. L'import crea solo le schede: nessun utente, membership o indirizzo. Gli id delle schede sono in `GET /api/institution/imports/{id}/result` (campo `studentId` di ogni riga).
2. **Creare un account per ogni studente.** Chiamare `POST /api/institution/accounts` con `roles: ["Student"]`, `studentId` della riga e l'indirizzo istituzionale dello studente, con un `eventId` diverso per ogni studente. Lo può fare un InstitutionAdmin o uno Staff.
3. **Consegnare le password.** Ogni risposta 201 contiene la password temporanea di quello studente. Va consegnata subito: non viene salvata né mostrata di nuovo. Se va persa, si usa `reset-password` con lo script.

Gli account non si creano in blocco: serve una richiesta per ogni studente. Per un docente la procedura è la stessa, con `roles: ["Teacher"]` e `teacherId`, oppure con nome e cognome per creare una scheda nuova.

**Non usare `ReserveInstitutionalAddress` se lo stesso indirizzo deve diventare il login.** Un indirizzo riservato dall'import non può diventare l'email di un account: l'inserimento fallisce sulla prenotazione globale (`institution_email_reservation`). Collegare la scheda con un indirizzo diverso funziona, ma lo studente avrebbe due indirizzi. Per unire i due flussi bisogna cambiare il trigger di prenotazione, che non è stato modificato.

## Limiti noti

- **Spazio email globale.** `user.email` è unico in tutto il sistema e ogni indirizzo è prenotato globalmente. La risposta 409 è identica in ogni caso e i tempi sono allineati, ma un admin può capire che un indirizzo è "occupato da qualche parte"; non può capire dove. Il rischio è accettato e documentato. Per eliminarlo serve un modello per istituto: chiave `(institution_id, email)`, login con codice istituto, trigger di prenotazione rifatti.
- **Funzioni assenti:**
  - creazione massiva;
  - invio email;
  - reset in app per admin, docenti e studenti (per tutti esiste solo lo script).
- **Inviti.** Un admin può ancora invitare un account esistente di un altro istituto, se ne conosce l'id (risposta neutra). È un'incoerenza con "un account per istituto", da chiudere in un lotto successivo.
- **VULN-002 ancora aperta.** Sul login non c'è un limite per account contro brute force e credential stuffing: esiste solo il limite di 60 POST al minuto per IP, condiviso con tutte le scritture. Il cambio password ha invece già un limite per account (5 tentativi al minuto).
- **Staff e schede docente esistenti (rischio noto).** Staff può creare un account Teacher collegandolo a una scheda docente già esistente del proprio istituto (`teacherId`), anche se l'ha creata un altro. Il nuovo account vede i dati di quella scheda. È coerente con la decisione 3, perché Staff crea già le schede docente, ma un collegamento sbagliato o malevolo è possibile. Lo limitano l'audit `AccountCreated` (attore, ruoli, `teacherId`) e il vincolo di un solo account per scheda; non c'è un'approvazione dell'admin. Lo stesso vale per gli account Student su schede studente esistenti.
- **INC-008 resta necessario per la produzione.** Lo script è una procedura manuale con connessione diretta al DB, protetta da UUID e release attesi. Non sostituisce una CLI attestata con TLS VerifyFull e doppia firma.
- **Frontend.** `Frontend/ElectronicRegisterReact` e `Frontend/ElectronicRegisterWeb` non chiamano `POST /api/identity/accounts` e non gestiscono `passwordChangeRequired`. Usano ancora le rotte globali storiche (`/Auth/login`, `/Auth/register`, `/Auth/RegisterForAdmin`, `/Auth/me`), già ritirate dal backend: il frontend va adeguato al flusso identità → selezione istituto → access token prima di qualsiasi uso.

## Evidenze

**CI**

| Commit | Contenuto | Esito |
|---|---|---|
| `4a5f051` | Fasi 1–3 | Tutti i workflow e tutti i job verdi, compresi `iam` 0/2 e 1/2 e `iam-security` 0–3/4 |
| `5fd2a85` | Fase 4 (registrazione rimossa) | 3/3 workflow e 22/22 job verdi, compresi `iam` 0/2 e 1/2 e `iam-security` 0–3/4 |

**Docker (Fase 5).** Il flusso è stato eseguito su:
- l'immagine API costruita da `infra/Dockerfile` a `5fd2a85`;
- un MySQL 8.4.13 usa e getta, con il bundle di release senza binding della fixture e principal non root.

Ha superato **18 passi su 18**:
1. readiness;
2. creazione dell'istituto e idempotenza;
3. admin: password temporanea, cambio, selezione dell'istituto, token InstitutionAdmin;
4. 404 della vecchia registrazione;
5. creazione di un docente (201, `no-store`), replay senza password, 409 neutro;
6. docente: cambio password, ruolo `[Teacher]`, scheda leggibile, nessun permesso di creazione;
7. reset del docente, con revoca dei token e della password vecchia;
8. reset dell'admin, con revoca del token.

Nessuna password è stata trovata nel log dell'API.

**Configurazione usata per la prova Docker locale (2026-11-05).** Ambiente `Production` con `Deployment:Mode=LocalTest`: listener e DB su loopback, senza TLS.

**Prova in configurazione di produzione (2026-11-06, CI).** Il workflow `staging-current.yml` esegue `infra/staging/production_flow.py` sull'immagine costruita da `github.sha`. La configurazione è:
- `ASPNETCORE_ENVIRONMENT=Production`, `Deployment:Mode=Production` e `RolloutApproved=true`;
- i flag di `release.runtime.example.json`;
- MySQL 8.4.13 con TLS 1.2 e `SslMode=VerifyFull`, con la CA generata nella run, `er_api` per l'API ed `er_migrator` per lo script, mai root;
- HTTPS tramite `infra/nginx.release.conf`, con una CA generata nella run.

Esito: **23 passi su 23**.
- **Flusso:** i 18 passi del flusso degli account.
- **Casi negativi:**
  - CA sbagliata e TLS disattivato verso MySQL;
  - avvio rifiutato senza `RolloutApproved`;
  - nessuna porta HTTP pubblicata dall'API;
  - HTTP sul proxy solo con redirect 308;
  - nessuna password nei log dell'API e del proxy.

Non provato: certificati pubblici reali, DNS reale e il proxy sull'host EC2. Lo smoke HTTPS pubblico resta un gate separato su EC2.
