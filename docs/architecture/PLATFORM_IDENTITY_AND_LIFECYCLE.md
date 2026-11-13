# Identità di piattaforma e ciclo di vita degli account

Stato: **implementato dietro flag**, `Multitenancy:PlatformIdentity:Enabled=false` per default.
Target database: **MySQL 8.4.13** (decisione D1, [DATABASE_TARGET_MYSQL84.md](DATABASE_TARGET_MYSQL84.md)).
Registro delle scelte prese durante l'implementazione: [PLATFORM_IDENTITY_DECISIONS.md](../refactoring/PLATFORM_IDENTITY_DECISIONS.md).

Il documento estende il modello multitenant esistente ([MULTITENANCY_DESIGN.md](MULTITENANCY_DESIGN.md),
[INSTITUTION_GOVERNANCE_AND_RECOVERY.md](INSTITUTION_GOVERNANCE_AND_RECOVERY.md)): `User` globale,
`Institution` come tenant, `InstitutionMembership` con ruoli, profili didattici per tenant e login in due
passi. Token, selezione dell'istituto e binding Microsoft non cambiano, salvo il rifiuto degli account chiusi.

## 1. Decisioni di prodotto

| # | Decisione |
|---|---|
| P1 | Un solo dominio email di piattaforma, da configurazione (`EmailDomain`). Gli istituti non scelgono formato né dominio. |
| P2 | Parte locale leggibile `nome.cognome`, con suffisso numerico crescente (`nome.cognome2`, `…3`) in caso di collisione. Nessun identificativo opaco. |
| P3 | L'email è una sola per `User`, globalmente unica, **immutabile**, generata dal server e **mai riassegnata**, neanche dopo la chiusura. |
| P4 | Codice fiscale (CF) obbligatorio per creare un account tramite API istituzionale o import. Serve alla deduplica tra istituti. |
| P5 | Stati dell'account: `Active` e `Closed`. Nessuna anonimizzazione, nessuna cancellazione fisica di `User`. Un account chiuso può essere riattivato con stesso `User`, stessa email, stesso CF e storico intatto. |
| P6 | CF protetto: HMAC-SHA256 per unicità e ricerca, AES-256-GCM a livello applicativo per il valore in chiaro. Due chiavi distinte, con identificatore di versione, senza valori di default. |
| P7 | La casella di posta non è implementata (in futuro Microsoft 365). Esiste solo il punto di aggancio `IMailboxProvisioner` con implementazione vuota. Non esiste recupero password via email. |
| G0 | `institution_email_reservation` resta l'unica autorità di unicità degli indirizzi. Due trigger `BEFORE` bloccano `UPDATE` di `user.email` e `DELETE` su `user`. Nessuna tombstone, nessuna modifica a trigger o FK esistenti. |
| D-A | Persona già attiva in un altro istituto: risposta con la stessa forma, `temporaryPassword` `null`, password **mai** reimpostata. |
| D-B | Teardown dei test: deroga ai trigger G0 legata all'istanza di test attestata, sul modello dei trigger `mt5c_*`. |
| D-C | Conta come attiva solo una membership `active` in un'istituzione `active`. Se una membership torna attiva, l'account chiuso viene riattivato (nella stessa transazione e, come rete di sicurezza, dal job). |
| D-D | `tools/OwnerProvisioning` non cambia: il primo InstitutionAdmin si crea con email scelta a mano e senza CF. |
| E1 | Persona già presente nello stesso istituto: `409` neutro. |
| E2 | Nome e cognome sempre obbligatori; con una scheda collegata devono coincidere con la scheda (senza maiuscole e accenti), altrimenti `422`. |
| E3 | Scadenza studenti: `joined_at` della prima membership con ruolo Student più la durata configurata. Uno studente con membership attiva non viene chiuso. |
| E4 | Import massivo di studenti con creazione degli account, fino a ~600 righe per file, in una sola transazione. |

## 2. Modello dati

La migrazione `migrations/platform-identity/001_expand.sql` è additiva e si applica **dopo** la release
`er-quality-2-mysql84`. Registra il marker `er-platform-identity-1` in `er_schema_release`.

### 2.1 Colonne aggiunte a `user`

