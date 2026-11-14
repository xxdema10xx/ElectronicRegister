# Account di piattaforma: creazione, deduplica e ciclo di vita

Contratto corrente di `POST /api/institution/accounts` con l'identità di piattaforma
(`Multitenancy:PlatformIdentity`). Design e decisioni: [PLATFORM_IDENTITY_AND_LIFECYCLE.md](../architecture/PLATFORM_IDENTITY_AND_LIFECYCLE.md).
Import massivo degli studenti: [INSTITUTION_BULK_IMPORT.md](INSTITUTION_BULK_IMPORT.md#account-di-piattaforma-createplatformaccounts).

## Abilitazione

L'endpoint risponde `404` se manca uno di questi valori:

| Chiave | Valore |
|---|---|
| `Multitenancy:SchoolApi:Enabled` | `true` |
| `Multitenancy:MembershipWriteApi:Enabled` | `true` |
| `Multitenancy:PlatformIdentity:Enabled` | `true` (default `false`) |
| `Deployment:RolloutApproved` | `true` in Staging e Production |

Con `PlatformIdentity` attivo servono anche dominio, chiavi e durate (`appsettings.example.json`); senza,
l'API non si avvia. Lo schema `er-platform-identity-1` è obbligatorio: senza, `/health/ready` risponde `503`.

## Richiesta

InstitutionAdmin crea account di ogni ruolo; Staff solo `["Student"]` o `["Teacher"]`. L'istituto viene dal token.

```json
{
  "eventId": "6f1c0c2e-8f4e-4c55-9d1e-0d6a8d2a4b11",
  "firstName": "Anna Maria",
  "lastName": "De Luca",
  "fiscalCode": "RSSMRA80A01H501U",
  "roles": ["Student"],
  "classId": null,
  "studentId": null,
  "teacherId": null,
  "reason": "Iscrizione al corso"
}
```

| Campo | Regola |
|---|---|
| `eventId` | Obbligatorio, idempotenza. Stesso `eventId` e stessa intenzione: `200` con gli stessi id, senza password. Intenzione diversa: `409` |
| `firstName`, `lastName` | Obbligatori per ogni ruolo, massimo 100 caratteri. Con `studentId`/`teacherId` devono coincidere con la scheda (senza maiuscole e accenti), altrimenti `422` |
| `fiscalCode` | Obbligatorio: codice fiscale personale di 16 caratteri, con omocodia e carattere di controllo. Mai restituito, registrato o salvato in chiaro |
| `roles` | Da 1 a 4 ruoli distinti, non insieme Student e Teacher |
| `studentId` / `teacherId` | Facoltativi: collegano una scheda esistente dello stesso istituto |
| `classId` | Facoltativo, solo per una nuova scheda Student |
| `reason` | Obbligatoria, massimo 500 caratteri |

Non esiste un campo `email`: inviarlo produce `400` (campo sconosciuto), come `institutionId` o un `role` fuori dall'array.

## Risposta

Stessi campi in ogni caso (persona nuova, già attiva altrove, riattivata). `temporaryPassword` è sempre presente.

```json
{
  "eventId": "6f1c0c2e-8f4e-4c55-9d1e-0d6a8d2a4b11",
  "userId": "…",
  "membershipId": "…",
  "email": "anna.maria.de.luca@its.example",
  "roles": ["Student"],
  "studentId": "…",
  "replayed": false,
  "temporaryPassword": "AbCdE-FgHjK-LmNpQ-RsTuV"
}
```

| Caso | Codice | `email` | `temporaryPassword` |
|---|---|---|---|
| Persona nuova | `201` | nuovo indirizzo `nome.cognome[N]` | nuova |
| Codice fiscale di un account attivo (altro istituto) | `201` | indirizzo esistente | `null` (la password non viene mai reimpostata) |
| Codice fiscale di un account chiuso | `201` | indirizzo esistente | nuova (riattivazione) |
| Replay dello stesso `eventId` | `200` | indirizzo corrente | `null` |

`Cache-Control: no-store`. Nessun dato dell'altro istituto (id, ruoli, schede, date) compare nella risposta.

| Errore | Quando |
|---|---|
| `400` | Richiesta non valida, con `detail` esplicito (codice fiscale, nome, cognome, ruoli, classe, motivazione) |
| `403` | Ruolo dell'attore non autorizzato |
| `404` | Scheda di un altro istituto o inesistente, oppure endpoint spento |
| `409` | Persona già presente nell'istituto (qualsiasi stato della membership), scheda già collegata, `eventId` riusato con altra intenzione |
| `409 "Indirizzo email non disponibile"` | Esauriti i candidati dell'indirizzo |
| `422` | Nome e cognome diversi dalla scheda collegata |
| `503` (`code: "PlatformBusy"`, header `Retry-After: 2`) | Deadlock o lock wait timeout persistenti dopo 4 tentativi automatici. La transazione è annullata per intero: nessun account, membership né indirizzo riservato. Riprovare con lo **stesso** `eventId`. Non è mai il `409` neutro: quello significa "persona già nell'istituto" |

### Conflitti di concorrenza (A21)

La creazione singola e ogni azione dell'import (anteprima, conferma) vengono **ripetute automaticamente** fino a
4 volte, con attesa casuale di 20-120 ms moltiplicata per il numero del tentativo, quando MySQL annulla la
transazione per deadlock (1213) o lock wait timeout (1205). È sicuro perché InnoDB annulla l'intera transazione,
la creazione è idempotente per `eventId` e gli hash BCrypt sono calcolati fuori dalla transazione (una sola volta).
Esauriti i tentativi la risposta è il `503` sopra, senza dettagli interni (né numero d'errore né testo SQL).

## Indirizzi

- Dominio unico di piattaforma (`EmailDomain`). Parte locale `nome.cognome`: minuscolo, senza accenti e apostrofi,
  spazi e trattini diventano punti; nomi composti completi (`anna.maria.de.luca`).
- Omonimi: `nome.cognome2`, `nome.cognome3`, … nell'ordine di creazione. Un indirizzo emesso non viene mai
  riassegnato, nemmeno dopo la chiusura dell'account. L'indirizzo non è modificabile.
- Se nome o cognome non contengono lettere rappresentabili la richiesta è rifiutata (`400`).

## Ciclo di vita

- Gli account senza membership attiva (membership `active` in istituzione `active`) vengono chiusi dal job:
  docenti, staff e admin alla prima passata; studenti trascorsi `StudentClosureAfterMonths` mesi dall'inizio della
  prima membership da studente. Chi ha un invito personale in corso non viene chiuso.
- Un account chiuso è trattato al login come un indirizzo inesistente (stessa risposta e stesso costo) e tutti i
  suoi token sono revocati.
- Si riapre: con una nuova creazione con lo stesso codice fiscale (nuova password), quando una sua membership torna
  attiva (revoca della sospensione, riattivazione dell'istituto: password invariata) o, come rete di sicurezza, dal job.
- Il primo InstitutionAdmin creato con `tools/OwnerProvisioning` non ha codice fiscale: non entra nella deduplica,
  ma segue lo stesso ciclo di vita.

## Policy email per istituto

Con `PlatformIdentity` attivo le scritture di `email-policies` e `email-assignments` rispondono come il modulo
disabilitato (`403`). Le letture e lo storico restano; gli indirizzi già assegnati non vengono rilasciati.

## Import con account: tempi, timeout e dimensione dei file

La conferma di `CreatePlatformAccounts` calcola un hash BCrypt (costo 11, invariato) per **ogni** riga, anche per le
persone già attive, perché il costo non deve rivelare chi esiste già (A17). Il lavoro è legato alla CPU:

| File da 600 righe (300 coppie di omonimi, 2 persone già attive, 1 chiusa) | Anteprima | Conferma |
|---|---:|---:|
| Tutti i core (16), prima della modifica | 1,1 s | 7,1 s |
| Tutti i core (16), dopo | 0,8 s | 7,8 s |
| Processo limitato a 2 core, prima | 0,9 s | 32,6 s |
| Processo limitato a 2 core, dopo | 0,8 s | 33,2 s |

(Misura con affinità di CPU del processo dell'API e del client di test a 2 core, container MySQL non limitato;
differenze di pochi decimi tra "prima" e "dopo" sono rumore.) I gradi di parallelismo sono ora limitati, ma i tempi
di una singola importazione non cambiano: il costo è circa **0,055 s per riga su 2 core** (misurato; stimato 0,11 s su 1
core, misurato 0,013 s su 16).

- `Multitenancy:PlatformIdentity:ImportHashParallelism` (facoltativo, 1-32): thread di hashing; di default uno per
  core disponibile, mai più dei core. Il limite è **condiviso da tutte le importazioni in corso nel processo**: più
  conferme contemporanee non possono occupare più thread di quelli configurati. Con meno thread altre richieste
  restano reattive ma la conferma dura di più.
- Le importazioni restano sincrone (nessun `202`): **configurare proxy e load balancer con un timeout di almeno
  120 s** per `POST /api/institution/imports/{id}/confirm`.
- Limite consigliato: **600 righe per file** su istanze con 2 vCPU (circa 33 s). Il limite tecnico del parser è 2000
  righe, che su 2 vCPU richiedono circa 110 s (stimato, proporzionale): oltre le 1000 righe o con 1 vCPU serve un
  timeout maggiore di 120 s. Dividere i file più grandi.
- L'operazione resta atomica: un errore a metà annulla il file intero, senza password, account o indirizzi riservati.
