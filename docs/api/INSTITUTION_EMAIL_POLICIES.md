# Email istituzionali — Fase 2

Il modulo assegna **indirizzi applicativi riservati**, separati dall'identità globale `User.Email`. Non crea caselle, User, password, membership, ruoli, alias di login o collegamenti Microsoft. La pubblicazione di una policy approva una convenzione locale: non verifica la titolarità del dominio. `DomainVerificationState` vale `ConfiguredUnverified`; `MailboxVerified`, `DomainVerified` e `LoginEnabled` sono falsi. Non esiste in questa fase un endpoint per dichiararli veri.

L'abilitazione richiede sia il rollout istituzionale esistente sia `Multitenancy:InstitutionEmail:Enabled=true`, disabilitato per default. Applicare e verificare lo schema prima di abilitare il modulo. Il software non applica migrazioni all'avvio.

## Template e grammatica

La parte locale e il dominio sono campi distinti, persistenti per Fondazione e categoria. Nessun formato, prefisso o dominio è imposto globalmente.

```ebnf
template       = (literal | "{{" | "}}" | placeholder)+ ;
placeholder    = "{" field (":" transformation){0,4} "}" ;
field          = "nome" | "cognome" ;
transformation = "iniziale" | "minuscolo" | "maiuscolo" | "primi(" number ")" ;
number         = integer in [1,64], without leading zeroes ;
suffix         = allowed-literals "{n}" allowed-literals ;
```

`literal` comprende i caratteri ASCII dot-atom: lettere, cifre, `!#$%&'*+-/=?^_` e backtick, barra verticale, tilde e punto. Le parentesi graffe letterali richiedono `{{` e `}}`. `@` non appartiene al template. Il punto non può iniziare, terminare o ripetersi consecutivamente nella parte locale finale. Le trasformazioni si applicano da sinistra a destra dopo la normalizzazione; i loro nomi sono case-sensitive. Non esistono espressioni, proprietà arbitrarie, codice, regex configurabili o accessi a database nel generatore puro.

Limiti: template 1–512 caratteri, massimo 32 placeholder e quattro trasformazioni per placeholder; input di ogni nome usato 1–256 caratteri; parte locale ASCII 1–64; indirizzo completo massimo 254. Il parser valida struttura e caratteri prima della pubblicazione. Ogni risultato è validato nuovamente: nomi che normalizzano a stringa vuota e risultati troppo lunghi falliscono senza troncamento silenzioso.

## Normalizzazione

`names` contiene `case` (`Lower`/`Upper`), `accents` (`LatinFoldV1`/`RejectNonAscii`), `wordSeparator` (`""`, `"."`, `"-"`, `"_"`) e `punctuation` (`Remove`/`WordBoundary`). Questi valori sono stringhe rigorose; valori numerici e campi JSON sconosciuti sono rifiutati.

`LatinFoldV1` decompone Unicode in FormD e rimuove i segni combinanti. Supporta esplicitamente anche ß→ss, Æ→ae, Ø→o, Ł→l, Œ→oe e le corrispondenti maiuscole. Non è una traslitterazione universale: alfabeti o simboli non convertibili, UTF-16 malformato e caratteri di controllo/formato richiedono correzione amministrativa. `RejectNonAscii` rifiuta i caratteri non ASCII. Spazi multipli diventano un solo confine di parola; apostrofi e trattini sono rimossi oppure trattati come confini secondo `punctuation`. `wordSeparator` determina come rendere i confini. Il default vuoto rimuove gli spazi, ma ogni Fondazione può scegliere diversamente.

Il dominio deve essere un nome DNS ASCII con almeno due etichette, ciascuna lunga 1–63 caratteri, lettere/cifre/trattini e senza trattino ai bordi. Non sono accettati URL, porte, percorsi, spazi, punti finali o U-label Unicode. Un A-label punycode già convertito è accettato sintatticamente; non costituisce verifica IDN, DNS o di titolarità. Il dominio è normalizzato in minuscolo. La parte locale può conservare le maiuscole configurate; la riserva usa la chiave in minuscolo.

## Esempio ITS Umbria

```json
{
  "localPartTemplate": "allievo_{cognome}.{nome:iniziale}",
  "domain": "itsumbria.it",
  "names": { "case": "Lower", "accents": "LatinFoldV1", "wordSeparator": "", "punctuation": "Remove" },
  "collision": "NumericSuffix",
  "numericSuffix": "_{n}",
  "suffixStart": 2,
  "maximumAttempts": 32
}
```

Mario Rossi → `allievo_rossi.m@itsumbria.it`; Giulia Bianchi → `allievo_bianchi.g@itsumbria.it`; Luca De Luca → `allievo_deluca.l@itsumbria.it`. L'ultimo risultato deriva dalla scelta esplicita `wordSeparator=""`.

