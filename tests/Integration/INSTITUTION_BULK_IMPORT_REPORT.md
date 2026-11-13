Evidenza storica della fase nominata in questo documento. Per codice, contratti ritirati e test correnti vedere il [report conclusivo](../../docs/refactoring/RESPONSIBILITY_REFACTORING_REPORT.md) e la [regressione finale](../../docs/refactoring/validation/current-cutover/final/README.md). I conteggi e i componenti sotto non certificano il working tree finale.

# ElectronicRegister — Fase 3: importazione massiva istituzionale

**IMPLEMENTED AND VERIFIED**, con provenienza delle evidenze distinta: codice/build/test HTTP-SQL eseguiti dalla sandbox; restore NuGet e validatori Python eseguiti manualmente dall'utente sulla PowerShell Windows host. Nessun commit o push. Nessun dato produttivo modificato.

## Baseline e preservazione

HEAD iniziale e finale `d5bf774d57eb143252a3659a29e653e4a0ce19a9`, branch `refactor-database-entities`. Working tree inizialmente non pulita: Fase1 Staff e Fase2 Email erano locali. Stato iniziale e SHA256 di tutti i file iniziali sono conservati in `.staff-private/import-phase3-*/source-baseline.json` e `initial-git-status.txt`. Nessun reset, clean o checkout/restore distruttivo; nessuna modifica a UserRole legacy, ordini dei ruoli o workflow didattici preesistenti.

Baseline controllate: Fase1 231 unit +1137 integration; Fase2 317 unit +1176 integration =1493. Tutte le1493 identità precedenti sono presenti e superate, inclusi Staff, Email, login legacy/istituzionale/Microsoft, IAM/onboarding, iscrizioni, trasferimenti, esami, submission, grading, worker e multi-tenancy. Le sole aspettative numeriche preesistenti aggiornate nei test riguardano i nove endpoint aggiunti. Nessuna assertion precedente indebolita o test rinominato/ignorato.

I report reali Fase2 hanno nome singolare `INSTITUTION_EMAIL_POLICY_*`, diversamente dai percorsi plurali citati nel requisito. Sono stati aggiornati con le evidenze manuali effettivamente comunicate dall'utente:11 test Python,159 SHA256, validatori Staff/Email, exit0. Questi risultati sono attribuiti all'host, non alla sandbox, e non riutilizzati per certificare i nuovi artefatti.

Quindici file storici rigenerati dalla regressione sono stati ripristinati soltanto dopo confronto dei byte con gli SHA256 iniziali; gli output nuovi sono conservati privatamente. Tutti i file iniziali sono ancora presenti. I cambiamenti applicativi/documentali previsti e i lock file ripristinati dall'host sono distinti dai file generati.

## Funzionalità e architettura

Import sincrono CreateOnly, una categoria Student/Teacher per file: template CSV, upload CSV/XLSX, parsing limitato, mapping, validazione per riga, risoluzione classi, preview persistente e privata, remapping con revisione, conferma esplicita AllOrNothing e risultato durevole con identificativi creati. Nessun202 o job import fittizio. Un worker effettivo separato gestisce soltanto la retention dello staging.

- Domain: enum/cataloghi e DTO JSON rigorosi, stati/issue, limiti, contratti di parsing/regole, batch/risultati e unità di lavoro autorizzata.
- Business: mapping esplicito e alias esatti, regole pure per righe/duplicati/nomi/candidati email, riuso di IInstitutionEmailGenerator.
- Application: InstitutionImportManager orchestra autorizzazione, parsing, preparazione, riferimenti, preview/digest/revision, remapping, replay e conferma entro una sola unità di lavoro.
- Infrastructure: CSV UTF-8 rigoroso, adapter Open XML SDK con preflight ZIP/XML, repository/sessione SQL parametrizzata, mapping EF Database First, integrità e retention.
- API: nove route protette, multipart e DTO chiusi, limiti HTTP/concorrenza, errori stabili e OpenAPI.

