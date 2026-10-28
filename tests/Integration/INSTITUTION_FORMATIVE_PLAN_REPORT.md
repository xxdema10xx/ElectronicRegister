# Fase 4 — piani formativi ITS versionati

Implementazione persistente completata e verificata con esecuzioni dirette .NET,
HTTP/Kestrel, MySQL 5.7.24 e Python. Nessuna evidenza manuale host è attribuita alla
sandbox. Nessun commit/push, database produttivo, porta 3306 o istanza preesistente
sulla 13306 modificati. Dati e log di test rimangono nelle directory private.

## Baseline, continuazione e preservazione

HEAD iniziale e finale `6133634c32fe013df4f8df8b79e17cd53701b030`, branch
`refactor-database-entities`. La prima parte aveva introdotto regole pure e 47 casi
nuovi, con 399 unitari superati. Questo incremento li conserva e completa i layer
mancanti. Copie del report/JSON/manifest parziali sono in `.formative-private/partial-domain-*`.

La working tree non era pulita: 14 JSON storici erano già modificati sotto TestResults;
alla continuazione si aggiungevano i file della consegna parziale. Tutti i 14 JSON sono
stati preservati byte per byte. Un ulteriore file storico (`mt3d/write-statements.sql.txt`)
rigenerato dai test è stato conservato privatamente e riportato al contenuto HEAD;
nessun file di test preesistente è stato modificato, disabilitato o rinominato.
Nessun reset, clean, checkout/restore distruttivo o ricostruzione della storia legacy.

I report Staff/Email/BulkImport documentano la baseline Fase3: 352 unitari e 1262
integration distinti (1261 nella suite più uno separato), 1614 complessivi. Le 399
identità unitarie della prima parte sono confrontate direttamente con il nuovo TRX;
sono tutte presenti e Passed. Tutte le sorgenti dei test preesistenti sono invariate:
il nuovo TRX completo contiene 352 unitari legacy e 1262 integration legacy Passed.
Confrontate inoltre le 35/86 identità aggiunte esplicitamente nei JSON Fase3: nessuna manca.
Il JSON finale conserva gli inventari delle identità e i digest reali dei TRX.

## Implementazione e decisioni

Schema additivo relazionale a otto tabelle: formative_plan, formative_plan_version,
formative_unit, formative_module, formative_module_competency, formative_module_content,
course_edition_formative_plan, formative_plan_event. Nessun piano JSON opaco.
Database First con entità/scalar mapping in sidecar; nessuna modifica ai generatori
owner shadow, alle FK, agli indici o ai trigger dei modelli legacy.

Piano tenant-scoped appartenente a uno StudyPath dello stesso tenant; codice unico
nella Fondazione e immutabile, denominazione amministrativa modificabile con audit.
Versioni numerate sotto lock, Draft modificabili e Published immutabili. Nessuna
archiviazione aggiunta. Clone solo da Published, atomico, con nuovi identificativi
per ogni elemento versionato e Subject condivisi, senza riscrittura della sorgente.

UF/moduli/competenze/contenuti si gestiscono con una PUT della **struttura Draft
completa**: aggiunta, modifica, rimozione, riordino e budget in un solo comando auditato.
ID null crea; ID esistente deve appartenere al parent originale. Il cambio di parent
richiede rimozione/ricreazione con nuovi ID. Nessun DELETE Published o binding generico.
Ordini espliciti unici tra fratelli; nomi ripetibili; codici UF unici per versione e
codici modulo unici per UF. SQL utf8mb4_unicode_ci resta l'autorità per le collisioni.

Minuti interi, somme Int64, totale UF ricavato dai moduli e totale piano dalle due
componenti. Draft incompleta salvabile; summary espone assegnati/mancanti/eccedenti.
Pubblicazione richiede somme esatte, struttura completa, almeno una competenza e un
contenuto per modulo, Subject valido. Errori di pubblicazione includono hourSummary.
72000/48000 sono fixture, non vincoli normativi. Tirocinio soltanto budget configurato.

CourseEdition riceve un binding iniziale a Published della stessa Institution e dello
stesso StudyPath; UNIQUE e trigger vietano sostituzione/unlink/cancellazione. Riutilizzo
dello snapshot su più edizioni consentito; nuova versione non sposta vecchi binding.
Legacy senza piano resta valida, senza backfill. Il preflight del vecchio backfill
riconosce le nuove tabelle tramite controlli read-only: non le aggiunge alle tabelle
da backfillare e non genera piani o storia didattica.

Subject non è duplicato per versione; moduli con lo stesso Subject mantengono identità,
ore e competenze distinte. ClassSubject e la sua UNIQUE restano invariati. Binding e
publish non scrivono Class, StudentEnrollment, Student.ClassId, esami, voti o submission.

## Layer, API e sicurezza

