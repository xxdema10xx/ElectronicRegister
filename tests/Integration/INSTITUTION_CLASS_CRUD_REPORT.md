# Institution Class CRUD — 22 ottobre 2026

Branch `refactor-database-entities`. Implementazione completata senza commit o push.
Baseline locale verificata prima di aggiungere test: **979 identita**. Risultato finale:
**1034/1034 test superati, zero failure e zero skip** (90 unitari, 944 integrazioni).
Le 979 identita precedenti sono tutte presenti; aggiunte 14 prove unitarie e 41 HTTP.

## Endpoint e contratti

| Metodo | Route | Risposta |
| --- | --- | --- |
| GET | /api/institution/classes | 200, pagina di classi |
| GET | /api/institution/classes/{id} | 200, DTO ed ETag; 404 se indisponibile |
| POST | /api/institution/classes | 201, GUID generato dal server e Location recuperabile |
| PUT | /api/institution/classes/{id} | 204; If-Match obbligatorio |
| DELETE | /api/institution/classes/{id} | 204 soltanto per classe eliminabile |

InstitutionClassDto contiene Id, Name, BienniumStudyPathId, CourseEditionId nullable
e Revision. La pagina contiene Items, TotalCount, PageNumber e PageSize. Parametri:
pageNumber=1, pageSize=20, massimo 100; filtri opzionali BienniumStudyPathId e
CourseEditionId. Filtri, ordinamento Name/Id, Skip e Take sono applicati in SQL.
Le classi senza CourseEdition non sono escluse. Non sono restituiti entity EF,
InstitutionId o dati di altri tenant. Una pagina usa count e proiezione SQL, senza N+1.

CreateInstitutionClassDto accetta soltanto Name e BienniumStudyPathId. Il nome viene
trimmato e validato a 1-20 caratteri; il percorso deve essere non vuoto, esistente e
di proprieta del tenant corrente. UpdateInstitutionClassDto accetta soltanto Name.
JsonUnmappedMemberHandling.Disallow respinge ID, owner, audit, sicurezza e riferimenti
strutturali sconosciuti. InstitutionId non e accettato neppure come filtro query.
L'unicita viene fatta rispettare dal vincolo MySQL reale, inclusa la collation
utf8mb4_unicode_ci, senza simulare la comparazione con regole .NET differenti.

Il contratto per React Native, esempi fetch, ETag ed errori sono documentati in
[docs/api/INSTITUTION_CLASSES.md](../../docs/api/INSTITUTION_CLASSES.md).

## Autorizzazione e separazione dei layer

Il controller e sottile: InstitutionSchoolEndpoint, policy InstitutionSchoolAccess,
schema InstitutionAccess e ruolo InstitutionAdmin. LegacyEndpointGate verifica il
marker e la policy effettivi; nessuna autorizzazione deriva dal prefisso della route.
Il rollout usa SchoolApi.Enabled, ancora false per default.

InstitutionClassManager usa InstitutionSchoolAuthorization.AdminAsync per ottenere
TenantContext verificato e rivalidare la membership corrente. Il repository applica
owner esplicito su INSERT e predicati tenant/ID su letture e mutazioni. Teacher e
Student non possono neppure leggere questa superficie. JWT legacy e ruoli/purpose
estranei non sono accettati; una generazione revocata non torna valida riattivando
la membership. ID assente e ID di altra Fondazione producono 404 indistinguibile.

Componenti nuovi: IInstitutionClassManager, InstitutionClassManager,
IInstitutionClassRepository, InstitutionClassRepository, InstitutionClassController,
DTO dedicati e InstitutionClassRules. Domain -> Application -> Infrastructure -> API
resta invariato; nessun DbContext o business rule nel controller.

## Cancellazione e inventario FK effettivo

L'inventario e stato eseguito su information_schema della MySQL 5.7.24 isolata prima
di implementare/applicare la cancellazione. Esistono cinque vincoli entranti:

| Figlio | Vincolo | Azione SQL |
| --- | --- | --- |
| student | fk_student_class | SET NULL |
| student | fk_mt3a_student_class | SET NULL |
| class_subject | fk_cs_class | CASCADE |
| class_subject | fk_mt3a_cs_class | CASCADE |
| course_administration_event | fk_mt4c_class | RESTRICT |

DELETE acquisisce un lock esclusivo tenant/ID sulla classe prima delle letture di
dipendenza. Studenti, ClassSubject, CourseEdition e riferimenti amministrativi
bloccano con 409. ClassSubject protegge anche tutti gli esami, submission e voti
raggiungibili. Non vengono scollegati studenti o materie e non viene invocata una
cancellazione in cascata come strategia applicativa.