Non vengono creati account, password, membership, profili collegati, mailbox, StudentEnrollment, CourseEdition, ClassSubject o trasferimenti. Non si aggiornano Student/Teacher già presenti; ClassId viene assegnato solo alla creazione tramite il repository istituzionale esistente. Il trasferimento rimane un workflow separato. Nessuna scrittura su voti, esami, submission o relativi storici.

## Formati, dipendenze e licenza

CSV UTF-8 con/senza BOM, `;`/`,`, virgolette/escape, separatori e newline quotati, righe vuote, accenti, nomi composti, colonne riordinate e intestazioni italiane/inglesi. Codifiche legacy non sono convertite con perdita silenziosa. Auto-detection ambigua richiede selezione esplicita.

XLSX è letto con **DocumentFormat.OpenXml3.5.1**, licenza **MIT**, e Framework3.5.1. Il pacchetto dispone di target.NET10; versione/licenza sono state verificate anche nel nuspec disponibile dopo il restore host. Nessuna libreria Excel commerciale o parser XLSX artigianale. I quattro lock file applicabili (Infrastructure, API, Integration, PlatformAdmin) e gli assets Infrastructure/API/Integration risolvono entrambe le dipendenze3.5.1.

Il primo restore sandbox era bloccato dall'accesso a `C:\Users\Lorenzo\AppData\Roaming\NuGet\NuGet.Config`, anche con configurazione esplicita. L'utente ha successivamente eseguito sull'host, entrambi con exit0:

```powershell
dotnet restore .staff-private/import-package-probe/probe.csproj --verbosity minimal
dotnet restore tests/Integration/Integration.Tests.csproj --verbosity minimal
```

Il primo prova soltanto il ripristino del probe; il secondo ha generato gli assets applicativi. Le build successive con `--no-restore` e i test sono stati eseguiti direttamente dalla sandbox. Nessun NuGet.Config preesistente, interprete, permesso o execution policy è stato modificato; nessuna restrizione aggirata.

## Sicurezza upload e privacy

Byte effettivi misurati, limite file default10MiB;2000 righe,16 colonne,512 caratteri/cella,8192/record. XLSX:128 parti,40MiB espansi effettivi,200000 nodi XML, profondità32, ratio1000 e complessità celle/stringhe limitata. DTD/resolver, formule cached/non cached, link esterni, macro e oggetti binari sono rifiutati. Celle numeriche/date/booleane/errori non vengono trasformate implicitamente in nomi/identificatori. XLSM/XLSB/XLS non supportati. MIME e filename non sono una prova di validità e non selezionano percorsi server.

Gli enum accettano soltanto stringhe note; mapping chiuso e indici validati. Nessun mass assignment di ruoli, attori, identificativi interni, sicurezza o verifica email. Prefissi formula nei valori mappati vengono rifiutati. I due export CSV sono template statici con dati fittizi, senza contenuto utente.

Il file originale e il filename non vengono persistiti. Staging JSON privato massimo2MiB, TTL30min default e quota20 payload per attore, inclusi scaduti non ancora ripuliti. Accesso esclusivamente per tenant/User/Membership creatori, anche per Admin. Alla conferma il payload è cancellato nella stessa transazione; alla scadenza diventa subito non leggibile/confermabile. Cleanup ogni minuto, passaggi limitati a20, con eventuale backlog; mantiene metadati, item, referenze ed audit. Il servizio continua a rilevare/pulire staging scaduti anche con ingestione disabilitata e non opera su DB privi dello schema.

Audit di servizio esplicito ActorKind=Retention con UserId/MembershipId null, senza impersonare l'autore. Azioni interattive conservano attori reali, tenant, operazione, revision/policy, hash, prima/dopo dei metadati tramite snapshot versionate, UTC, motivazione e ID creati; non l'intero file o i nomi/email delle righe. Log generici contengono soltanto tipi di errore, non celle o credenziali.

## Autorizzazione

