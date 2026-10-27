# Importazione istituzionale Student/Teacher — CreateOnly

Il modulo importa una sola categoria per file mediante **upload → parsing → preview persistente → conferma esplicita → commit AllOrNothing → risultato durevole**. Le righe non aggiornano anagrafiche esistenti e non effettuano merge. L'importazione crea solo Student/Teacher e, se richiesto, riserve email della Fase2. Non crea User, password, membership, profili collegati, mailbox, StudentEnrollment, CourseEdition, ClassSubject o trasferimenti.

## Requisiti, rollout e ruoli

Applicare e verificare lo schema prima dell'abilitazione. Non esistono migrazioni automatiche all'avvio. Servono InstitutionAccess corrente, Fondazione/membership attive, ruoli esatti e SecurityGeneration corrente. I flag `Multitenancy:SchoolApi:Enabled` e `Multitenancy:InstitutionImport:Enabled` devono essere true; il secondo è false per default. L'abilitazione viene ricontrollata nel manager, nel repository e prima del commit. La riserva email richiede anche il modulo email abilitato e una policy pubblicata Student/Teacher.

| Operazione | InstitutionAdmin | Staff | Teacher/Student/PlatformOperator |
|---|---|---|---|
| Template, upload, preview, mapping, conferma | Proprio tenant/attore | Proprio tenant/attore | No |
| Consultazione preview, righe, storico e risultato | Solo propri batch | Solo propri batch | No |
| Creazione Student/Teacher | Sì | Sì | No |
| Riserva secondo policy pubblicata | Sì | Sì | No |
| Cambiare policy/domino/ruoli, forzare email arbitrarie | Altri workflow Admin, non questo modulo | No | No |
| Eliminare anagrafiche/storico mediante import | Nessun endpoint | Nessun endpoint | Nessun endpoint |

Non è prevista collaborazione automatica tra attori: neppure un altro Admin dello stesso tenant può aprire una preview privata conoscendone l'ID. ImportId estranei o inesistenti restituiscono lo stesso404. La membership è rivalidata anche nei manager chiamati direttamente e sotto lock nel repository. Un ruolo legacy Admin non attribuisce permessi istituzionali; Staff+Teacher/Student mantiene i confini Staff. Non occorre un TeacherProfile per importare come Staff.

## Formati supportati

CSV UTF-8 rigoroso, con o senza BOM, separatore `;` o `,`, quoting `"..."`, escape `""`, separatori/newline dentro campi quotati, righe vuote, colonne riordinate, nomi accentati/composti e celle vuote. Auto-detection solo quando un solo separatore produce una struttura valida; altrimenti scegliere esplicitamente. Nessuna conversione silenziosa Windows-1252/UTF-16: esportare preventivamente UTF-8. Il digest SHA256 riguarda esattamente i byte caricati, BOM incluso.

XLSX è letto con **DocumentFormat.OpenXml3.5.1**, licenza MIT, target.NET10. La lettura è readonly, senza AutoSave, formule o esecuzione di codice. Prima dell'SDK, il preflight ZIP/XML verifica parti, percorsi, duplicati, dimensioni dichiarate ed effettivamente decompresse, ratio massimo1000, profondità XML32, budget nodi e complessità di celle/stringhe. DTD e resolver esterni sono disabilitati. Macro, oggetti binari/embedded, link esterni e formule sono rifiutati, inclusi valori cached. Il parser di dati XLSX è l'SDK ufficiale, non un parser ZIP/XML sostitutivo.

Per XLSX usare celle testo per nomi e riferimenti: numeri, date, booleani ed errori non vengono convertiti implicitamente, per evitare perdita di zeri o ambiguità. Stringhe inline, shared strings e rich text sono supportati. Se il workbook contiene più fogli, `worksheetName` deve sceglierne uno esplicitamente; viene importato soltanto quel foglio. XLSM/XLSB/XLS e altri formati sono rifiutati. Estensione e MIME non costituiscono prova di struttura valida; non vengono scaricati riferimenti esterni.

## Template CSV

Download autenticati: `GET /api/institution/imports/templates/students.csv` e `GET /api/institution/imports/templates/teachers.csv`. UTF-8 con BOM, intestazioni italiane, separatore`;`, dati fittizi; nessun dato di utenti reali.

```csv
Nome;Cognome;Classe;RiferimentoEsterno
Mario;Rossi;;STU-001
Giulia;Bianchi;;STU-002
```

