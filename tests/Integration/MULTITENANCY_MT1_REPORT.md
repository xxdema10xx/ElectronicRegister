# ElectronicRegister - MULTITENANCY MT-1 Report

## Esito e limite

**MT-1 completata come base strutturale preparatoria. ElectronicRegister NON e ancora una piattaforma multi-tenant isolata.** Nessun secondo tenant e abilitato. Non sono stati aggiunti endpoint, modificati JWT/ClaimsContext o attivati filtri tenant sui repository legacy. Il sistema operativo corrente rimane single-tenant.

Sono implementate quattro tabelle, Domain model e repository preparatori, migrazione SQL database-first versionata, backfill DML esplicito e verifiche MySQL. Il backfill e stato provato su un dataset legacy rappresentativo e poi ripulito dal teardown: non sono state lasciate membership permanenti che alterino i reset della baseline single-tenant. Il backfill operativo permanente e un'attivita controllata futura, documentata; non viene eseguito all'avvio dell'app.

## Commit, stato iniziale e baseline

- Branch locale `refactor-database-entities`; HEAD iniziale `970d5d7` (`fix: enforce exam closure rules and preserve retake grades`). Nessun fetch/pull/reset/checkout o commit effettuato.
- Lo stato locale conteneva le modifiche **non committate** dell'audit precedente: 19 file produttivi modificati, tre file nuovi di test Audit, FINAL_BACKEND_AUDIT.md ed evidenze. Sono state conservate, non sostituite con il remoto piu vecchio. La build iniziale e la suite hanno confermato la presenza reale della baseline richiesta.
- Baseline prima delle modifiche MT-1: **39 unit + 197 integration = 236/236 Passed, 0 Failed, 0 Skipped**, MySQL MAMP reale. TRX in `TestResults/mt1/baseline/`.
- Nessun file/test preesistente dell'audit e stato modificato da MT-1, ad eccezione delle sole aggiunte Infrastructure DI/csproj necessarie a nuovi repository e resource SQL (questi due file non erano parte delle modifiche iniziali dell'audit). Nessuna modifica a SeedData, IntegrationFixture o TestDatabaseSchema. Nessuna modifica a controller/manager/API/worker per MT-1.

## Analisi e decisioni

Documenti:

- [MULTITENANCY_DESIGN.md](../../docs/architecture/MULTITENANCY_DESIGN.md): mappa completa delle 21 tabelle legacy, nuove entita, identita globale/profili tenant, CourseEdition/Enrollment, TenantContext, token/revoca, raw SQL/aggregati/worker, onboarding e PlatformOperator.
- [MULTITENANCY_MIGRATION.md](../../docs/architecture/MULTITENANCY_MIGRATION.md): expand/backfill/validate/contract, SQL, backup/recovery, database-first, limiti DDL, gate prima del secondo tenant.

Il tenant e la Fondazione ITS Academy. Region non e un tenant, Campus e una sede figlia di un solo istituto, CourseEdition non deve avere un unico campus o durata biennale fissa. User resta globale; ruoli e membership sono per istituto. I collegamenti User.Role/StudentId/TeacherId restano intatti. Student/Teacher futuri saranno profili collegati alla membership dell'istituto, conservando i Guid; gli enrollment conserveranno lo storico invece di sovrascrivere ClassId.

Non e stato attribuito un nome ITS ufficiale inventato ai dati legacy. La configurazione di backfill richiede un Guid di migrazione e nome tecnico dichiarato; il codice riservato `LEGACY_MT1` e un identificativo applicativo di holding, non ministeriale. La holding resta provisioning. LegalTaxCode e Region opzionali; nessun codice fiscale, sede o regione fittizi presentati come reali.

## Schema aggiunto e sorgente

Sorgente SQL: `migrations/mt1/001_expand.sql`, `002_backfill.sql`, `003_validate.sql`; `.gitignore` locale rende questi SQL versionabili nonostante l'ignore globale. Non esisteva un DDL completo canonico del legacy nel repository: questa limitazione e documentata, non e stato inventato uno schema ufficiale di produzione dal DB test.

| Tabella | Vincoli principali |
| --- | --- |
| institution | PK Guid; Code univoco case-insensitive; status provisioning/active/suspended/archived; nome, metadati opzionali, timestamp |
| campus | PK Guid; InstitutionId NOT NULL, FK RESTRICT; piu sedi per istituto; IsActive NOT NULL |
| institution_membership | PK Guid; FK Institution e User RESTRICT; UNIQUE(user_id,institution_id); pending/active/suspended/left, JoinedAt/SuspendedAt |
| institution_membership_role | PK(membership_id,role), FK membership RESTRICT; tre ruoli InstitutionAdmin/Teacher/Student |