| Operazione | Admin | Staff | Altri ruoli |
|---|---|---|---|
| Template/upload/preview/remap/confirm | Proprio tenant/attore | Proprio tenant/attore | Negata |
| Storico/righe/risultato | Solo propri batch | Solo propri batch | Negata |
| Student/Teacher CreateOnly | Consentita | Consentita | Negata |
| Riserva email pubblicata Student/Teacher | Consentita | Consentita | Negata |
| Ruoli/global User/policy email/override libero | Nessun comando nel modulo | Negata | Negata |
| DELETE di target, batch o audit | Nessun endpoint; history protetta | Negata | Negata |

Teacher/Student, token Selection e legacy, membership sospese/revocate, generation obsolete, altro tenant e altro attore sono coperti da prove negative. Nessun privilegio didattico per PlatformOperator. Staff non necessita di TeacherProfile e non viene convertito in Admin/Teacher. Rollout verificato dal manager, dal repository e prima del commit; prova di disabilitazione runtime effettuata.

## Duplicati, classi ed email

L'omonimia non è una chiave persona: righe identiche o possibili corrispondenze sono warning da riconoscere, senza merge. ExternalReference è opzionale, case-sensitive dopo trim e univoca per tenant/categoria con PK e FK reali. Riferimenti già presenti bloccano CreateOnly. Un file identico già completato richiede nuova preview, intenzione esplicita e motivazione, senza divieto assoluto basato sul digest.

Classi risolte in SQL nel tenant; nomi ambigui richiedono scelta di un ID autorizzato, con candidati limitati e nessuna scelta arbitraria. GUID del file non sostituibili con altri mediante disambiguazione. Fingerprint di ID/nome/percorso/edizione/revisione rivalidato al commit. Nessun enrollment o trasferimento implicito.

None crea solo anagrafiche. ReserveInstitutionalAddress usa policy/versione pubblicata e generatore Fase2; la preview non legge il namespace globale e non riserva nulla. In commit lo stesso servizio Fase2 partecipa alla transazione ambientale autorizzata, con UNIQUE globale comune ai writer User.Email, collision policy ed audit esistenti.

Nessun cambiamento silenzioso degli indirizzi: un candidato effettivo diverso produce409 e rollback totale. Per una nuova conferma si può scegliere esclusivamente un offset di candidato numerico ammesso dalla policy pubblicata, mediante nuova revisione; non un'email/dominio arbitrari. Reject non consente offset successivi. Il nuovo commit continua a verificare coincidenza con il primo candidato realmente assegnato da Fase2. Nessuna esposizione dei titolari di riserve cross-tenant. Indirizzi reserved, non verificati/provisionati; User.Email e login restano separati.

## Transazioni, idempotenza e concorrenza

Una transazione ReadCommitted copre tutte le persone, referenze, assignment/riserve, eventi email/import, item e Completed. Lock IAM-first, rivalidazione di membership/ruoli/generation mantenuta fino al commit, ordine righe stabile. Nessuna chiamata HTTP o transazione per riga; repository Student/Teacher e percorso email/create-person riutilizzati.

Replay controllato prima di creare record. EventId unico per tenant e digest dell'intento comprensivo di attore/batch/revision impediscono riuso diverso. Due conferme contemporanee generano un solo insieme di target; perdita della risposta dopo commit restituisce gli stessi ID al retry. Nuovo EventId su batch Completed non crea nuovo lavoro. Una riga invalida blocca tutto. Fault su seconda anagrafica, email o audit rollbacka anche scritture precedenti e stato finale, conservando soltanto la preview già committata.

Revoca concorrente aspetta il commit già autorizzato sotto lock; richieste successive con token obsoleto sono negate. Errori SQL/deadlock/timeout previsti producono conflitto generico senza dati di altri tenant. La conferma rivalida policy e riferimenti e non si fida di dati normalizzati ritrasmessi dal client.

## Schema, migrazione e bootstrap

