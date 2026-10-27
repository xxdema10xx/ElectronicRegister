# Institution admin academic management — 2026-10-22

Branch: `refactor-database-entities`. Work is local and uncommitted. The preceding
Exam presentation/name and Class CRUD implementations are preserved. No commit,
push, reset, restore or destructive checkout was performed.

## Administrative coverage matrix

Staff extension (2026-10-27): see [Staff API contract](../api/INSTITUTION_STAFF_PERMISSIONS.md)
and [verification report](../../tests/Integration/INSTITUTION_STAFF_ROLE_REPORT.md).
Staff adds local Student/Teacher, Grade and Exam CRU, with explicit operation capabilities.
It does not add IAM, configuration, transfers, structural catalog writes or DELETE.
Admin + Staff retains Admin privileges; Staff + Teacher cannot recover exam DELETE/cancel.
Coverage and outstanding checks are recorded separately from this historical academic CRUD report.

| Resource | Coverage | Boundary |
| --- | --- | --- |
| StudyArea | Complete CRUD | Tenant uniqueness; no dependent delete |
| StudyPath | Complete CRUD | Does not change class paths or editions |
| Biennium | Complete CRUD | DateOnly, valid pair, no inferred edition duration |
| BienniumStudyArea | Intentional partial CRUD | Create/read/delete; immutable FK |
| BienniumStudyPath | Intentional partial CRUD | Create/read/delete; immutable FK; blocks linked Class |
| Class | Existing complete safe CRUD | PUT Name only; edition assignment stays separate |
| Subject | Existing CRUD | Subject rename preserves derived Exam.Name |
| Teacher | Existing CRUD | Tenant/profile/assignment protections retained |
| Student | Existing CRUD | Dedicated audited within-institution class transfer; see STUDENT_CLASS_TRANSFER.md |
| ClassSubject | Complete safe CRUD | Teacher-only PUT without Exam/Grade/history; teacher list retained |
| Campus | Complete CRUD | Active toggle; linked/historical delete blocked |
| CourseEdition | Intentional partial CRUD | Create/read/descriptive update; no generic DELETE |
| CourseEditionCampus | Dedicated workflow | Existing link/read; no automatic unlink |
| Class edition attribution | Dedicated workflow | Existing immutable assignment; no transfer |
| StudentEnrollment | Dedicated workflow | Create/read, transition, completion correction/history; no generic PUT/DELETE |
| Exam / questions / options / assigned students | Existing exam workflow | Draft/publish/close/recovery and template management preserved |
| ExamSubmission / answers / violations | Existing attempt workflow | Start/resume/submit/review/finalize; no administrative historical CRUD |
| Grade | Existing protected CRUD/grading | Persistent IDs and automatic/manual correction unchanged |
| Institution membership / roles | Dedicated IAM workflow | Current generation/status/role validation; no implicit platform privileges |
| Invitations / onboarding / recovery | Dedicated IAM/governance workflows | Existing boundaries retained |
| Institutional school profiles | Existing scoped profile workflows | No global account CRUD introduced |
| Institution identity/directory | Existing read / governance boundaries | No new Foundation CRUD or Campus-as-Institution model |
| Administrative and exam ledgers | Append-only / scoped reads | No event UPDATE/DELETE APIs |
| New shared catalog/resource ledger | Persistence only | Audited SQL history; dedicated history HTTP API not requested/added |
| Class/Campus archival, historical teacher reassignment | Not implemented | Separate policy/workflow needed |
| Student class transfer within an institution | Dedicated workflow | Audited, calendar-limited; optional atomic course withdrawal/new enrollment |
| Class/course structural transfers; repeated enrollment episodes | Not implemented | Intentionally excluded |
| CourseEdition lifecycle or generic historical deletion | Not added | Requires explicit workflow/policy |

## Delivery phases and verification

| Phase | New endpoints | Added test outcomes | Completed regression |
| --- | --- | --- | --- |
| A: academic catalog | 23 | 7 unit + 32 integration | 1073 passed: 97 + 976 |
| B: ClassSubject | 4, preserving list | 19 integration | 1092 passed: 97 + 995 |
| C plus cross-surface checks | 4, preserving list | 6 unit + 32 integration | 1130 passed: 103 + 1027 |