Nuovi contratti Domain in InstitutionFormativePlan.cs; Business FormativeDraftBuilder;
Application InstitutionFormativePlanManager; Infrastructure repository SQL parametrizzato,
entità/mapping e FormativePlanIntegrity; Api controller, filtro errori di pubblicazione
e provider di rollout. Registrazioni DI nei tre layer esistenti, nessuna dipendenza nuova.

Sedici action: lista/creazione/dettaglio/rename piano, lista/creazione versioni,
dettaglio/sostituzione Draft, clone, validate, hour-summary, publish, binding iniziale,
lettura piano dell'edizione, lettura modulo sotto il binding e history amministrativa.
Contratti chiusi anche annidati; body massimo 2 MiB; pagine massimo 100; UTC e GUID.
ExpectedRevision obbligatoria per update/publish/clone. Creazioni serializzate e
binding con precondizione persistente di assenza. EventId identifica un'intenzione.

| Operazione | Admin | Staff | Teacher | Student |
|---|---|---|---|---|
| Catalogo, versioni, budget | sì | lettura | no | no |
| Modifiche Draft, publish, clone, binding | sì | no | no | no |
| Piano/modulo sotto edizione | sì | sì | assegnazione docente reale | propria iscrizione |
| Audit e attori | sì | no | no | no |

InstitutionAccess, membership/Institution attive, ruoli e SecurityGeneration rivalidati
in API/manager/repository. Teacher e Student risolti da profili della membership;
Staff+Teacher non è Admin; UserRole.Admin legacy e PlatformOperator non concedono
privilegi. Fuori tenant e assente producono la stessa assenza, senza dettagli SQL.
La vista personale non enumera catalogo, persone o audit.

FormativePlans/SchoolApi/CourseApi sono chiusi per default. Controller assente quando
FormativePlans è chiuso all'avvio; attivazione con restart. Chiusura runtime rivalidata
anche prima del commit. L'inventario storico delle route resta invariato a rollout
chiuso, senza modificare le aspettative dei test precedenti.

## Audit, concorrenza e rollback

Transazione ReadCommitted con mutex persistente Institution e ordine IAM-first;
lock membership/ruoli/generation mantenuti fino al commit. Hash dell'intenzione
comprende attore, tenant, operazione, risorsa e payload. Replay sotto lock prima di
scrivere; ricevuta originale anche dopo revisioni successive. Payload/attore diversi
con lo stesso EventId producono conflitto. EventId UUID globale, lookup sempre scoped.

Audit append-only atomico con risorsa, attori reali, revisioni, UTC, motivo, hash,
BeforeJson e AfterJson: quest'ultimo contiene Receipt e State. Trigger controlla
coerenza dei riferimenti e attore Admin attivo; UPDATE/DELETE audit vietati anche SQL.
Revisioni monotone nella rispettiva risorsa; numerazione versioni unica per piano.

Trigger figli prendono FOR UPDATE sulla versione: non possono passare una pubblicazione
concorrente. Parent/owner immutabili, FK composite e restrizioni SQL proteggono struttura,
Subject e binding. Letture gerarchiche in snapshot RepeatableRead. Non si dipende da
CHECK ignorati o lock in memoria. Fault injection prova rollback dopo modulo, struttura,
audit e prima del commit; clone/publish/binding sono AllOrNothing.

## MySQL, migrazione e bootstrap

Predecessore: 52 tabelle /129 trigger /158 FK /218 indici. Finale: **60/153/171/257**,
incremento **8/24/13/39**. Migrazione 001_expand.sql, dieci controlli 002_validate.sql,
predecessor-contract, bootstrap canonico e schema contract aggiornati.

Runner nativo `tools/formative_test_db.py`: solo nuovi datadir GUID sotto .staff-private,
porte nuove, binari MAMP MySQL 5.7.24. Verifica PID/path del binario, UUID, versione,
porta/datadir, schema predecessore completo e assenza di altri client; backup SHA256
prima del DDL, confronto delle vecchie definizioni dopo, nessun retry/reset/DROP.
La policy PowerShell impediva il primo script: non è stata modificata o aggirata;
il runner Python usa direttamente i binari MySQL autorizzati.

| Prova | Porta | Directory privata |
|---|---:|---|
| Prima migrazione esplorativa | 23441 | formative-79faca2337a849239bd888b62fde6630 |
| Primo bootstrap, tentativo con utente errato | 23442 | formative-020e772713934fefa33598008400d311 |
| Migrazione con preflight completo | 23443 | formative-6b55e98dc0784c77a3145d1868185bab |
| Regressione completa finale | 23444 | formative-90692bbe705a4ca5a742c00185348f28 |
| Migrazione finale e suite Fase4 | 23445 | formative-59ef02a0d3674887953df57c92beb9ab |
| Bootstrap finale e suite Fase4 | 23446 | formative-79b108c292f44f14a4e0cd0cdde8a67d |

Le ultime due hanno schema uguale dopo normalizzazione esclusiva dei guard delle
fixture per porta/datadir esatti. Nessuna definizione legacy è stata cambiata nel
bootstrap sorgente: la parametrizzazione è fatta prima del CREATE delle nuove istanze.
Dieci controlli a zero anomalie su ciascuna delle due istanze, ripetuti dopo le prove HTTP.

