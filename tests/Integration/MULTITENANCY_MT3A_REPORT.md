Evidenza storica della fase nominata in questo documento. Per codice, contratti ritirati e test correnti vedere il [report conclusivo](../../docs/refactoring/RESPONSIBILITY_REFACTORING_REPORT.md) e la [regressione finale](../../docs/refactoring/validation/current-cutover/final/README.md). I conteggi e i componenti sotto non certificano il working tree finale.

# ElectronicRegister — MT-3A: ownership e integrita dello schema

**Checkpoint A completato sul database di test. MT-3 nel suo insieme resta PARZIALE: B/C/D/E non completati, gate scolastico chiuso, nessun secondo tenant operativo e nessun cutover.**

Questa consegna riguarda integrita strutturale e attribuzione dei dati. Non dichiara isolamento HTTP dei repository legacy. MySQL effettivo: **5.7.24**, database esclusivo `electronic_register_test`, SQL mode strict. Nessun ambiente produttivo modificato.

## 1. Stato Git iniziale

Branch locale `refactor-database-entities`, HEAD `970d5d704283daf0c33e106b1ff17149360cfcf4`. Il workspace conteneva gia modifiche audit/MT1/MT2/MT3 non committate, entity, script, test e documentazione non tracciati. Conservati senza reset, checkout, pull, cancellazioni o commit. Stato iniziale in [initial-git-status.txt](TestResults/mt3a/initial-git-status.txt). Il remoto non e stato usato come baseline.

Letti i report MT2/MT3 e i tre documenti architetturali richiesti; verificate entity/context/sidecar, SQL effettivo di information_schema, repository/raw SQL, percorsi audit e gate. Non modificati ExamManager, manager scolastici, worker, controller o autenticazione in questa fase; i loro diff gia presenti appartengono al lavoro precedente.

## 2. Baseline

Build iniziale riuscita; **48 unit + 300 integration = 348/348 Passed**, zero Failed e zero Skipped, MySQL reale. Due TRX distinti per progetto in [baseline](TestResults/mt3a/baseline/). La baseline e stata eseguita prima dell'EXPAND.

## 3. Inventario ownership

Inventario completo di **28 tabelle (27 preesistenti + mt3a_legacy_scope)** in [MULTITENANCY_MT3A_OWNERSHIP_INVENTORY.md](../../docs/architecture/MULTITENANCY_MT3A_OWNERSHIP_INVENTORY.md). Per ciascuna: categoria A/B/C/D/E, PK, tutte le UNIQUE, tutte le FK con colonne e delete/update rule, percorso, rischio cross-tenant, garanzia SQL e controllo applicativo necessario. Include `submission_answer_option`, ponte dictionary senza DbSet esplicito, e le due tabelle profilo gia presenti.

Le evidenze [schema-columns.tsv](TestResults/mt3a/schema-columns.tsv), [schema-indexes.tsv](TestResults/mt3a/schema-indexes.tsv), [schema-fks.tsv](TestResults/mt3a/schema-fks.tsv), [schema-triggers.tsv](TestResults/mt3a/schema-triggers.tsv) sono metadati, senza dati delle celle o credenziali. Gli indici nuovi e quelli di catalogo con scope modificato sono analizzati **per indice** nell'inventario: predicato, ordine, FK/UNIQUE, costo di scrittura e ridondanze eliminate.

## 4. Tabelle globali e tenant-owned

`User` rimane A GLOBAL_IDENTITY: un solo account/credenziale puo avere membership e profili distinti in A/B. `Institution` e il registro del tenant (D control plane), non un amministratore globale. Campus/Membership/link profilo hanno ownership diretta; i ruoli MT1 sono C, derivati dal singolo parent Membership.

Le **19 tabelle scolastiche** hanno `institution_id NOT NULL`, FK Institution RESTRICT e ownership immutabile. Anche le dipendenze materializzano il tenant: i ponti multi-parent necessitano di una chiave comune per le FK; le dipendenze a un parent usano lo stesso modello esplicito, a costo di una colonna/indice aggiuntivi. Nessun catalogo scolastico modificabile e condiviso implicitamente.

Audit e classificato per riga: B per eventi didattici riconducibili, D per soli metadati `platform` espliciti. E indica i casi polimorfici da decidere, che il preflight rifiuta anziche attribuire arbitrariamente. Non sono rimaste ambiguita nel dataset sottoposto a backfill.