Prima48 tabelle/117 trigger/145 FK/199 indici; dopo **52/129/158/218**. Quattro nuove tabelle: institution_import_batch, institution_import_item, institution_import_external_reference, institution_import_event. Dodici trigger,13 FK e19 indici nuovi. Chiavi composite tenant-aware, stati/immutabilità/audit enforceati con trigger MySQL5.7, non CHECK ignorati. Mapping EF sidecar Database First; FK Student/Teacher composite restano SQL-only come in MT3A, evitando alternate key sui tenant shadow legacy.

Tutte le48 definizioni e i117 trigger predecessori sono stati confrontati e preservati. Contratto predecessore versionato, bootstrap completo, schema contract, manifest, validator SQL/Python aggiornati. DDL implicitamente committed: nessuna promessa di rollback transazionale, reset o replay automatico.

Istanze tutte nuove, datadir GUID distinti, PID/porta/versione/UUID verificati:

- 23331: regressione preparatoria sullo schema Fase2.
- 23332: sviluppo/verifiche mirate della migrazione; draft iniziale seguito da aggiunta controllata FK policy, con backup separato. Non è questa la prova della singola applicazione del file definitivo.
- 23333: bootstrap definitivo da zero,52 SHOW CREATE TABLE esatti e129 corpi/metadata trigger corrispondenti al contratto;20 contatori SQL import/email a zero.
- 23334: riproduzione della **migrazione definitiva**, una sola applicazione del file finale dopo bootstrap predecessore byte-identico agli input iniziali Fase2, verifica del predecessore e dump SHA256; postflight52/129 esatto e10 contatori import a zero. È il target della regressione finale.

Dieci guard root-only di teardown preesistenti sono stati vincolati alla sola23334 dopo verifica esatta, prima della regressione; sorgenti canoniche e nuovi guard import non sono stati indeboliti. Nessuna porta3306/13306 o datadir preesistente riutilizzato. Dopo la regressione20 contatori SQL e tutte158 FK sono stati verificati per orfani, considerando correttamente le componenti nullable: zero violazioni. I quattro processi sono stati spenti regolarmente dopo nuovo confronto con PID/versione/porta/datadir/UUID registrato; dati e dump conservati.

Il runner controllato `Invoke-InstitutionImportMigration.ps1` è stato analizzato sintatticamente, non eseguito come script; le applicazioni native SQL sopra sono prove reali separate. I contratti intermedi versionati restano il riferimento per gli step precedenti, mentre il bootstrap corrente rappresenta lo schema finale. Deployment/definer produttivi richiedono revisione operativa dedicata; qui nessuna operazione produttiva è stata effettuata.

## Test ed evidenze

**352 unitari** Passed. Regressione completa **1261 integration** Passed, più **1 caso aggiuntivo Teacher/email** Passed dopo la suite, senza modifica applicativa: **1262 identità integration distinte**, totale **1614**, zero Failed/Skipped. Sono35 nuovi casi unitari e86 integration rispetto alla Fase2; i37 casi dei lettori CSV/XLSX sono test infrastrutturali senza SQL, mentre49 casi HTTP/SQL BulkImport usano Program reale e MySQL reale. Tutte1493 identità baseline preservate. I TRX/digest e le identità sono nel JSON.

Il caso aggiuntivo richiesto per Teacher con policy distinta è stato eseguito separatamente sull'identica implementazione già verificata, non presentato come un diverso contatore del singolo TRX completo. Fallimenti intermedi (cast GUID retention, aspettativa401 del Selection, wrapping DbUpdateException e ordine del vincitore concorrente) sono conservati privatamente e corretti senza indebolire test preesistenti.