Sono configurazioni equivalenti del medesimo motore, per esempio, `{nome}.{cognome}`, `{cognome}.{nome}`, `docente-{cognome}-{nome}`, `x+{nome:primi(3)}_{cognome:primi(2)}-2027` e `{cognome}{nome:iniziale}`. Il dominio è sempre indipendente dal formato.

## Categorie, versioni e destinatari

`Student`, `Teacher`, `Staff`, `InstitutionAdmin` sono categorie di generazione, distinte dai ruoli IAM. Una membership multiruolo non produce indirizzi automaticamente. L'amministratore sceglie esplicitamente una categoria primaria; l'integrità impedisce assegnazioni multiple alla stessa anagrafica o membership. Il collegamento successivo di profili che unirebbe destinatari già assegnati richiede riconciliazione e viene rifiutato. La Fase 2 non offre un workflow di unificazione automatica.

Ogni categoria ha un `PolicyId` stabile e revisioni append-only per Fondazione. `ExpectedRevision=0` crea la categoria; aggiornamenti richiedono la revisione corrente. Stati: `Draft`, `Published`, `Disabled`. Solo l'ultima revisione `Published` può riservare nuovi indirizzi. Una nuova revisione, anche Draft o Disabled, sospende le nuove riserve finché non viene pubblicata una successiva versione. Le assegnazioni storiche continuano a riferire la versione utilizzata e non vengono rinominate.

Un destinatario specifica **esattamente uno** fra `studentId`, `teacherId`, `membershipId`. Per Student/Teacher i nomi e l'eventuale membership canonica derivano dal database tenant-scoped; il client non può sostituirli. Le anagrafiche possono non avere User. Una destinazione membership-only richiede Admin, categoria Staff/InstitutionAdmin e nomi espliciti, perché User globale non possiede questi campi. La categoria scelta non attribuisce alcun ruolo.

## Autorizzazioni

| Operazione | InstitutionAdmin | Staff | Teacher/Student |
|---|---|---|---|
| Leggere policy | Tutte del tenant | Student/Teacher del tenant | No |
| Anteprima configurazione libera | Sì | No | No |
| Anteprima policy pubblicata | Sì | Student/Teacher | No |
| Creare/versionare policy e leggere audit | Sì | No | No |
| Riservare per Student/Teacher | Sì | Sì | No |
| Creare anagrafica insieme alla riserva | Sì | Student/Teacher | No |
| Assegnare a membership senza profilo | Sì | No | No |
| Leggere assegnazioni | Tenant | Student/Teacher tenant | Solo proprie |
| Override/ritiro con motivazione | Sì | No | No |
| Eliminare assegnazioni/audit | No | No | No |
| Verificare mailbox/dominio o cambiare login | Nessun endpoint | Nessun endpoint | Nessun endpoint |

Ruoli, tenant e attore derivano esclusivamente dal contesto InstitutionAccess rivalidato. Non sono accettati InstitutionId, ActorUserId, ActorMembershipId o ruoli dichiarati nel body. Membership revocate/sospese, generation obsolete, token Selection/legacy e operatori di piattaforma non ottengono privilegi tramite questo modulo. Admin+Staff mantiene Admin; Staff+Teacher/Student non ottiene override o DELETE. Non serve un profilo docente per operare come Staff.

## Contratti API

Tutti i percorsi iniziano con `/api/institution`. Le risposte delle mutazioni sono HTTP 200 con la rappresentazione risultante. JSON usa camelCase e enum stringa rigorosi.

| Metodo e percorso | Corpo / parametri |
|---|---|
| GET `email-policies` | Ultima revisione di ogni categoria visibile |
| GET `email-policies/{category}` | `revision` opzionale |
| POST/PUT `email-policies/{category}` | `eventId`, `expectedRevision`, `state`, `template`, `reason` |
| GET `email-policies/{category}/history` | `page=1`, `size=20` |
| POST `email-policies/preview` | `category`, `policyRevision`, `firstName`, `lastName`; `draft` opzionale solo Admin |
| GET `email-assignments` | `page=1`, `size=20`; risposta pageNumber/pageSize/totalCount/items |
| GET `email-assignments/{id}` | Rappresentazione filtrata per tenant/ownership |
| POST `email-assignments` | `eventId`, `category`, `policyRevision`, `recipient` |
| POST `email-assignments/create-person` | `eventId`, `category` Student/Teacher, `policyRevision`, `firstName`, `lastName`, `classId` opzionale solo Student |
| POST `email-assignments/{id}/override` | `eventId`, `expectedRevision`, `policyRevision`, `email`, `reason` |
| POST `email-assignments/{id}/retire` | `eventId`, `expectedRevision`, `reason` |
| GET `email-assignments/{id}/history` | `page=1`, `size=20` |