```csv
Nome;Cognome;RiferimentoEsterno
Mario;Rossi;DOC-001
Anna Maria;Verdi;DOC-002
```

La classe vuota crea lo Student senza classe. Nel file reale può essere un nome classe del tenant o un GUID. Non esiste un download template XLSX: è facoltativo, mentre upload XLSX e download CSV sono implementati. Non sono inventati codice fiscale, telefono, data di nascita o matricola.

## Mapping colonne

Destinazioni chiuse: `FirstName`, `LastName`, `Class`, `ExternalReference`, `Ignore`. Indici zero-based; enum stringa rigorosi. Nessuna reflection arbitraria, SQL o fuzzy matching.

| Destinazione | Alias automatici |
|---|---|
| FirstName | Nome, FirstName, First Name |
| LastName | Cognome, LastName, Last Name, Surname |
| Class | Classe, Class |
| ExternalReference | RiferimentoEsterno, Riferimento Esterno, ExternalReference, External Reference |

Gli alias ignorano maiuscole/minuscole e spazi ai bordi. Colonne sconosciute sono ignorate con warning. Se due colonne corrispondono allo stesso campo, la preview segnala `AmbiguousMapping`; occorre selezionare esplicitamente la colonna corretta. Nome/cognome sono obbligatori. Un mapping manuale può omettere o ignorare colonne non usate. Indici duplicati/fuori intervallo, destinazioni sconosciute/numeriche e proprietà JSON aggiuntive sono rifiutati.

Id, InstitutionId, UserId, MembershipId, Role, PasswordHash, Revision e campi audit/verifica non sono destinazioni ammesse. Per Teacher una colonna Class deve essere ignorata esplicitamente; non attribuisce classi, materie o ClassSubject. Non esiste una modalità Update/Upsert.

## Upload e preview

`POST /api/institution/imports/preview`, multipart/form-data con **un solo** file, `settings` JSON e `parsing` JSON opzionale. Nessun InstitutionId/attore deve essere passato dal client.

`settings`:

```json
{
  "category": "Student",
  "emailMode": "None",
  "mapping": [
    {"columnIndex":0,"field":"FirstName"},
    {"columnIndex":1,"field":"LastName"},
    {"columnIndex":2,"field":"Class"},
    {"columnIndex":3,"field":"ExternalReference"}
  ]
}
```

Omettere `mapping` per il riconoscimento automatico. Categorie: Student/Teacher; modalità email: None/ReserveInstitutionalAddress. `parsing` CSV: `{"delimiter":";","encoding":"utf-8"}`; XLSX: `{"worksheetName":"Studenti"}`. Un separatore non si applica a XLSX e una selezione foglio non si applica a CSV.

Risposta201 con Location e batch: `id`, `category`, `state`, `revision`, `fileSha256`, `previewDigest`, `totalRows`, `validRows`, `invalidRows`, `createdAtUtc`, `expiresAtUtc`, `policyRevision`, `warnings`, `columns`, `mapping`. La preview può contenere errori ed essere comunque creata: nessuna anagrafica o riserva viene scritta. Un file strutturalmente invalido viene invece rifiutato prima dello staging.

I dati normalizzati e gli errori si leggono mediante `GET /{id}/rows?page=1&size=20`:

```json
{
  "pageNumber":1,"pageSize":20,"totalCount":1,
  "items":[{
    "data":{
      "rowNumber":2,"status":"Invalid","firstName":"Mario","lastName":"",
      "classReference":null,"externalReference":null,"emailPreview":null,
      "issues":[{"field":"LastName","code":"RequiredFieldMissing","message":"Campo obbligatorio mancante.","warning":false}]
    },
    "classId":null,"classFingerprint":null,"classCandidates":null
  }]
}
```

I numeri riga corrispondono alla riga fisica iniziale del record CSV o all'indice riga XLSX, non a un indice arbitrario del client. Stati: Valid, Warning, Invalid, AmbiguousReference. Warning non implica identità duplicata certa; errori bloccano l'intero commit. Colonne riconosciute, mapping, riferimenti risolti ed email simulate appartengono alla preview server-side, non a valori fidati ritrasmessi nella conferma.

## Validazione e classi

Trim dei valori, nome/cognome obbligatori e massimo100 caratteri come le anagrafiche attuali; riferimento esterno massimo100, testo classe massimo128. Controlli, caratteri di formato invisibili e UTF-16 malformato sono rifiutati. I prefissi formula `=`, `+`, `-`, `@` nei valori mappati sono rifiutati. Nessuna formula viene valutata.