L'ultima stretta su due guard Fase4 (codice case-only e ENUM zero con SQL mode non
strict) è verificata con due probe SQL nativi su entrambe le istanze finali. La suite
HTTP Fase4 è rieseguita su entrambe; la regressione legacy completa sulla 23444 usa
lo stesso codice applicativo e definizioni legacy identiche, antecedenti alla sola
stretta dei due guard nuovi. I due probe non si sommano due volte alle identità .NET.

DDL non transazionale: recovery documentata nell'architettura; fermarsi su errore,
conservare inventario/log/backup, ripristinare esclusivamente in un'altra nuova istanza
prima di decidere come proseguire. Nessun deployment produttivo è stato effettuato.

## Test effettivi e tentativi precedenti

- **411 unitari Passed**, inclusi i 399 precedenti: 352 baseline Fase3 +47 prima parte
  Fase4 +12 builder. Zero Failed/Skipped.
- **1348 integration Passed**, 1262 baseline +86 Fase4. Zero Failed/Skipped.
- **1759 identità .NET distinte**. I due run aggiuntivi da 86 non aumentano il totale.
- **14 test Python Passed**: 11 precedenti e 3 nuovi controlli di sicurezza del runner.
- Due probe SQL distinti, ripetuti sulle due istanze finali, tutti Passed.

Copertura Fase4 effettiva: persistenza e replay; codice tenant-scoped; budget esatti e
summary; clone profondo e nuovi ID; revisioni e numerazione concorrenti; due Admin;
pubblicazioni/binding concorrenti; ID stabili sotto parent originale; Subject condivisi;
rimozione/ordine/competenze/contenuti persistenti; binding per versioni/edizioni differenti;
viste personali con iscrizione/assegnazione reale; Staff/ruoli combinati; Selection,
generation e membership/Institution inattive; ruolo legacy e claim piattaforma;
immutabilità SQL di header, figli, binding e audit; FK Subject; owner/parent;
fault/rollback; revoca e chiusura rollout durante scrittura; backfill read-only.

Regressione preservata: Staff, email, import CSV/XLSX, login/IAM, iscrizioni, trasferimenti,
ClassSubject, Exam, Grade, Submission, worker e CLI amministrativa reale. La CLI è stata
compilata prima della regressione finale, senza cambiare i suoi test `--no-build`.

Tentativi conservati e non certificati come verdi: primo run mirato con valore fixture
`revoked` non appartenente all'ENUM (corretto a `left`); regressione con er_test fermata
per il teardown root-only; run root con sei errori (classificazione schema mancante,
percorso mysqldump non configurato e CLI non compilata). Corretti codice/invocazione,
mai indeboliti i test precedenti. Il run finale completo è interamente Passed.

## Artefatti, validatori e limiti

Manifest finale e SHA256, elenco completo dei file cambiati, TRX, identità, backup,
UUID/PID/porta/datadir e risultati sono nel JSON di verifica. Manifest di chiusura
sorgenti separato dai report per evitare autoreferenze. I 166 hash della baseline
non sono usati come certificazione dei nuovi file: tutti i digest sono ricalcolati.

**205 artefatti SHA256**, manifest finale
`790D26EB4FE34557DC82F95EE5F8EE44352B253BFD97EAE94E064BACC764EEBD`.
I sei soli processi MySQL creati per queste prove sono stati fermati dopo ulteriore
verifica di identità e assenza di client; datadir, backup, credenziali private e risultati
sono conservati. L'istanza MAMP preesistente non è stata interrogata o fermata.

Eseguiti direttamente: verify-project-artifacts, verify-institution-staff,
verify-institution-email, verify-institution-import e verify-institution-formative-plan,
oltre ai 14 test Python. Il validatore Fase4 controlla contratti/digest e i TRX locali
disponibili, senza SQL/HTTP e senza dedurre copertura dai soli nomi. Python effettivo
3.13.14; il percorso 3.14.2 indicato inizialmente non esiste su questo host. Nessuna
reinstallazione o modifica di execution policy; nessuna prova host da richiedere per
le verifiche già eseguite con l'interprete disponibile.

Warning NU1900 del feed advisory NuGet non raggiungibile: non è dichiarato un nuovo
audit vulnerabilità. Non sono introdotti pacchetti. Mutex tenant conservativo: eventuale
ottimizzazione del parallelismo richiederà prove dedicate. Nessun workflow di rettifica
binding o archiviazione incompleto; nessuna modifica grading o gestione tirocini/calendario.

Documentazione completa in ITS_FORMATIVE_PLAN_VERSIONING.md e INSTITUTION_FORMATIVE_PLANS.md,
inclusi istruzioni React Native e lookup modulo per il futuro calendario. Nessuna decisione
architetturale esterna pendente per il perimetro consegnato. Nessun comando host manuale
necessario per ottenere le evidenze qui riportate.