Prima del DELETE il repository confronta i vincoli entranti reali con un allowlist
chiuso: un vincolo nuovo o mancante blocca la cancellazione finche l'inventario non
viene aggiornato. Il lock sul padre e le FK serializzano gli inserimenti concorrenti
dei figli. Le violazioni SQL sono tradotte in conflitti generici, senza dettagli SQL.
Il test con esame, submission e voto reali confronta il digest di tutti i dati
didattici prima/dopo DELETE respinta e rinomina: nessuna associazione viene modificata.

Una classe completamente vuota, non attribuita e senza riferimenti esterni puo
essere eliminata. I suoi snapshot nel ledger dedicato sono storici, non FK vive a
Class, e rimangono integri dopo il DELETE. Non e stato aggiunto IsArchived o Status.
La dismissione di classi con dati richiede un futuro workflow di archiviazione.

## Audit e concorrenza

Il ledger dei corsi richiede CourseEditionId e quello generico ammette soltanto
classificazioni exam/exam_submission/platform: nessuno dei due rappresenta correttamente
una classe non attribuita. E stato quindi aggiunto institution_class_event con owner,
attore user/membership, identificativo storico della classe, azione, revisione,
snapshot essenziali prima/dopo e timestamp UTC server. Non vengono inventate edizioni.

FK restrittive mantengono l'integrita di owner e attore. ClassId e uno snapshot senza
FK a Class, per consentire l'eliminazione fisica mantenendo la storia. Tre trigger
verificano attore/forma e vietano UPDATE/DELETE del ledger, anche per root: non e
stata introdotta alcuna eccezione di purge per i test. Mutazione e audit sono nella
stessa transazione. Tre fault injection, dopo l'audit reale, verificano rollback
completo di POST, PUT e DELETE.

Lo stesso ledger conserva le revisioni senza nuove colonne sulla tabella Class.
Classi preesistenti: revisione 0, nessun backfill. Creazione: revisione 1. PUT richiede
If-Match con la revisione letta; il lock Class precede MAX(revision), controllo,
UPDATE del solo Name e append audit. Il vincolo univoco owner/class/revision fornisce
un'ulteriore protezione persistente. Due rinomine dalla stessa revisione hanno un
solo vincitore e un 409; If-Match mancante restituisce 428. Nessuna dipendenza da
istanza API o memoria. CourseEditionId non viene sovraccaricato come token.

Il validatore offline InstitutionClassIntegrity classifica il nuovo ledger soltanto
dopo controlli su owner, attori, JSON, forma, sequenze, FK restrittive, indice univoco
e trigger append-only. Il rifiuto di altre tabelle sconosciute rimane invariato.

## SQL, bootstrap e sicurezza operativa

Migrazione additiva 20261022.2: una tabella e tre trigger; la tabella Class e le sue
semantiche non cambiano. File: migrations/institution-class/001_expand.sql,
002_validate.sql e README.md. Il manifest SHA256, bootstrap consolidato, schema
contract, validatore del setup e LOCAL_DATABASE_SETUP.md sono aggiornati.
Inventario finale: **41 tabelle, 91 trigger, 120 FK, 167 indici**.

Lo script Invoke-InstitutionClassMigration.ps1 attesta server 5.7.24, porta 13306,
database electronic_register_test e suffisso datadir isolato; rifiuta tabella gia
presente, connessioni/transazioni concorrenti e directory di evidenze preesistente.
Esporta l'inventario FK, produce backup completo con SHA256, applica SQL una sola
volta e controlla i validatori. Un secondo tentativo e stato realmente respinto
prima di qualsiasi DDL o creazione della directory di evidenze.

Verificati sia il bootstrap completo su una nuova istanza vuota, usando il file
consolidato candidato a Git, sia l'upgrade tramite lo script pubblico su un'altra
istanza con il precedente schema pubblico. Nessun dump privato costituisce input
del bootstrap. Tutte le definizioni e metadata di 41 tabelle e 91 trigger coincidono
con il contract; verificate anche 120 FK e 167 indici. I tre contatori del validatore
SQL sono zero. Nessuna DDL ha avuto esito parziale.

La porta 3306 non e stata usata ne aperta dai comandi di lavoro. Tutti i database di
prova sono nuovi, separati e identificati sulla 13306; quelli usati sono conservati
nei datadir privati. Nessun reset, restore, modifica di GLOBAL sql_mode o disabilitazione
di trigger/FK. Le fixture con audit immutabile conservano i rispettivi attori nel
database di prova anziche introdurre un bypass append-only per il teardown.

## Test eseguiti

- Baseline enumerata: 979 identita; confronto con l'enumerazione finale: nessuna mancante.
- Build completa della soluzione riuscita.
- Nuovi mirati: 14 unitari e 41 integrazioni, tutti superati.
- Mirati ampliati ai guard legacy/offline e inventari: 14 unitari e 48 integrazioni,
  tutti superati dopo la classificazione del ledger e l'adeguamento dei conteggi route.