Il workflow `create-person` crea anagrafica e riserva atomicamente attraverso i repository Student/Teacher esistenti. Non collega membership. Replay EventId restituisce gli stessi identificativi senza creare una seconda persona. Una collisione o un errore audit annulla entrambe le scritture. Le API anagrafiche preesistenti continuano a creare soltanto anagrafiche: per l'assegnazione successiva usare POST `email-assignments`.

La preview restituisce `email`, `normalizedEmail`, `localPart`, `domain`, `warnings`. Non controlla disponibilità, non legge il registro globale e non effettua scritture. Un indirizzo valido in preview può collidere in assegnazione. Le pagine hanno size 1–100; history è accessibile solo Admin. Dimensione richiesta massima 16 KiB.

## Collisioni, override e concorrenza

`Reject` rifiuta la collisione con 409. `NumericSuffix` prova prima l'indirizzo base, poi i numeri da `suffixStart` (2–9999), fino a `maximumAttempts` (1–100, incluso il tentativo base). Il suffisso contiene esattamente un `{n}` e testo dot-atom, massimo 24 caratteri: `{n}`, `_{n}`, `-{n}` sono esempi. Non viene troncata una parte locale troppo lunga. L'indirizzo base è deterministico; il suffisso assegnato dipende dall'ordine delle riserve serializzate nel database, non da COUNT+1.

Il registro globale `institution_email_reservation` ha PK effettiva sull'indirizzo con la stessa collation di User.Email, `utf8mb4_unicode_ci`. I trigger di INSERT/UPDATE di User e delle assegnazioni partecipano allo stesso namespace: non esiste una garanzia basata solo su SELECT fra tabelle. Il mutex database serializza le assegnazioni; anche i writer User concorrenti devono superare l'INSERT UNIQUE. Una collisione con User.Email viene rifiutata anche se l'utente è collegato alla persona: nessun collegamento identitario implicito.

L'indirizzo istituzionale precedente a un override o ritirato resta riservato per sempre. Il ritiro non elimina dati e non consente di riassegnare il vecchio indirizzo. Un override Admin richiede un nuovo indirizzo nel dominio della policy pubblicata corrente, motivazione e ExpectedRevision. Ripristinare un vecchio indirizzo già riservato non è supportato. Per compatibilità, una riserva del solo User globale segue invece le modifiche/eliminazioni di quel User: non è storico istituzionale.

EventId è obbligatorio e unico nel tenant. Stesso attore e stesso intento canonico restituiscono il risultato storico; riuso per un intento diverso produce 409. Revision e disponibilità sono rivalidate durante la mutazione. Ordine lock: istituzione/IAM, membership/ruoli/security state, mutex globale degli indirizzi, scritture. Nessun lock in memoria o cache dei permessi. L'audit registra tenant, attore User/membership, operazione, destinatario, versione policy, prima/dopo, UTC e motivazione; trigger vietano UPDATE/DELETE. Non viene inventato storico per User legacy: il backfill registra soltanto le riserve correnti.

## Errori e frontend futuro

400: JSON/campi/enum/template/dominio/nome non validi; 401: autenticazione assente o token non valido; 403: ruolo, rollout o membership non autorizzati; 404: destinatario/assegnazione non visibile o cross-tenant, senza rivelarne l'esistenza; 409: revisione obsoleta, EventId riutilizzato diversamente, collisione, destinatario già assegnato o vincolo di integrità. Gli errori SQL non restituiscono indirizzi o identità di altri tenant.

Il frontend deve conservare EventId per il replay della medesima intenzione, inviare ExpectedRevision corrente, mostrare i warning di preview e distinguere sempre riserva da mailbox. Non deve presentare `Published` come verifica dominio. Se cambia la policy fra preview e assegnazione, ricaricare la configurazione dopo 409. Non inviare automaticamente inviti sensibili a un indirizzo soltanto riservato.

Il generatore `IInstitutionEmailGenerator` è puro e riutilizzabile. Parsing/normalizzazione/generazione sono separati dalla riserva transazionale `IInstitutionEmailRepository` e dal manager applicativo. La futura importazione massiva potrà usare gli stessi contratti; questa fase non implementa CSV/Excel, provider email, DNS verification, invio inviti, frontend o migrazioni automatiche degli indirizzi esistenti.

## Verifiche e limiti di consegna

Consultare `tests/Integration/INSTITUTION_EMAIL_POLICY_REPORT.md` e il relativo JSON per risultati realmente eseguiti, provenienza e blocchi residui. Le verifiche Python precedenti alla Fase 2 non certificano i nuovi file o il nuovo manifest. DDL MySQL implicitamente committed: backup, manutenzione e verifica successiva sono necessari; non è promesso rollback transazionale della migrazione.