## 5. Schema SQL modificato

SQL canonico in `migrations/mt3/`, incorporato tramite la resource wildcard Infrastructure gia esistente:

| Script | Effetto |
| --- | --- |
| 003_ownership_expand.sql | 20 InstitutionId nullable (19 scolastiche + Audit); tre scope nullable SET NULL; ExamId tecnico Answer; QuestionId tecnico ponte opzioni |
| 004_ownership_preflight.sql | Owner esistenti, tutti gli archi, profili, stesso Exam/domanda, classificazione audit; qualunque violazione interrompe |
| 005_ownership_backfill.sql | DML transazionale e idempotente con Guid legacy esplicito; nessuna identita/membership inventata |
| 006_ownership_enforce.sql | NOT NULL scolastici, FK Institution, chiavi e FK composite, UNIQUE cataloghi scoped |
| 007_ownership_triggers.sql | Derivazione ownership e chiavi tecniche, immutabilita, manifest SQL legacy, attribuzione audit |
| 008_owned_profile_backfill.sql | Successore scoped della 002: collega solo profili del manifest, senza spostare quelli A/B |
| 009_explicit_platform_audit.sql | Sostituisce solo i due trigger audit, distinguendo metadati platform da didattica |
| 010_prune_redundant_owner_indexes.sql | Rimuove tre UNIQUE transitorie non referenziate; sostituti documentati |
| 011_prune_covered_fk_indexes.sql | Rimuove tre indici FK coperti dai prefissi delle UNIQUE triple |

Le SQL MT1 e le 001/002 MT3 sono conservate. Le FK legacy restano presenti: le nuove composite sono garanzie SQL aggiuntive; nessuna duplicazione delle stesse relazioni nel mapping EF. Nessun CHECK critico: MySQL 5.7 e SQL mode strict effettivi. Guid CHAR(36)/ascii_general_ci coerenti fra chiavi e riferimenti.

## 6. Nuove FK composite

**27 nuove FK composite**: 25 sul grafo didattico e due dai link profilo al profilo dello stesso istituto. Sono verificate in information_schema per nome, ordine colonne, parent, colonne referenziate e regole DELETE/UPDATE, con indici effettivi e test negativi reali.

Sono chiusi i confini Biennium/Area/Path/Class, ClassSubject/Class/Subject/Teacher, Exam/ClassSubject/Retake, assegnazione/Submission/Grade/Student, Question/Exam, Option/Question, Answer/Submission/Question, ponte Answer/Option e Violation/Submission. Tutti i parent Institution usano RESTRICT.

Per Student.Class, Exam.Retake e Submission.Grade, una FK `(institution_id,reference_id) ON DELETE SET NULL` annullerebbe anche il proprietario. Si usano invece `class_institution_id`, `retake_institution_id`, `grade_institution_id` nullable: i trigger le impostano all'owner quando il riferimento esiste; le FK composite possono annullare soltanto scope e riferimento. I test verificano **tutte e tre** le cancellazioni storiche e il mantenimento dell'owner e dei dati B. Nessun cambio alle delete rule originali.

## 7. Cataloghi per fondazione

Le UNIQUE `uq_subject_name`, `uq_study_area_name`, `uq_study_path_name` diventano `(institution_id,name)`; `uq_biennium` diventa `(institution_id,start_year,end_year)`. Nomi e coppie date identici sono persistiti in A/B con Guid distinti. Class/ponte mantengono le unicita funzionali originali, gia ancorate a parent con identita univoca. Il significato di Biennium resta invariato; CourseEdition non introdotta.

## 8. Student/Teacher ownership

Owner diretto nella tabella scolastica, indipendente da User. I profili senza account appartengono al manifest legacy esplicito; nessun account o membership fittizi. I link istituzionali gia presenti non sono duplicati: UNIQUE(profile_id) e FK membership/istituto originali sono integrate da FK `(institution_id,profile_id)` verso il profilo.

Le fixture MT3 Profiles sono state adattate a creare Institution prima dei profili e a valorizzare il proprietario. I **21 casi originali rimangono tutti**. Il caso di backfill usa ora un profilo effettivamente posseduto dalla holding, e verifica anche che il profilo A non venga spostato. Il conflitto viene rifiutato dal preflight (`InvalidOperationException`) prima della precedente collisione SQL: assert negativa rafforzata, non eliminata. Il teardown elimina prima i soli profili fixture e poi i loro istituti, secondo le nuove FK.