| Colonna | Tipo | Significato |
|---|---|---|
| `fiscal_code_hmac` | `char(64) ascii_bin NULL`, `UNIQUE` | HMAC-SHA256 esadecimale maiuscolo del CF normalizzato. `NULL` per gli account senza CF (primo admin D-D, dati precedenti). |
| `fiscal_code_hmac_key_version` | `varchar(32) ascii_bin NULL` | Versione della chiave HMAC che ha prodotto il valore. |
| `fiscal_code_enc` | `varbinary(64) NULL` | Nonce (12 byte) ‖ testo cifrato ‖ tag (16 byte), AES-256-GCM. |
| `fiscal_code_enc_key_version` | `varchar(32) ascii_bin NULL` | Versione della chiave di cifratura. |
| `lifecycle_state` | `enum('Active','Closed') ascii_bin NOT NULL DEFAULT 'Active'` | Stato del ciclo di vita. |
| `closed_at_utc` | `datetime(6) NULL` | Ultima chiusura (UTC). |
| `reactivated_at_utc` | `datetime(6) NULL` | Ultima riattivazione (UTC). |

Le quattro colonne del CF sono tutte valorizzate o tutte `NULL`. Nessuna colonna esistente viene rimossa
o modificata; `User.Role`, `StudentId` e `TeacherId` non cambiano e continuano a non concedere privilegi.

### 2.2 Tabella `identity_lifecycle_event`

Ledger append-only, senza dati personali, conservato per sempre.

| Colonna | Significato |
|---|---|
| `id` | ID deterministico: derivato da `user_id`, transizione e generazione di sicurezza raggiunta. Lo stesso passaggio ripetuto produce lo stesso ID e viene rifiutato dalla chiave primaria. |
| `user_id` | FK `RESTRICT` verso `user`. |
| `transition` | `Closed` o `Reactivated`. |
| `rule_code` | `NoActiveMembership`, `StudentPeriodElapsed`, `AccountRecreated`, `MembershipReactivated`, `InstitutionReactivated`, `ActiveMembershipFound`. |
| `rule_version` | Versione della regola applicata (oggi `1`). |
| `security_generation` | Generazione dell'identità dopo la transizione. `UNIQUE(user_id, transition, security_generation)`. |
| `occurred_at_utc` | Istante UTC. |

Trigger: `BEFORE INSERT` valida forma, valori ammessi e coerenza con lo stato corrente dell'utente;
`BEFORE UPDATE` e `BEFORE DELETE` rifiutano sempre.

### 2.3 Trigger su `user`

| Trigger | Regola |
|---|---|
| `platform_user_insert` | Stato iniziale `Active`, timestamp di ciclo di vita `NULL`, colonne CF tutte valorizzate o tutte `NULL`, formato di HMAC e versioni chiave. |
| `platform_user_update` | `email` immutabile (G0); colonne CF immutabili; transizioni ammesse solo `Active→Closed` (con nuovo `closed_at_utc`) e `Closed→Active` (con nuovo `reactivated_at_utc`). |
| `platform_user_delete` | `DELETE` fisico rifiutato (G0). |

**Perché bastano (G0).** Con l'email immutabile `email_user_update` non può più cancellare la riserva
del vecchio indirizzo; con il `DELETE` vietato il `CASCADE` di `fk_email_reservation_user`, che in MySQL
non attiva trigger, non può più scattare. `email_reservation_delete` continua a rifiutare la cancellazione
delle riserve ancora collegate a un utente. Le riserve restano quindi per sempre e un indirizzo emesso
non torna mai libero: non serve una tabella di tombstone.

**Deroga per i test (D-B).** I trigger `platform_user_update` e `platform_user_delete` contengono lo
stesso predicato dei trigger `mt5c_*`: istanza di test attestata, utente `root`, utente elencato in
`@er_mt5b_fixture_owners` e indirizzo `…example.test`. Nella forma pubblicata il predicato richiede
`VERSION()='5.7.24'` e non è mai vero su 8.4. Solo il bundle della fixture (`tools/mysql84-fixture.cjs`)
lo sostituisce con l'identità del container creato dal processo di test e con la capability casuale
`@er_mt5b_fixture_capability`. Un test dedicato dimostra il rifiuto fuori deroga.

### 2.4 Rilascio 8.4

La migrazione è un passo **successivo** alla release `er-quality-2-mysql84`, non una sua modifica:

- i file di `migrations/platform-identity/` sono nella closure di `tools/mysql84-release.cjs`
  (manifest rigenerato con `--write` secondo la regola §8 di `BACKEND_REVIEW.md`);
- `node tools/mysql84-release.cjs successor <config> <dir>` produce lo SQL legato a DEFINER e, per la
  sola fixture, al container attestato;
- la fixture di integrazione applica il bundle `er-quality-2-mysql84` e poi il successore;
- `contract.json` di er-quality-2 resta invariato: il contratto del successore è in
  `migrations/platform-identity/predecessor-contract.json`;
- `tools/Invoke-PlatformIdentityMigration.ps1` applica il successore a un container Docker
  `mysql:8.4.13` usa-e-getta già installato con er-quality-2.

Il codice rileva la presenza del marker. Senza migrazione il login funziona come prima e nessun account
può essere chiuso; con il flag attivo la readiness richiede il marker e risponde `503` se manca.

## 3. Normalizzazione dell'email

Funzione pura `PlatformEmailRules` (Domain).

1. Nome e cognome: trim; decomposizione Unicode NFKD e rimozione dei segni diacritici (`à→a`, `ç→c`).
2. Lettere speciali mappate: `ß→ss`, `æ→ae`, `œ→oe`, `ø→o`, `ł→l`, `đ→d`, `ð→d`, `þ→th`, `ı→i`.
3. Minuscolo invariante.
4. Apostrofi (`'`, `’`, `‘`, `` ` ``, `´`) rimossi: `D'Angelo → dangelo`.
5. Spazi, trattini, underscore e punti diventano `.`; ogni altro carattere non `[a-z0-9]` viene rimosso.
6. Punti consecutivi compressi, punti iniziali e finali rimossi.
7. Parte locale = `nome` + `.` + `cognome`; i nomi composti restano completi e ordinati come scritti
   (`Anna Maria` + `De Luca` → `anna.maria.de.luca`).
8. Se nome o cognome restano vuoti (per esempio solo caratteri non latini) la richiesta è rifiutata:
   non si inventa un identificativo opaco.
9. Lunghezze: parte locale ≤ 64 caratteri **compreso il suffisso**, indirizzo ≤ 254. Se serve, la base
   viene troncata a `64 - lunghezza(suffisso)` caratteri e poi ripulita dai punti finali. Il dominio è
   validato all'avvio (minuscolo, etichette DNS, massimo 189 caratteri).

Candidati: `base`, `base2`, `base3`, … fino a 9999 tentativi. Il suffisso non viene mai riusato perché
ogni indirizzo emesso resta riservato.

## 4. Allocazione

Nessun "controlla poi inserisci" non atomico: l'unicità la garantisce la chiave primaria di
`institution_email_reservation` (popolata dal trigger `email_user_insert`) e `uq_user_email`.

1. Transazione esplicita; lock del tenant (`institution … FOR UPDATE` e lock IAM esistenti).
2. Lock del mutex email globale `institution_email_mutex` (id=1), lo stesso dei trigger delle assegnazioni
   istituzionali. Ordine sempre tenant → mutex.
3. Lettura in blocco degli indirizzi già riservati che iniziano con le basi richieste
   (`address LIKE 'base%'`, a gruppi), filtrati esattamente in memoria con `^base(\d*)@dominio$`.
4. Scelta del primo candidato mai emesso, nell'ordine delle righe per l'import.
5. `INSERT` dell'utente. Su errore 1062 sull'indirizzo (scrittore concorrente che non usa il mutex, come
   lo script del proprietario) si passa al candidato successivo nella stessa transazione: in InnoDB
   l'errore annulla solo l'istruzione, trigger compresi. È lo stesso schema dell'attuale
   `AccountEmailUnavailableException`, che resta la risposta se i tentativi si esauriscono.

## 5. Codice fiscale

**Normalizzazione e validazione** (`FiscalCodeRules`, Domain): trim, maiuscolo invariante, 16 caratteri,
struttura `LLLLLL NN L NN L NNN L` con le cifre sostituibili dalle lettere di omocodia `LMNPQRSTUV`,
mese in `ABCDEHLMPRST`, giorno 1–31 o 41–71, carattere di controllo calcolato con le tabelle ufficiali
dispari/pari. Il CF numerico provvisorio di 11 cifre non è accettato.

**Protezione** (`IFiscalCodeProtector`, Infrastructure):

- `fiscal_code_hmac = HEX(HMAC-SHA256(chiave_hmac, UTF8(cf)))`, usato per `UNIQUE` e ricerca;
- `fiscal_code_enc = nonce ‖ AES-256-GCM(chiave_enc, cf, aad)`, con nonce casuale di 12 byte a ogni cifratura
  e `aad = "er-cf-v1|" + key_id + "|" + hmac`, così un testo cifrato non è trasferibile a un'altra riga;
- il CF in chiaro non compare in log, audit, eventi, intent hash o risposte API. Nessun endpoint lo mostra.
  `Unprotect` esiste ed è testato, per un futuro uso strettamente necessario.

**Chiavi** (`Multitenancy:PlatformIdentity`): `FiscalCodeHmacKey` e `FiscalCodeEncryptionKey` in base64,
con `FiscalCodeHmacKeyId` e `FiscalCodeEncryptionKeyId`. Nessun valore di default. Con il flag attivo
l'avvio viene rifiutato se una chiave manca, non è base64, è più corta di 32 byte (HMAC) o non è
esattamente di 32 byte (AES-256), se le due chiavi coincidono o se un identificatore non è valido.

**Rotazione** (documentata, non implementata):

- *Cifratura*: aggiungere una nuova coppia id/chiave, cifrare i nuovi valori con la nuova chiave e
  ricifrare i vecchi con una migrazione dedicata che legge `fiscal_code_enc_key_version`. Il trigger rende oggi
  immutabili le colonne CF: la ricifratura richiede una migrazione rivista che lo consenta.
- *HMAC*: cambiare la chiave invalida la ricerca. Serve ricalcolare tutti gli HMAC dai valori decifrati,
  in manutenzione, con verifica di unicità prima del commit. Fino ad allora la chiave HMAC non va cambiata.

## 6. Deduplica

La ricerca avviene sotto lock (`SELECT … FROM user WHERE fiscal_code_hmac=? FOR UPDATE`, dopo il mutex).

| Caso | Effetto | `temporaryPassword` |
|---|---|---|
| CF assente sulla piattaforma | Nuovo `User`, email allocata, membership, ruoli e profili. | Nuova |
| CF di un account `Active` | Solo membership, ruoli e profili nel tenant del token. Email invariata, password invariata (D-A). | `null` |
| CF di un account `Closed` | Riattivazione: stato `Active`, nuova password temporanea, generazione +1, evento `Reactivated/AccountRecreated`, poi membership, ruoli e profili. | Nuova |
| Persona già con una membership nello stesso istituto (qualsiasi stato) | Nessuna modifica, `409` neutro (E1). | — |

La risposta ha sempre la stessa forma (`eventId`, `userId`, `membershipId`, `email`, `roles`, `studentId`,
`teacherId`, `replayed`, `temporaryPassword`). L'istituto B scopre al massimo che il CF ha già un account
attivo sulla piattaforma (password `null`), mai in quale istituto, con quali ruoli o da quando. Nessun dato
didattico di A è letto o restituito. Il costo BCrypt è identico in tutti i rami perché l'hash viene
calcolato prima di accedere al database.

**Limite noto (D-D).** Il primo InstitutionAdmin creato da `tools/OwnerProvisioning` non ha CF: non entra
nella deduplica. Se la stessa persona viene poi creata da un istituto con il CF, nasce un secondo `User`.

## 7. Macchina a stati e regole di chiusura

```text
          job: nessuna membership attiva (+ regola studenti)
 Active ────────────────────────────────────────────────▶ Closed
   ▲                                                         │
   └──── creazione con lo stesso CF (nuova password) ────────┤
   └──── membership tornata attiva / istituto riattivato ────┘   (password invariata)
```

**Membership attiva**: `institution_membership.status='active'` in un'istituzione `status='active'`.
Le membership `pending`, `suspended`, `left` e quelle di istituti `provisioning`, `suspended` o `archived`
non contano.

**Candidato alla chiusura**: account `Active` senza membership attiva e senza invito personale
`pending` non scaduto (un invito in corso va prima accettato o lasciato scadere).

**Regola per ruolo** (`PlatformLifecycleRules`, funzione pura):

- nessuna membership con ruolo Student (docenti, staff, admin, account senza ruoli): chiusura alla prima passata;
- almeno una membership con ruolo Student: chiusura solo quando sono trascorsi `StudentClosureAfterMonths`
  mesi (36 nella configurazione d'esempio) dal `joined_at` **minimo** tra le membership con ruolo Student,
  di qualsiasi stato;
- ruoli multipli: vale la regola più restrittiva, cioè servono entrambe le condizioni.

**Caso limite (E3).** Uno studente con membership ancora attiva dopo i 3 anni (ripetente, percorso
prolungato) non viene chiuso, perché la chiusura richiede sempre l'assenza di membership attive. La regola
non è configurabile.

**Limite noto (E3).** `institution_membership_role` non ha una data. Se il ruolo Student viene aggiunto a
una membership esistente, la scadenza parte dal `joined_at` della membership e può arrivare prima del dovuto.

**Effetti della chiusura**: `lifecycle_state='Closed'`, `closed_at_utc`, generazione dell'identità +1
(tutti i token emessi prima sono revocati), evento append-only, `IMailboxProvisioner.SuspendAsync` (no-op).

**Login**: `IdentityAccountRepository` tratta un account `Closed` come inesistente. `LoginAsync` esegue
quindi `VerifyAbsent` con lo stesso costo BCrypt e restituisce la stessa risposta di un indirizzo
sconosciuto; `TicketAsync`, la selezione dell'istituto, le sessioni e il login Microsoft rifiutano l'account.
Nessun nuovo messaggio rivela lo stato.

**Riattivazione** (D-C): nella stessa transazione di
- `InstitutionMembershipMutationRepository` azione `Reactivate`;
- `InstitutionGovernanceRepository` riattivazione dell'istituto (`suspended → active`);
- creazione con lo stesso CF (con nuova password).

Il job, come rete di sicurezza, riattiva gli account `Closed` che hanno una membership attiva
(`ActiveMembershipFound`). Le riattivazioni da membership non cambiano la password e avanzano la
generazione una sola volta.

## 8. Job di ciclo di vita

`PlatformLifecycleWorker` (`Api/Operations`), sul modello di `InstitutionImportRetentionWorker`:
`BackgroundService` `internal sealed`, `PeriodicTimer` con `LifecycleInterval` (un giorno nella
configurazione d'esempio), nuovo scope DI a ogni passata, uscita pulita su cancellazione, log del solo tipo
di eccezione. Attivo solo con il flag.

Ogni passata: prima le riattivazioni di sicurezza, poi le chiusure, entrambe a lotti (`LifecycleBatchSize`)
con paginazione per `id`. Ogni account in una transazione propria (`READ COMMITTED`):
`SELECT … FOR UPDATE` dell'utente, rilettura delle membership, rivalutazione della regola,
`UPDATE … WHERE lifecycle_state='Active'`, avanzamento della generazione, evento con ID deterministico,
mailbox no-op, `faults.BeforeCommitAsync("LifecycleClose")`, commit. Una riga che fallisce viene registrata
e non blocca le altre. Due passate consecutive non producono effetti duplicati.

## 9. Policy email per istituto

Con il flag attivo le scritture del modulo email per istituto (`email-policies` POST/PUT,
`email-assignments` POST, `create-person`, `override`, `retire`) rispondono come il modulo disabilitato
(`403`). Letture, storico, tabelle e audit restano. Gli indirizzi già assegnati non vengono rilasciati e
restano nel namespace globale, quindi un indirizzo di piattaforma non può collidere con loro. La modalità
di import `ReserveInstitutionalAddress` è rifiutata. Il frontend non usa questi endpoint.

## 10. Contratto API

### `POST /api/institution/accounts`

Abilitato da `Multitenancy:SchoolApi`, `Multitenancy:MembershipWriteApi` **e** `Multitenancy:PlatformIdentity`
(altrimenti `404`). InstitutionAdmin crea ogni ruolo; Staff solo Student o Teacher.

```json
{
  "eventId": "GUID",
  "firstName": "Mario",
  "lastName": "Rossi",
  "fiscalCode": "RSSMRA80A01H501U",
  "roles": ["Student"],
  "classId": null,
  "studentId": null,
  "teacherId": null,
  "reason": "Iscrizione"
}
```

Campi sconosciuti (compreso `email`) → `400`. Risposte: `201` creazione, `200` replay (senza password),
`400` richiesta non valida (con messaggio), `409` EventId con intento diverso o persona già nell'istituto,
`422` nome/cognome diversi dalla scheda collegata.

`intent_hash` = SHA-256 di attore, `eventId`, HMAC del CF, ruoli ordinati, `studentId`, `teacherId`, nomi,
classe e motivo. Mai il CF in chiaro, mai l'email generata (il replay è deterministico).

### Import massivo (E4)

Modalità `CreatePlatformAccounts`, solo categoria Student, solo con il flag attivo. Vedi
[INSTITUTION_BULK_IMPORT.md](../api/INSTITUTION_BULK_IMPORT.md).

- Colonne: `Nome`, `Cognome`, `CodiceFiscale` obbligatorie; `Classe` e `RiferimentoEsterno` facoltative.
  Una colonna email nel file fa rifiutare il file.
- Il CF viene cifrato prima di salvare la preview: nel payload di staging non c'è mai in chiaro.
- Errori di riga, tutti riportati: CF mancante o non valido, CF duplicato nel file, CF già presente nello
  stesso istituto. La conferma con righe errate risponde `422` con l'elenco completo righe e codici.
- Gli omonimi nel file ricevono suffissi deterministici nell'ordine delle righe.
- CF già presenti in altri istituti o chiusi: collegamento o riattivazione come nella creazione singola.
- Una sola transazione per l'intero file, mutex email globale, candidati calcolati in memoria e verifica delle
  collisioni in blocco. Password temporanee e indirizzi compaiono solo nella risposta della prima conferma.

## 11. Configurazione

Sezione `Multitenancy:PlatformIdentity` (classe `PlatformIdentityOptions` in `Infrastructure/Options`).

| Chiave | Esempio | Regola |
|---|---|---|
| `Enabled` | `false` | Flag di rollout, `false` per default. |
| `EmailDomain` | `its.example` | Obbligatoria con il flag attivo. |
| `FiscalCodeHmacKeyId` / `FiscalCodeHmacKey` | — | Obbligatorie, nessun default, ≥ 32 byte. |
| `FiscalCodeEncryptionKeyId` / `FiscalCodeEncryptionKey` | — | Obbligatorie, nessun default, 32 byte. |
| `StudentClosureAfterMonths` | `36` | Obbligatoria, 1–240. |
| `LifecycleInterval` | `1.00:00:00` | Obbligatoria, da 1 minuto a 7 giorni. |
| `LifecycleBatchSize` | `200` | Obbligatoria, 1–1000. |

Le durate non sono costanti nel codice: i valori d'esempio sono in `appsettings.example.json`.

## 12. Test previsti

- Unit: normalizzazione email (accenti, apostrofi, composti, lunghezze), suffissi; CF e carattere di
  controllo; HMAC deterministico, cifratura (round-trip, nonce diverso, chiave errata); regola di chiusura
  per ogni combinazione di ruoli e date; validazione delle opzioni.
- Integrazione (MySQL 8.4.13 in Testcontainers): concorrenza sugli omonimi, indirizzo mai riassegnato,
  `DELETE`/`UPDATE email` bloccati fuori deroga e consentiti nella deroga, trigger append-only,
  `002_validate.sql` a zero; test A/B tra tenant; idempotenza; job; login; avvio con chiavi errate;
  sospensione e revoca; primo admin senza CF; import da 600 righe con misura dei tempi.

## 13. Limiti noti

- Primo admin senza CF fuori dalla deduplica (D-D).
- Data di inizio studente approssimata dal `joined_at` della membership (E3).
- Nomi senza caratteri latini rappresentabili: account non creabile, nessuna traslitterazione.
- Un account chiuso che riceve un invito non può accettarlo finché non viene riattivato (il login è rifiutato).
  L'istituto usa la creazione con CF, che lo riattiva.
- Con il CF l'istituto B può sapere che la persona ha già un account sulla piattaforma (D-A).
- Il mutex email globale serializza creazioni e import tra tutti gli istituti per la durata della transazione.
- Rotazione delle chiavi documentata, non implementata.
- Nessuna integrazione Microsoft 365 e nessuna casella reale.