The initial enumeration confirmed all **1034** documented identities (90 unit,
944 integration). No test was removed, renamed, disabled or skipped to get green.
Existing exact route counts/manager expectations were updated only where new
surfaces make them incompatible. Legacy rejection and policy assertions remain.
Final MVC inventory is **199**: 73 legacy, 6 identity, 52 school, 37 exam, 23 course,
8 membership-admin. New/expanded resources comprise 33 routes, 31 net additions.

Targeted checks: catalog 22 initially, ClassSubject 18 after race/rollback additions,
Campus/security 30, combined previous Class + all new HTTP tests **124 passed**.
The subsequent global run also includes the new dependency-race case and all old
exam/randomization/grading/expiry/worker/IAM suites. Detailed phase reports:

- `tests/Integration/INSTITUTION_ACADEMIC_CATALOG_CRUD_REPORT.md`
- `tests/Integration/INSTITUTION_CLASS_SUBJECT_CRUD_REPORT.md`
- `tests/Integration/INSTITUTION_CAMPUS_CRUD_REPORT.md`

## Security, concurrency and audit

All new administrative commands/detail reads require the active selected
InstitutionAdmin membership through InstitutionAccess and explicit endpoint
markers/policies. The existing teacher ClassSubject list remains owner/profile
scoped. DTOs disallow unknown members. IDs/owner/audit come from server and verified
tenant, never request authority. SQL query predicates enforce tenant ownership;
foreign and absent IDs have indistinguishable 404 responses. SQL errors are hidden.

Tenant-first authorization read locks hold membership/roles/security state stable
against IAM mutation. Exclusive resource row locks protect mutations and parent
FK races. Durable per-resource unique audit revisions make If-Match concurrency
safe even if a waiting transaction retains an older REPEATABLE READ snapshot:
the duplicate revision rolls back its entire mutation. No in-memory lock/cache.
Concurrent names/pairs use actual MySQL UNIQUE/collation constraints.

Creation of an institutional exam now takes the tenant-first lock and a current
ClassSubject ownership/teacher lock at its existing repository write boundary.
This closes the race with safe association reassignment/deletion. Existing exams
already block those operations, so exam updates receive no new assignment lock.
There is no exam engine rewrite, altered grading or permutation regeneration.

DELETE inventories incoming SQL FK at runtime and uses current locking child
reads. Unknown non-id references fail closed. Dependencies produce 409 before a
cascade can run; FK/triggers remain the final guard against concurrent insertions.
No student, class association, exam, submission, grade, edition or history is
automatically unlinked/deleted. Historical retirement requires future archival.
The metadata-discovered child queries explicitly join the tenant-owned parent in
SQL, and role locking queries explicitly join the tenant-owned membership.

One new `institution_administration_event` ledger records tenant, actor user and
membership, resource type/ID, action, revision, before/after JSON and server UTC.
Owner and composite actor FK are restrictive; UPDATE/DELETE triggers reject even
root. No live resource FK: unused-resource deletion preserves immutable history.
Mutation and audit share the transaction; real HTTP fault injection after audit
verifies create/update/delete rollback for all three modules. A mid-request
revocation test verifies the transactional revalidation boundary.

## Schema and reproducibility

Before: 41 tables / 91 triggers / 120 FK / 167 indexes.
After: **42 / 94 / 122 / 170**. Existing business table/FK semantics are unchanged.
Additive MySQL 5.7.24 migration: `migrations/institution-administration/001_expand.sql`;
validator `002_validate.sql`, README and guarded PowerShell runner. EF Database
First entity/partial context and strict offline classification are updated.
The public bootstrap, schema contract and SHA256 manifest include the ledger.

The API test instance bound 13306 using a new process and fresh ignored datadir;
the exact new datadir was attested before DDL. No preexisting instance was adopted. Two additional
fresh datadirs prove the migration path on 13307 and full candidate-only bootstrap
on 13308. All instances attest MySQL 5.7.24, port and exact datadir. Initial backups
and hashes precede DDL; the migration runner also takes a full preceding-schema
backup and refuses replay. No DDL failed partially; no automatic reset/restore.

Both proof instances match all **42 table definitions, 94 trigger bodies and 94
complete trigger metadata records**, plus FK/index inventory. Ten versioned SQL
validators pass on both paths. The preceding public bootstrap prefix is byte-for-
byte identical to its initial working-directory hash, retaining Exam and Class DDL.
The fresh bootstrap source came from a private copy of **138 manifest candidates**,
all SHA256-verified and Git-trackable, with no data dump as an input.