## 9. Exam/Grade/Submission ownership

Ogni riga ha owner verificabile, senza cambiare Guid, struttura, tentativi, recuperi, punteggi, GradeId, lock o workflow. L'Answer riceve un `exam_id` tecnico dalla Submission; le FK `(institution_id,exam_id,submission_id)` e `(institution_id,exam_id,question_id)` impediscono domande di un altro esame anche nello stesso tenant.

Il ponte opzioni riceve `question_id` dall'Answer; FK `(institution_id,question_id,answer_id/option_id)` garantiscono la domanda esatta. I trigger ricalcolano le chiavi tecniche anche su UPDATE: NULL o valori forniti dal writer non aggirano le FK.

Dentro lo stesso istituto il DB non garantisce iscrizione/classe ammessa, ruolo docente corrente, corrispondenza studente/materia del Grade ufficiale rispetto all'esame, cardinalita Single/Multiple/Open, lifecycle e calcolo. Sono responsabilita applicative esplicite; non vengono presentate come nuove garanzie SQL.

## 10. Audit ownership

Backfill dei tipi effettivi `exam` e `exam_submission` attraverso EntityId. Nessun owner dedotto dall'attore globale, dal primo istituto o da JSON arbitrario. Preflight sconosciuto/orfano senza owner -> interruzione, nessun dato cancellato o classificato globalmente per convenienza.

Gli append legacy dei tipi noti ereditano il tenant dall'entita. L'owner storico, EntityType ed EntityId sono immutabili; cancellare in seguito la risorsa non annulla l'audit attribuito. BeforeJson, AfterJson, ActorUserId, RequestId e date sono conservati. La FK dell'attore mantiene SET NULL.

`entity_type='platform'` richiede `action LIKE 'platform.%'`, InstitutionId/EntityId/BeforeJson/AfterJson NULL: solo metadati espliciti, nessun payload didattico. Un evento sconosciuto non viene convertito in platform. Nessuna API di lettura audit o privilegio globale e stato aggiunto. Nuove categorie audit richiederanno una mappatura versionata ed esaminata prima dell'uso.

## 11. Backfill

L'utente ha autorizzato esplicitamente una holding tecnica di test. Guid del manifest **`4d543341-0000-4000-8000-000000000001`**, codice `MT3A_LEGACY_TEST`, nome dichiaratamente tecnico, stato **provisioning**. Non rappresenta una fondazione legalmente verificata e non e attivata. Nessuna membership permanente creata per completare l'ownership.

Il preflight confronta gli owner esistenti/proposti per ogni arco e link profilo. Righe non ancora attribuite vengono assegnate al manifest soltanto se l'intero grafo e coerente; un link gia riferito a un'altra fondazione interromperebbe il backfill. Gli owner gia validi A/B rimangono tali nei rerun. DML atomica, snapshot prima/dopo, nessuna scrittura legacy autorizzata in finestra di migrazione. La transazione non sostituisce la necessaria quiescenza di app/worker.

La prova di ambiguita usa una tabella **temporanea di sessione** con colonne audit legacy e un evento non attribuibile, sul vero MySQL, senza trigger/FK per rappresentare lo stato pre-enforcement. Backup prima della CREATE TEMPORARY, nessun DROP o FOREIGN_KEY_CHECKS=0. Il vero preflight/BackfillConnectionAsync rifiuta l'evento; una nuova connessione conferma ogni cella reale invariata. Non e un mock del repository e non pretende che il DB gia vincolato ammetta dati corrotti.

## 12. Dati legacy preservati

[legacy-preservation.json](TestResults/mt3a/legacy-preservation.json) confronta le **21 tabelle legacy** prima dell'EXPAND e dopo il backfill: identici nomi/ordine delle colonne legacy, conteggi e SHA-256 di righe ordinate contenenti ogni cella. Include PK/FK, password hash, classi, cataloghi, esami, retake, risposte/opzioni, Grade, associazioni Submission/Grade e audit JSON. Nessun valore sensibile e scritto nei report.