| Requisiti test | Evidenze comportamentali |
|---|---|
|1–12 CSV|InstitutionImportCsvReaderTests e RulesTests: UTF-8/BOM, entrambi delimitatori, quoting/escape/newline, vuoti, accenti, ordine/alias|
|13–22 XLSX/limiti/ostilità|Workbook reali SDK; formule cached, numeri, link esterni, ZIP path/binary/DTD, budget XML/espansione, byte effettivi, conteggio righe/colonne/celle e mapping chiuso|
|23–36 preview|Digest invariato prima della conferma, zero assignment, errori campo/riga, classi mancanti/ambigue/cross-tenant, mapping, paginazione, expiry e attore/tenant privati|
|37–44 Student|CSV e XLSX Admin/Staff, classe iniziale/null, disambiguazione,100 righe, User/membership/enrollment invariati e nessun transfer/profile implicito|
|45–49 Teacher|CSV e XLSX Admin/Staff,100 righe, nessun ClassSubject/TeacherProfile/membership automatico|
|50–57 email|Policy Student Umbria e Teacher distinta, None, riserva reale, collisioni interne/globali, Reject/NumericSuffix e offset riconfermato|
|58–63 identità/sicurezza email|Cambio policy409, revoca Staff, User.Email/credenziali invariati, reserved, campi proibiti400 e nessun titolare cross-tenant divulgato|
|64–68 atomicità|Batch valido, riga invalida422, trigger di fault sulla seconda persona o audit e collisione seconda email: rollback completo|
|69–76 idempotenza/context|Replay ID identici, intento diverso409, doppia conferma stesso/diverso EventId, risposta persa, Completed non riusabile, SHA reale, mapping/digest obsoleto e classe modificata|
|77–89 auth|Admin/Staff positivi, Teacher/Student403, niente DELETE/ruoli/email arbitrari, altra Fondazione/attore404, legacy/Selection401, generation/sospensione/revoca403 e lock concorrente|
|90–100 regressione|Intera suite precedente conservata: Staff, Email, login, iscrizioni, transfer, esami/submission/grading, audit e multi-tenancy; nessun Failed/Skipped|

## Manifest e Python

**166 SHA256**; manifest finale `1D03F79615233122A4C56725E8CF9B894566B02330DB67AB2EE47018E78F072F`. Verifica indipendente PowerShell e chiusura versionabile. L'utente ha eseguito realmente sull'host i cinque comandi sotto, riportando11 test OK e le quattro stampe finali di successo166 SHA256/contratti; nessun SQL è stato eseguito dai validatori offline. I codici d'uscita non sono stati riecheggiati separatamente nel blocco finale, mentre gli output di successo di ogni script sono presenti. Nessun artefatto coperto dal manifest è stato modificato dopo quell'evidenza; ricontrollati i digest nella sandbox.

```powershell
python -m unittest discover -s tools -p 'test_*.py'
python tools/verify-project-artifacts.py
python tools/verify-institution-staff.py
python tools/verify-institution-email.py
python tools/verify-institution-import.py
```

La sandbox non ha eseguito Python: invocazione assoluta CommandNotFoundException e ispezione path UnauthorizedAccessException/PermissionDenied (`ItemExistsUnauthorizedAccessError`). Questi errori ambientali sono conservati, non attribuiti al progetto o aggirati. Le verifiche manuali host nuove sostituiscono il blocco per questi esatti input; non sono dichiarate esecuzioni sandbox. Nessun comando host necessario rimane pendente per la certificazione corrente.

## Limiti e funzionalità rinviate

Import sincrono limitato, CreateOnly, privacy actor-scoped; nessun upsert o collaborazione automatica. XLSX testo-only per i campi, niente macro/formule/oggetti binari; selezione esplicita con più fogli. Template download soltanto CSV (XLSX consigliato ma facoltativo). Disponibilità email garantita solo dal commit, con nuova conferma se cambia; candidati numerici vincolati alla policy. Retention fisica dipende dal passaggio/backlog, con accesso TTL immediato e quote. Nessuno di questi limiti è presentato come funzionalità ulteriore implementata.

Fuori perimetro e non implementati: frontend React Native, account/provisioning/invii benvenuto, Microsoft365/GoogleWorkspace, import voti/esami/calendari, unità/moduli/monte ore/tirocinio, trasferimenti o upsert massivi. Nessuna Fase4 avviata. Contratto completo e istruzioni frontend in `docs/api/INSTITUTION_BULK_IMPORT.md`. Nessun blocco funzionale residuo per la Fase3 nei limiti dichiarati.