Port 3306 was never connected to or modified. No FK/trigger disabling or GLOBAL
sql_mode change. Private datadir, backup, logs, compiler helpers and test TRX stay
ignored. No credentials/keys/certificates are added to public candidates.

## GitHub and frontend readiness

New source is grouped into dedicated catalog/ClassSubject/Campus DTOs, manager/
repository interfaces and implementations, controllers, validation rules, shared
administrative transaction/audit helper, DB-first ledger mapping and tests.
Shared changes: scoped DI, exact route assertions, offline classifier, consolidated
bootstrap/contract/manifest, setup validators, `.gitignore`/`.gitattributes` and setup
documentation. Preserve and publish the preceding Exam/Class files together with
these changes. The sanitized verification JSON and changed-file inventory accompany
this report; raw evidence and backups do not belong in Git.

React Native contract: `docs/api/INSTITUTION_ACADEMIC_MANAGEMENT.md`. Existing
ClassSubject/Campus array and choice ID fields remain compatible; pagination adds
SQL limits and X-Total-Count. Catalog lists use a page envelope. Detail GET exposes
ETag; PUT requires If-Match (428 absent, 400 malformed, 409 stale). POST returns a
recoverable Location. No unnecessary React code changes.

**Frontend-ready does not mean operationally enabled.** SchoolApi, ExamApi and
CourseApi rollout defaults remain false. No deployment or rollout flag changed.

## Limitations and verification tooling

Historical association rewrites, structural class/course transfers, archival and lifecycle changes remain the
explicitly excluded workflows above. Catalog association details expose IDs;
descriptive data is available from scoped catalog details instead of large graphs.
Campus names are not artificially made unique. Existing rows receive no fictitious
audit backfill; revision zero advances on their first real mutation.

The original implementation verification used native MySQL and a Node harness
because Python had not been located. On 2026-10-22 this pending check was resolved
with the existing MySQL Shell Python 3.13.0 runtime: the actual Python setup
validator completed a fresh candidate-only bootstrap and full SQL verification
on a new isolated instance. All 11 Python tooling tests also passed. See
[the publication validation report](GITHUB_PUBLICATION_VALIDATION.md) for commands,
schema inventory and the new regression results. No alternate database provider
or mocked repository replaced real HTTP/MySQL integration tests.
The sandbox does not permit CIM listener inventory. Native netstat PID matching
and exact MySQL datadir attestation were used for ownership verification. All three
owned test instances were cleanly shut down after verification; private datadirs
and evidence are retained and ignored. No existing user process was stopped.

Final global result: **1130/1130 passed**, 103 unit + 1027 integration, zero failures
and zero skips. All 1034 baseline identities are preserved; 96 added. The
identity-preservation and schema proof are recorded
in `tests/Integration/INSTITUTION_ADMIN_ACADEMIC_VERIFICATION.json`.

## File candidates changed by this delivery

This list is relative to the initial local working directory, not only HEAD; it
preserves the preceding uncommitted Exam/Class additions. Review/add these files
together with the already pending prior implementation. No staging or commit done.