Le classi sono cercate esclusivamente nel tenant corrente. Un nome non univoco produce AmbiguousClass e fino a tre `classCandidates` autorizzati; non viene scelta una classe arbitrariamente. Per disambiguare, modificare `settings.classes`, per esempio `[{"rowNumber":2,"classId":"GUID-DELLA-CLASSE"}]`. Un GUID esplicito nel file non può essere sostituito con un altro mediante disambiguazione. Classe inesistente e cross-tenant hanno lo stesso errore generico. Il fingerprint include ID, nome, percorso, eventuale CourseEdition e revisione classi: cambiamenti prima del commit richiedono nuova preview/conferma.

L'assegnazione è soltanto iniziale, tramite il repository Student esistente. Nessun StudentEnrollment o falso evento di trasferimento viene creato. Gli spostamenti successivi restano nel workflow di trasferimento. I docenti non ricevono assegnazioni didattiche.

Codici per riga/mapping: RequiredColumnMissing, AmbiguousMapping, IgnoredColumn, UnsupportedField, RequiredFieldMissing, ValueTooLong, InvalidCharacter, FormulaLikeValue, DuplicateWithinFile, DuplicateExternalReference, ExternalReferenceExists, PossibleExistingPerson, ClassNotFound, AmbiguousClass, InvalidClassChoice, EmailPolicyMissing, EmailPolicyCategoryMismatch, InvalidEmail, EmailCollisionWithinFile. Messaggi ed errori generici non riportano dati di altri tenant.

## Duplicati e referenze stabili

Nome+cognome non sono una chiave persona. Righe identiche e possibili omonimi esistenti generano warning, non merge o blocchi automatici per omonimia. La conferma deve riconoscere esplicitamente i warning.

Riferimenti esterni sono opzionali, ordinali/case-sensitive dopo trim e unici per **Fondazione e categoria**. Duplicati nel file rendono invalide entrambe le righe; un riferimento già registrato blocca CreateOnly. Il registro ha PK e FK reali: non è una SELECT preliminare priva di protezione concorrente. Le referenze non vengono liberate eliminando lo storico.

Un file byte-identico già completato nello stesso tenant/categoria genera FilePreviouslyImported. Per ripeterlo serve nuova preview, `allowRepeatedFile=true`, riconoscimento warning e motivazione non vuota. Non è un divieto assoluto basato sul digest: due omonimi legittimi senza referenze possono essere importati intenzionalmente. Un batch già completato non può diventare nuovo lavoro usando un EventId differente.

## Email istituzionali

None crea soltanto anagrafiche. ReserveInstitutionalAddress carica la policy pubblicata Student/Teacher corrente del tenant, riutilizza `IInstitutionEmailGenerator` e mostra candidati puri: **nessuna query sul namespace globale e nessuna riserva durante la preview**. La versione/identità policy è vincolata al digest confermato e alla FK del batch. Una modifica di policy invalida la conferma precedente.

Con `allievo_{cognome}.{nome:iniziale}` configurato nella policy e dominio itsumbria.it, Mario Rossi produce allievo_rossi.m@itsumbria.it; la rimozione degli spazi in De Luca dipende dalle regole di normalizzazione persistenti. Non è una convenzione hardcoded dell'importatore. Il generatore supporta tutte le policy della Fase2.

Collisioni interne seguono Reject/NumericSuffix nell'ordine delle righe. Al commit si riutilizza il percorso atomico Fase2, il registro globale UNIQUE, l'audit e le transazioni ambienti autorizzate. Ogni email effettivamente assegnata deve coincidere esattamente con quella mostrata. Se la disponibilità globale produce un candidato differente, viene effettuato rollback completo e restituito409: non viene cambiata silenziosamente l'email.

Per riconfermare un candidato numerico successivo si può modificare `settings.emailCandidates`, ad esempio `[{"rowNumber":3,"candidateOffset":1}]`. Offset0 è la base, offset1 il primo suffisso della policy. Gli offset sono limitati ai candidati NumericSuffix pubblicati e al massimo tentativi della policy; Reject consente soltanto0. Non sono override liberi: nome, cognome, dominio e grammatica restano quelli della policy. Anche dopo la nuova preview il commit accetta soltanto il primo candidato realmente assegnato dal servizio Fase2 se coincide con quello confermato; altrimenti ritorna409. Nessuna informazione sul titolare di altre riserve viene divulgata.