Nessuna nuova colonna o modifica alle FK legacy. Nessun CHECK assunto efficace su MySQL 5.7. Le ENUM limitano i ruoli in SQL mode strict (quello verificato nel test). Non esiste SuperAdmin e non viene creato PlatformOperator come admin didattico. Estendere i ruoli richiedera una migrazione esplicita e policy dedicate.

L'EXPAND e stato eseguito solo sul database autorizzato. `Mt1SchemaMigration` e tooling esplicito non registrato in DI/Program: rifiuta connessioni con nomi database diversi da `electronic_register_test` prima di connettersi, ricontrolla DATABASE(), esegue gli SQL incorporati. DDL puo fare commit impliciti; nessun rollback atomico strutturale promesso. Backfill e invece transazionale DML, non tocca legacy, valida violazioni prima di commit, conserva sospensioni al rerun e fallisce su configurazioni/ruoli in conflitto.

## Database-first e file modificati

Le quattro entity nuove sono state **generate dal database di test**, non scritte a mano. Comando selettivo sulle quattro tabelle e user in cartella temporanea, con `--no-onconfiguring`; User/context temporanei non applicati al legacy. Configurazione mantenuta in un nuovo sidecar partial tramite OnModelCreatingPartial, con relazione unidirezionale a User: nessuna navigation aggiunta manualmente alla User generata esistente.

`migrations/mt1/Scaffold-Mt1.ps1` rigenera candidati da controllare e, solo con `-Apply`, copia le quattro entity/sidecar. Lo script e stato realmente eseguito: le quattro entity **e il sidecar** coincidono byte per byte con il codice finale. Tool EF CLI 10.0.12, EF Core/Pomelo repository 9.0.8/9.0.0; nessun upgrade pacchetti. Una prima esecuzione dello script ha rifiutato anche la connessione test per un dettaglio del setter PowerShell DbConnectionStringBuilder: corretto prima della verifica riuscita, nessuna connessione a DB non autorizzati. Hash entity nelle evidenze.

Delta MT-1 rispetto alla baseline locale, tutti file sotto `Backend/ElectronicRegisterAPI/src` salvo dove indicato:

- Domain/Enums: InstitutionRole.cs, InstitutionStatus.cs, MembershipStatus.cs.
- Domain/Models: Institution.cs, Campus.cs, InstitutionMembership.cs (lista ruoli tipizzata).
- Domain/Interfaces/Repositories: IInstitutionRepository.cs, ICampusRepository.cs, IInstitutionMembershipRepository.cs.
- Infrastructure/Persistence/Entities: Institution.cs, Campus.cs, InstitutionMembership.cs, InstitutionMembershipRole.cs, tutti generati.
- Infrastructure/Persistence: ElectronicRegisterContext.Multitenancy.cs; Migrations/Mt1SchemaMigration.cs. Il file generato ElectronicRegisterContext.cs non cambia.
- Infrastructure/Repositories: InstitutionRepository.cs, CampusRepository.cs, InstitutionMembershipRepository.cs.
- Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs: tre registrazioni scoped. Infrastructure.csproj: resource SQL versionate, nessuna dipendenza nuova.
- `migrations/mt1/`: tre SQL, .gitignore e Scaffold-Mt1.ps1.
- `docs/architecture/`: i due documenti richiesti.
- `tests/Integration/Mt1IntegrityTests.cs`, questo report ed evidenze `TestResults/mt1/`.

InstitutionRepository non consente di creare un record Active; non ha un metodo di attivazione. Campus e Membership richiedono InstitutionId nei lookup/operazioni. Questo e uno scope di persistenza, **non una verifica di autorizzazione dell'utente**: nessun TenantContext operativo e ancora introdotto. I nuovi repository non vengono usati dalle API legacy.

## Backup e validazione dati

Server effettivo: MySQL **5.7.24** su MAMP, porta 3306; `electronic_register_test`; InnoDB, REPEATABLE-READ, STRICT_TRANS_TABLES. Verificata la collation User.Id char(36)/ascii_general_ci prima di definire le FK.