- Regressione finale: 90 unitari + 944 integrazioni = **1034 superati**, zero skip/failure.

Le integrazioni avviano il Program ASP.NET Core reale, Kestrel, JWT InstitutionAccess
e MySQL 5.7.24. Coprono CRUD valido, Location, pagina/dettaglio, trimming/limiti,
percorsi mancanti e stranieri, collation/case/accenti, inserimenti e update concorrenti,
dipendenze Student/ClassSubject/CourseEdition/storiche, isolamento A/B, ruoli negati,
JWT legacy/PlatformOperator, revoca/generazione, rollout, campi sconosciuti e
strutturali, utilizzo immediato tramite Student CRUD e workflow edizione, audit
immutabile/atomico, rollback e inventario delle cinque route con policy esatta.

La prima regressione ha rilevato i guard che rifiutavano il ledger non ancora
classificato, i vecchi totali route e riferimenti legacy nelle nuove fixture
istituzionali. Sono stati corretti senza indebolire le assertion di sicurezza.
Le fixture nuove usano soltanto profili istituzionali, senza puntatori legacy.
Nessun test preesistente e stato eliminato, rinominato o disabilitato.

Il riepilogo pubblico e INSTITUTION_CLASS_CRUD_VERIFICATION.json. I log grezzi restano
ignorati da Git; gli artefatti storici rigenerati dai test vengono preservati nella
loro versione iniziale, senza modificare il report precedente sugli esami.

## GitHub, file e limiti

File nuovi: controller/manager/repository e relative interfacce, InstitutionClassDtos,
InstitutionClassRules, entita InstitutionClassEvent e mapping partial del DbContext,
InstitutionClassIntegrity, tre file di test integrazione e test unitari, directory
migrations/institution-class, script di migrazione, contratto docs/api e questo
report/riepilogo. File condivisi aggiornati: DI Application/Infrastructure, richiamo
mapping e classificazione offline, cinque inventari/assertion route, bootstrap,
contract, manifest, setup Python, LOCAL_DATABASE_SETUP.md, .gitignore/.gitattributes.

Gli SQL nuovi sono ammessi esplicitamente; il contratto API ha attributo LF per
SHA256 riproducibili. Verificati hash e visibilita Git degli artefatti pubblici.
Backup, datadir e log non sono candidati a Git; nessuna credenziale reale o chiave
privata e aggiunta. Le modifiche precedenti agli esami, inclusi codice, migrazioni,
report e parte originale del bootstrap, sono preservate: controllati gli hash.

Limiti intenzionali: modifica del solo nome, nessun trasferimento di percorso o
studenti, nessuna attribuzione edizione implicita, nessuna archiviazione e nessuna
API di consultazione del ledger classi in questa versione. PUT richiede If-Match.
Python non e installato nell'ambiente: il driver Python di setup non e stato avviato;
bootstrap e verifiche equivalenti sono stati eseguiti col client MySQL nativo e lo
script PowerShell pubblico di migrazione e stato eseguito realmente. Nessun commit/push.

### File nuovi da aggiungere a Git

- `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionClassController.cs`
- `Backend/ElectronicRegisterAPI/src/Application/ElectronicRegisterAPI.Application/Managers/InstitutionClassManager.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/DTOs/InstitutionClassDtos.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/Interfaces/Managers/IInstitutionClassManager.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/Interfaces/Repositories/IInstitutionClassRepository.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/Models/InstitutionClassRules.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/ElectronicRegisterContext.InstitutionClasses.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/Entities/InstitutionClassEvent.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/Migrations/InstitutionClassIntegrity.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Repositories/InstitutionClassRepository.cs`
- `docs/api/INSTITUTION_CLASSES.md`
- `migrations/institution-class/001_expand.sql`
- `migrations/institution-class/002_validate.sql`
- `migrations/institution-class/README.md`
- `tests/Integration/INSTITUTION_CLASS_CRUD_REPORT.md`
- `tests/Integration/INSTITUTION_CLASS_CRUD_VERIFICATION.json`
- `tests/Integration/InstitutionClassCrudFixture.cs`
- `tests/Integration/InstitutionClassCrudSecurityTests.cs`
- `tests/Integration/InstitutionClassCrudTests.cs`
- `tests/Unit/InstitutionClassRulesTests.cs`
- `tools/Invoke-InstitutionClassMigration.ps1`

Le modifiche preesistenti degli esami restano pendenti e devono essere incluse nella
futura pubblicazione con i relativi candidati, senza scartarle o sovrascriverle.

INSTITUTION CLASS CRUD IMPLEMENTED AND VERIFIED