Gli indirizzi rimangono `reserved`, non mailbox verificate o identità globali. Nessun User.Email, password, ruolo, invito attendibile, identità Microsoft o provider mailbox viene modificato.

## Mapping, conferma e idempotenza

`PUT /{id}/mapping`:

```json
{
  "eventId":"GUID-UNIVOCO",
  "expectedRevision":1,
  "settings":{"category":"Student","emailMode":"None","classes":[{"rowNumber":2,"classId":"GUID-CLASSE"}]}
}
```

Rivalida i dati originali conservati dal server, sostituisce il mapping completo e incrementa revision/digest. Non modifica celle/anagrafiche né cambia categoria; per cambiare dati originali o categoria caricare un nuovo file. La scadenza non viene prorogata. Conservare gli indici di colonna necessari quando si invia un nuovo mapping esplicito.

`POST /{id}/confirm`:

```json
{
  "eventId":"GUID-UNIVOCO",
  "expectedRevision":2,
  "previewDigest":"SHA256-DELLA-PREVIEW",
  "acknowledgeWarnings":true,
  "allowRepeatedFile":false
}
```

`reason` è obbligatoria per una ripetizione intenzionale, massimo500 caratteri, senza controlli. La conferma non riceve nomi, ruoli, InstitutionId, UserId, email arbitrarie o target ID da creare. Utilizza il payload integro server-side associato a tenant, User e Membership.

Una sola transazione copre tutte le persone, referenze, riserve/assignment email, eventi email/import, item di risultato e stato Completed. Nessuna chiamata HTTP o commit per riga. Ordine lock IAM-first e ordine righe stabile. La revoca concorrente aspetta il completamento dell'operazione già autorizzata; operazioni successive con token obsoleto falliscono. Un errore SQL, riserva o audit annulla ogni scrittura transazionale del commit; la preview precedentemente persistita rimane disponibile fino alla scadenza.

EventId è unico per tenant e vincolato con SHA256 a operazione, batch, attore e intento canonico. Replay della stessa intenzione restituisce il risultato salvato con gli stessi ID, anche dopo perdita della risposta o restart. Riutilizzo con intento diverso →409. Due conferme concorrenti con stesso EventId producono lo stesso risultato; con EventId diversi soltanto una completa il batch. Il risultato è consultabile con `GET /{id}/result` e comprende importId, revision, createdCount e rowNumber/StudentId/TeacherId/EmailAssignmentId. Le liste di ID sono limitate dal numero massimo righe; non contengono nomi del file originale.

## Privacy, TTL e stati

Il file originale e il suo nome non sono conservati. Solo righe/setting necessari alla rimappatura sono memorizzati in JSON privato, limitato e tenant/attore-scoped. Non esistono file pubblici o path server scelti dal filename. L'eventuale buffering multipart è temporaneo del framework, non storage permanente del modulo; il deployment deve proteggere la directory temporanea del processo.

Stati: Preview, Completed, Expired. Il payload viene cancellato immediatamente al completamento. Una preview scaduta non espone più righe o intestazioni e non può essere confermata. Il worker effettivo di retention, distinto dall'importazione sincrona, ogni minuto processa fino a20 payload scaduti per passaggio, cancellando soltanto il JSON e aggiungendo un evento Retention con attore di servizio esplicito. La rimozione fisica può attendere il passaggio/backlog; l'accesso è bloccato alla scadenza. La quota per attore include payload già scaduti ma non ancora ripuliti, limitando l'accumulo.

Retention opera anche con ingestione disabilitata, rilevando read-only l'eventuale assenza dello schema. Non cancella eventi, referenze, item, anagrafiche o audit precedenti. L'audit conserva attore User/Membership per azioni interattive, tenant, hash file/preview/intento, categoria, policy/revisioni, operazione, UTC, motivazione e ID creati; le snapshot per revisione consentono confronto prima/dopo dei metadati. Non contiene le righe sorgenti o le email simulate. Nessuno storico viene inventato per dati legacy.

## Configurazione dei limiti

Chiavi sotto `Multitenancy:InstitutionImport`:

| Chiave | Default | Tetto/intervallo |
|---|---:|---:|
| TtlMinutes |30|1–120|
| MaxStageBytes |2MiB|1KiB–3MiB|
| MaxPendingPreviews |20 per attore|1–100|
| ConcurrentUploads |2|1–8, coda0|
| Limits:MaxFileBytes |10MiB|1–64MiB|
| Limits:MaxDataRows |2000|1–10000|
| Limits:MaxColumns |16|2–64|
| Limits:MaxCellCharacters |512|1–4096|
| Limits:MaxRecordCharacters |8192|almeno cella, massimo1MiB|
| Limits:MaxZipEntries |128|4–256|
| Limits:MaxUncompressedBytes |40MiB|1–128MiB|
| Limits:MaxXmlNodes |200000|1–1000000|

Il limite staging è indipendente da quello del file: un documento sintatticamente valido può superarlo e ricevere413. I limiti numerici richiedono configurazione valida all'avvio; non sono accettati dal client. Limite HTTP assoluto multipart64MiB+256KiB; settings256KiB, parsing2KiB, body JSON256KiB. Intestazioni massimo128 caratteri. Restano attivi i rate limit e timeout globali; non vengono disabilitati. Due upload concorrenti per default proteggono anche il costo di decompressione. La risorsa non viene elaborata in un job asincrono e non viene restituito202.

## API complete ed errori

Tutti i percorsi iniziano con `/api/institution/imports`:

| Metodo | Percorso | Risposta |
|---|---|---|
| GET | `/templates/students.csv` |200 CSV fittizio|
| GET | `/templates/teachers.csv` |200 CSV fittizio|
| POST | `/preview` |201 preview multipart|
| GET | `/` |200 storico proprio, page/size|
| GET | `/{id}` |200 metadati e colonne solo se preview attiva|
| GET | `/{id}/rows` |200 righe paginate, page/size|
| PUT | `/{id}/mapping` |200 nuova revisione o replay|
| POST | `/{id}/confirm` |200 risultato o replay|
| GET | `/{id}/result` |200 risultato Completed|

Page inizia da1, size1–100, default20. Non sono presenti DELETE, update di persone o endpoint di gestione ruoli. Non esiste export CSV di dati personali; i due download sono statici e privi di formula injection. Un futuro export di risultati dovrà neutralizzare formule e fare escaping CSV.

400 JSON/mapping/template/celle/file malformati;401 autenticazione assente/token incompatibile;403 permessi, generation, membership, Fondazione o rollout;404 batch estraneo/inesistente;409 digest/revisione obsoleti, warning non riconosciuti, quota preview, file ripetuto non autorizzato, collisioni o concorrenza;413 limiti;415 formato/codifica non supportati;422 righe invalide.429 segnala rate/concurrency limit;405 un metodo inesistente. ProblemDetails delle anomalie import include `code` stabile senza dettagli SQL o dati di altri tenant. Esempi409: StalePreview, PreviewContextChanged, PreviewExpired, AlreadyCompleted, EventIdConflict, WarningsNotAcknowledged, RepeatedFileNotAuthorized, EmailAvailabilityChanged, PreviewQuotaExceeded, DatabaseConflict.

## Futuro frontend React Native

Mostrare categoria e modalità email prima dell'upload. Visualizzare colonne/mapping e pagine di righe/errori; non trattare Valid come già importato. Richiedere disambiguazione e riconoscimento dei warning. Conservare EventId soltanto per il retry della medesima intenzione, con revision/digest ricevuti dal server. Dopo409 ricaricare metadati/righe o modificare mapping/candidato e ottenere nuova conferma; non riscrivere silenziosamente il body di una richiesta con EventId già usato. Dopo Completed mostrare gli ID e gli indirizzi attraverso le API email autorizzate; distinguere sempre riserva da mailbox/account. Non inviare inviti sensibili agli indirizzi soltanto riservati.

Frontend, upsert, provisioning/invio email, importazioni voti/esami/calendari, iscrizioni o trasferimenti massivi restano fuori perimetro. Stato verifiche effettive, provenienza dei restore host, test sandbox, bootstrap/migrazione e comandi Python pendenti sono nel report `tests/Integration/INSTITUTION_BULK_IMPORT_REPORT.md` e JSON associato.

Dipendenza: [pacchetto Open XML SDK3.5.1](https://www.nuget.org/packages/DocumentFormat.OpenXml/3.5.1), [licenza MIT](https://github.com/dotnet/Open-XML-SDK/blob/main/LICENSE). Restore host non equivale a build/test; queste evidenze sono registrate separatamente.