Le colonne nuove sono intenzionalmente escluse dal digest legacy. Le UPDATE di owner/scope di Exam/Submission assegnano anche `updated_at=updated_at` per neutralizzare l'ON UPDATE automatico MySQL. Il tooling confronta inoltre i digest prima/dopo ENFORCE e sostituzione trigger audit. I test ripetono due volte il backfill gia vincolato e verificano tutti i digest da connessioni nuove, inclusa la conservazione delle righe B.

Le suite successive modificano legittimamente il seed: gli snapshot di preservazione si riferiscono alla **finestra della migrazione**, non a una falsa uguaglianza dell'intero DB dopo l'esecuzione dei test.

Backup schema+dati privati e ignorati in `mt3a-evidence/private`. Prima di ogni DDL nuovo: connection string test/loopback, SELECT DATABASE(), VERSION(), SQL mode, nuovo mysqldump leggibile con marker di completamento, SHA-256. Evidenza senza dati in [backup-verification.json](TestResults/mt3a/backup-verification.json). Nessun dump con hash password in Git. Restore completo non provato e nessuna promessa di rollback atomico: MySQL esegue commit impliciti.

## 13. Scaffold EF

Database-first mantenuto: nessuna entity o context legacy generati modificati manualmente. Lo scaffold completo viene prodotto in cartella candidata separata da `Scaffold-Mt3a.ps1 --no-onconfiguring`, verificando proprieta ownership di tutte le entity (incluso il nuovo candidato ponte esplicito) e relazioni composite.

Il sidecar `ElectronicRegisterContext.Mt3aOwnership.cs` conserva le entity/navigazioni originali: InstitutionId e una **shadow property EF** (colonna diretta SQL), incluso il join dictionary; Audit owner nullable; quattro indici catalogo riconfigurati senza mantenere l'indice globale nel modello. Le FK composite e le colonne tecniche sono enforcement SQL, non una seconda copia delle relazioni EF. I trigger/default gestiscono le scritture dei manager legacy durante questa fase. Nessun Global Query Filter come falsa autorizzazione.

Scaffold MT1/MT3 parziali ora rifiutano Apply quando il sidecar MT3A e presente, prima di copiare qualunque file. Provati entrambi i rifiuti e verificati identici gli hash di tutti i sorgenti Persistence: [scaffold-guards.json](TestResults/mt3a/scaffold-guards.json). Rigenerazione futura: candidato completo, riconciliazione esplicita delle shadow property e della rappresentazione many-to-many, conservazione di un solo OnModelCreatingPartial e di tutti i suoi hook. Nessun overwrite automatico di parent o sidecar; nessun pacchetto NuGet aggiunto.

## 14. Test negativi cross-tenant

**39 nuovi casi integration MySQL**, senza mock di integrita. Comprendono 23 archi cross-tenant con 1452 effettivo dopo rimozione di UNIQUE/FK in ingresso che avrebbero mascherato il vincolo; persistenza controllata da connessioni nuove; due cataloghi identici; profili non trasferibili/riusabili; User globale con membership A/B; domanda di un altro Exam e opzione di altra domanda nello stesso tenant; tre cancellazioni SET NULL; Institution RESTRICT; owner inesistente; profilo legacy senza User; audit attribuito/errato/non classificato/platform; backfill idempotente e ambiguo; verifica esatta di ogni nuova FK e indice rimasto; rifiuto DB non autorizzati prima della rete.

Gli errori di preparazione dei primi run mirati sono conservati localmente: campi obbligatori raw SQL inizialmente omessi e vincoli preesistenti che mascheravano la FK in prova. Corrette le fixture per isolare la garanzia, senza accettare genericamente un qualsiasi errore DB. Le suite definitive non contengono errori o skip.

## 15. Vincoli applicativi ancora necessari

- Filtri SQL `InstitutionId` su ogni lettura, lookup per Guid, aggregato, raw SQL/lock, export e background item; un Guid valido non autorizza.
- Membership/Institution Active e ruoli correnti del caller; profilo corretto per la membership; nessun admin globale per leggere didattica A/B.
- Reviewer User globale autorizzato nella fondazione e nella classe al momento dell'azione; l'FK User da sola non fornisce questo diritto.
- Iscrizione/assegnazione studente alla classe/esame, Grade ufficiale con stesso studente e ClassSubject dell'esame entro tenant; retake semantici/cicli e workflow.
- Regole domanda Single/Multiple/Open, cardinalita opzioni, formule grading e transazioni/lock esistenti.
- Nuovi tipi audit, semantica dei JSON, accesso alle letture audit e processi tecnici per tenant/item; nessuna autorizzazione scolastica ricavata da un evento platform.