- `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionAcademicHttp.cs`
- `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionBienniumController.cs`
- `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionBienniumStudyAreaController.cs`
- `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionBienniumStudyPathController.cs`
- `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionStudyAreaController.cs`
- `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionStudyPathController.cs`
- `Backend/ElectronicRegisterAPI/src/Application/ElectronicRegisterAPI.Application/Managers/InstitutionAcademicCatalogManager.cs`
- `Backend/ElectronicRegisterAPI/src/Application/ElectronicRegisterAPI.Application/Managers/InstitutionCampusManager.cs`
- `Backend/ElectronicRegisterAPI/src/Application/ElectronicRegisterAPI.Application/Managers/InstitutionClassSubjectManager.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/DTOs/InstitutionAcademicCatalogDtos.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/DTOs/InstitutionCampusDtos.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/DTOs/InstitutionClassSubjectDtos.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/Interfaces/Managers/IInstitutionAcademicCatalogManager.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/Interfaces/Managers/IInstitutionCampusManager.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/Interfaces/Managers/IInstitutionClassSubjectManager.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/Interfaces/Repositories/IInstitutionAcademicCatalogRepository.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/Interfaces/Repositories/IInstitutionCampusRepository.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/Interfaces/Repositories/IInstitutionClassSubjectRepository.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/Models/InstitutionAcademicRules.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/Models/InstitutionCampusRules.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/ElectronicRegisterContext.InstitutionAdministration.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/Entities/InstitutionAdministrationEvent.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/Migrations/InstitutionAdministrationIntegrity.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Repositories/InstitutionAcademicCatalogRepository.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Repositories/InstitutionAdministrationPersistence.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Repositories/InstitutionCampusRepository.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Repositories/InstitutionClassSubjectRepository.cs`
- `docs/api/INSTITUTION_ACADEMIC_MANAGEMENT.md`
- `docs/development/INSTITUTION_ADMIN_COVERAGE_REPORT.md`
- `migrations/institution-administration/001_expand.sql`
- `migrations/institution-administration/002_validate.sql`
- `migrations/institution-administration/README.md`
- `tests/Integration/INSTITUTION_ACADEMIC_CATALOG_CRUD_REPORT.md`
- `tests/Integration/INSTITUTION_CAMPUS_CRUD_REPORT.md`
- `tests/Integration/INSTITUTION_CLASS_SUBJECT_CRUD_REPORT.md`
- `tests/Integration/InstitutionAcademicAuditFaultTests.cs`
- `tests/Integration/InstitutionAcademicCatalogTests.cs`
- `tests/Integration/InstitutionAcademicManagementSecurityTests.cs`
- `tests/Integration/InstitutionAdministrationDependencyRaceTests.cs`
- `tests/Integration/InstitutionCampusAuditFaultTests.cs`
- `tests/Integration/InstitutionCampusCrudTests.cs`
- `tests/Integration/InstitutionClassSubjectAuditFaultTests.cs`
- `tests/Integration/InstitutionClassSubjectCrudTests.cs`
- `tests/Unit/InstitutionAcademicRulesTests.cs`
- `tests/Unit/InstitutionCampusRulesTests.cs`
- `tools/Invoke-InstitutionAdministrationMigration.ps1`
- `.gitattributes`
- `.gitignore`
- `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCampusController.cs`
- `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionClassSubjectController.cs`
- `Backend/ElectronicRegisterAPI/src/Application/ElectronicRegisterAPI.Application/DependencyInjection/AddApplicationManagers.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/ElectronicRegisterContext.Multitenancy.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/Migrations/Mt3aOwnershipMigration.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Repositories/ExamRepository.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Repositories/InstitutionExamDataScope.cs`
- `docs/development/LOCAL_DATABASE_SETUP.md`
- `migrations/bootstrap/001_schema.sql`
- `migrations/bootstrap/README.md`
- `migrations/bootstrap/manifest.json`
- `migrations/bootstrap/schema-contract.json`
- `tests/Integration/AuditBootstrapTests.cs`
- `tests/Integration/MembershipHttpSurface.cs`
- `tests/Integration/Mt2IdentityTests.cs`
- `tests/Integration/Mt3cHttpTests.cs`
- `tests/Integration/Mt3dHttpTests.cs`
- `tests/Integration/Mt4bHttpTests.cs`
- `tools/local_test_db.py`
- `tests/Integration/INSTITUTION_ADMIN_ACADEMIC_VERIFICATION.json`
# Fase 2 — email istituzionali

Il modulo opzionale aggiunge policy versionate per Fondazione e categoria, preview,
riserva indirizzi, creazione atomica di anagrafica e riserva, override/ritiro Admin e
audit append-only. Non modifica User.Email, login, mailbox o ruoli IAM. Staff usa
soltanto policy Student/Teacher e non modifica configurazioni o storico.
Contratti: [INSTITUTION_EMAIL_POLICIES](../api/INSTITUTION_EMAIL_POLICIES.md).
Evidenze e limiti: `tests/Integration/INSTITUTION_EMAIL_POLICY_REPORT.md`.

## Fase 3: importazione massiva CreateOnly

Nove endpoint istituzionali consentono template CSV, preview persistente privata, mapping, conferma AllOrNothing e risultati per Admin/Staff. CSV e XLSX usano parsing limitato; classi e referenze sono tenant-scoped. Nessun account, membership, enrollment, ClassSubject o trasferimento è creato. Riserva email opzionale tramite Fase2, senza cambio silenzioso dei candidati confermati. Actor-scoped history e retention della sola staging; audit append-only. Vedere [contratto bulk import](../api/INSTITUTION_BULK_IMPORT.md) e il report Integration dedicato.