Backup completo esportato prima del DDL con mysqldump, conservato localmente in `Backend/ElectronicRegisterAPI/mt1-evidence/legacy-before-expand.sql` (ignorato da Git, non incluso nei report perche contiene dati/password hash). SHA-256 `02C7820A5960EB1E0EBD09BA74D9CBDF70348DAA84549BA2D5A431768CF76B76`. Export e leggibilita verificati; dump canonici dati prima/dopo EXPAND sulle 21 tabelle identici, SHA-256 `AFB4C08687EFD292B5B38604EEF538DED02CF0B957E7369A24E4BEDC8FDEABBB`.

Il test controllato di backfill costruisce un grafo rappresentativo usando Submit/review/finalize esistenti (non li modifica), poi confronta conteggi e SHA-256 di **ogni cella** di tutte le 21 tabelle: comprende Guid, email/password hash, profili, classi, materie, esami, risposte, voti e audit. Non soltanto il numero di righe. Manifest con conteggi e digest nei TRX; nessun hash password grezzo stampato.

Membership ricostruite una per ciascun User e ruolo legacy mappato allo stesso istituto; nessuna modifica User.Role e nessun privilegio globale nuovo. Rerun conserva Id/JoinedAt e stato suspended, senza duplicati. Seconda LegacyInstitutionId con stesso codice di migrazione viene rifiutata senza alterare legacy. L'attribuzione di tutti i dati storici alla holding e un manifest di migrazione, anche per righe scollegate, **non ancora InstitutionId materializzato sulle tabelle legacy**.

Non eseguito restore integrale distruttivo del database; procedura e obbligo di prova recovery prima di cutover operativo sono documentati. Un file di backup con hash corretto non viene presentato come prova di disaster recovery. Nessuna modifica su database diversi da quello test.

## Test aggiunti

**21 nuovi casi MySQL**, nessun mock di persistenza e nessun test esistente alterato:

| Test | Casi | Garanzia provata |
| --- | ---: | --- |
| MultipleFoundationsInSameRegionRemainDistinctAndProvisioning | 1 | Regione condivisa, Id/code distinti, timestamp e provisioning |
| InstitutionCodeIsUniqueIncludingCaseVariants | 2 | UNIQUE codice anche case-insensitive, MySQL 1062 |
| MultipleCampusesBelongToExactlyOneFoundationAndInactiveFalsePersists | 1 | Due sedi, scope Institution, PK impedisce stessa sede in due istituti, false non perso nel default |
| GlobalUserCanHaveDifferentAndMultipleRolesPerFoundation | 1 | User comune, membership diverse, piu ruoli nella stessa, User.Role legacy invariato |
| DuplicateMembershipAndDuplicateRoleAreRejectedByMySql | 1 | Coppia User/Institution unica e ruolo duplicato respinto 1062 |
| SuspensionPreservesMembershipRolesIdentityAndEveryLegacyCell | 1 | Sospensione persistita da nuovo contesto; dati storici identici |
| MembershipOperationsRejectAnotherInstitutionGuid | 1 | Operazione scoped non usa membership Guid di altra fondazione |
| InvalidForeignKeysAreRejectedByMySql | 4 | Campus/Institution, Membership/Institution, Membership/User, Role/Membership; 1452 |
| HistoricalParentsCannotBeCascadeDeleted | 3 | Institution, User, Membership referenziati non cancellati: 1451 e storia presente |
| UnknownGlobalPrivilegeCannotBeInsertedAsMembershipRole | 1 | SuperAdmin non accettato da enum DB con mode strict, 1265 |
| PreparatoryRepositoryDoesNotActivateFoundations | 1 | Nessuna attivazione tramite Add preparatorio |
| MigrationToolRejectsOtherDatabaseBeforeOpeningConnection | 2 | EXPAND/BACKFILL rifiutano nome DB non autorizzato senza connettersi |
| CanonicalExpandIsRerunnableAndDoesNotChangeLegacyData | 1 | SQL ripetibile e legacy invariato, quattro FK RESTRICT effettive |
| ControlledLegacyBackfillPreservesEveryGuidAndCellAndIsIdempotent | 1 | Backfill DML reale, mapping tutti User, manifest 21 tabelle, rerun/sospensione/conflitto |

Snapshot e verifiche hanno nuovi scope/DbContext o connessioni SQL. I test vengono eseguiti nella collection MySQL serializzata esistente; teardown cancella solo le nuove righe di prova, non modifica il seed condiviso. Sono prove di struttura e migrazione, **non di isolamento delle 73 API fra tenant**. I test bootstrap reali dell'audit continuano a verificare esattamente le stesse 73 azioni.