Sono limiti dichiarati delle garanzie SQL, da affrontare nei checkpoint successivi; non giustificano apertura del gate.

## 16. Compatibilita legacy

`mt3a_legacy_scope` e il manifest DB esplicito per le sole radici legacy prive di owner/parent; le dipendenze ereditano il parent. Il Guid vuoto DEFAULT e solo un sentinel di compatibilita, sostituito dal BEFORE INSERT; trigger e FK impediscono che rimanga memorizzato. Non viene scelto il primo Institution e non si usa TenantContext HTTP. Nuove scritture istituzionali dovranno fornire l'owner esplicito dal contesto verificato.

Il manifest tecnico DB non cambia opzioni auth/allowlist: in un futuro deployment verificato bisognera allinearlo all'InstitutionId legacy approvato, senza abilitare account nuovi. `User.Role`, `User.StudentId`, `User.TeacherId`, ClaimsContext, route e LegacyEndpointGate sono conservati. Le nuove tabelle non sono esposte da API. Le cinque API MT2 restano le sole nuove route istituzionali; ticket/access hanno scopi diversi, revoca/stati verificati.

## 17. Due suite complete definitive

| Run | Unit | Integration | Totale | Failed | Skipped/NotExecuted |
| --- | ---: | ---: | ---: | ---: | ---: |
| Baseline | 48/48 | 300/300 | 348/348 | 0 | 0 |
| Definitiva 1 | 48/48 | 339/339 | 387/387 | 0 | 0 |
| Definitiva 2 consecutiva | 48/48 | 339/339 | 387/387 | 0 | 0 |

TRX separati per progetto/run: [final-1](TestResults/mt3a/final-1/), [final-2](TestResults/mt3a/final-2/). Contatori XML e SHA-256, identita dei casi, inclusione di tutti i 348 precedenti, classi MT1/MT2/Profiles/Ownership e bootstrap reale Development/Production verificati in [verification.json](TestResults/mt3a/verification.json). Build completa riuscita; warning nullable preesistenti, nessuno introdotto nei file MT3A. `git diff --check` senza errori.

Le prove SQL e quelle HTTP sono distinte: i 39 casi nuovi dimostrano integrita DB; i test MT2 dimostrano ancora il blocco di ticket e token scoped A/B sulle route legacy e la revoca. **Non** sono prove HTTP positive dei flussi didattici multi-tenant, che non sono implementati.

## 18. Rischi aperti

MT-3 complessiva rimane parziale: query/manager/API scolastici e worker non tenant-aware. Le FK prevengono relazioni cross-tenant, non letture globali o accessi autorizzati soltanto dal Guid. Mitigazione operativa attuale: gate chiuso e nessun secondo tenant attivo persistente. Holding test provisioning, senza membership persistenti.

Il bridge root DB e temporaneo: eliminabile solo quando tutti i writer forniscono ownership verificata. Rigenerazioni EF richiedono riconciliazione del candidato completo. DDL non atomico e finestra di sostituzione trigger richiedono quiescenza, backup e recovery revisionato in caso di applicazione parziale; i preflight rifiutano retry ciechi. Nessuna prova di restore completo, prestazioni su carico/staging o cutover operativo. I requisiti di deployment/security audit preesistenti rimangono separati.

## 19. Prerequisiti per il checkpoint successivo

Adottare i profili nei flussi applicativi e migrare progressivamente repository/manager con filtro SQL e autorizzazione risorsa+tenant, senza riaprire gate finche i percorsi non siano verificati. Prima dell'operativita di B: account management istituzionale, ExamManager/worker/audit tenant-aware, test HTTP A/B positivi e negativi/concorrenza/revoca, prova recovery e manifest legale riconciliato, gestione esplicita del bridge pre-backfill.

CONTRACT dopo C/D/E e cutover autorizzato: rimozione User.Role/link globali, ClaimsContext/route legacy, gate/bridge e manifest root soltanto quando non piu necessari. Nessuna di queste rimozioni e eseguita ora. CourseEdition, onboarding, randomizzazione e MT4 non iniziati.