## Risultati finali

```powershell
# Da Backend/ElectronicRegisterAPI; connessione di test fornita dall'utente nella sessione.
dotnet build ElectronicRegisterAPI.slnx
dotnet test ../../tests/Integration/Integration.Tests.csproj --no-build --filter "FullyQualifiedName~Mt1IntegrityTests" --logger:trx --results-directory ./mt1-evidence/new-tests
dotnet test ElectronicRegisterAPI.slnx --no-build --logger:trx --results-directory ./mt1-evidence/final-1
dotnet test ElectronicRegisterAPI.slnx --no-build --logger:trx --results-directory ./mt1-evidence/final-2
```

| Esecuzione | Unit Passed/Totale | Integration Passed/Totale | Failed | Skipped | Totale |
| --- | ---: | ---: | ---: | ---: | ---: |
| Baseline pre-MT1 | 39/39 | 197/197 | 0 | 0 | 236/236 |
| Nuovi casi mirati | - | 21/21 | 0 | 0 | 21/21 |
| Completa finale 1 | 39/39 | 218/218 | 0 | 0 | 257/257 |
| Completa finale 2 consecutiva | 39/39 | 218/218 | 0 | 0 | 257/257 |

Build completa riuscita, zero errori. L'ultima build incrementale mostra 0 warning; le ricompilazioni mostrano gli 11 warning nullable preesistenti dell'audit, nessuno nuovo nei file MT-1. `git diff --check` senza errori. Contatori TRX total/executed/passed/failed/notExecuted verificati per entrambi i progetti di entrambi i run, e i 236 nomi baseline confrontati con il run finale. Nessuno Skipped contato come Passed.

Evidenze versionabili: `tests/Integration/TestResults/mt1/` con baseline, run mirato, final-1/final-2 (unit.trx/integration.trx), digest scaffold ed export-verification; dump sensibili esclusi. Questi file distinguono i risultati reali dalle sole affermazioni del report.

## Limitazioni e problemi aperti

1. JWT e ClaimsContext legacy non portano InstitutionId/MembershipId/ruoli scoped. I repository legacy continuano a leggere il dataset single-tenant e il worker non ha ancora TenantContext. Non usare una membership nuova per ammettere una seconda fondazione alle API esistenti.
2. InstitutionId non e stato aggiunto alle tabelle legacy; ownership materializzata e FK composite sono nelle fasi successive. Le membership non bastano ad attribuire righe senza percorsi FK a un User: serve il manifest/crosswalk esplicito.
3. Backfill permanente, identita legale della fondazione legacy e profili incoerenti/senza User richiedono preflight e configurazione verificati; nessuna normalizzazione silenziosa. Nessun onboarding, PlatformOperator o gestione inviti implementati.
4. La sospensione e persistita ma non revoca ancora token legacy. Versione autorizzazioni/revoca e linearizzazione delle richieste concorrenti sono progettate, non provate in MT-1.
5. ENUM ruoli rifiuta unknown in SQL strict; nuove categorie richiedono DDL versionato e policy. SQL mode non strict, modifiche manuali DB o uso di repository senza autorizzatore non costituiscono una modalita multi-tenant supportata.
6. Nuove FK RESTRICT impediscono cancellare identita con membership storiche. La futura amministrazione deve disattivare account/membership e gestire l'errore applicativo appropriato, senza DELETE globale; gli endpoint legacy non sono stati modificati in MT-1.
7. DDL MySQL 5.7 non atomico; backup export verificato ma restore completo non provato. Necessaria prova recovery in finestra test prima di applicazioni permanenti. Versione MAMP 5.7.24 non e una baseline di deployment produttivo supportato.
8. Concorrenza durante migrazione e carichi multi-tenant non testati; nessun secondo tenant operativo abilitato. Le gare Exam/Submission esistenti e la loro baseline sono preservate, non ridisegnate.

## Prossimo passo MT-2

Implementare in modo separato il contratto TenantContext autenticato, selezione istituto verificata, JWT scoped e controllo stato/ruoli/revoca membership, definendo la compatibilita temporanea senza fallback globale. Pianificare poi migrazione di tutti i repository/manager/worker/raw SQL/audit, InstitutionId e FK composite, profili/enrollment, test negativi A/B e frontend. **Non abilitare una seconda fondazione finche l'intera catena di isolamento non e implementata e verificata.** Nessuna attivita MT-2 eseguita da questa consegna.
