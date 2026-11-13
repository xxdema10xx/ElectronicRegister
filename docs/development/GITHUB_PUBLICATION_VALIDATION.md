# GitHub publication validation — 2026-10-22

Branch: `refactor-database-entities`. Initial local HEAD and independently checked
GitHub branch: `bded97b06b7957d73bcfcaafc674aad62037c5e2` (no divergence).
This validation preserves the three completed implementations. No application
feature, test identity, assertion or rollout flag was changed.

## Python and portability

An existing, complete Python **3.13.0** runtime was located in MySQL Workbench's
MySQL Shell distribution. No installation, PATH edit or application dependency
change was needed. The scripts require only the Python standard library.
The top-level Workbench Python executable lacks the required standard library
and was not used for validation.

Executed successfully:

- `tools/verify-project-artifacts.py`: all **138 SHA256** values, EOL rules and
  versionable bootstrap closure verified.
- `python -m unittest discover -s tools -p 'test_*.py' -v`: **11/11 passed**.
- `tools/package-versioning-candidates.py <new-private-destination>`: initial
  **637** source candidates copied, with per-file SHA256, excluding private data,
  credentials, build outputs, backups and operational logs.
- The copied `tools/local_test_db.py --mysql-bin <MySQL57-bin> --port 23316
  --create-instance --confirm-isolated-test` completed the actual project
  bootstrap, verified the schema before/after technical runtime provisioning,
  and passed a separate `--verify-only` run.

The test target was a new MySQL **5.7.24** process, new datadir and verified free
loopback port **23316**. The project validator attested executable, PID, datadir,
server UUID, binding, private accounts and SQL mode. It recorded an empty-target
journal before the one-time bootstrap; no user data existed to back up. Existing
bootstrap attempts are never replayed. No reset, restore, disabled FK/trigger,
GLOBAL sql_mode change or private dump was used.

The regression suite explicitly asserts port 13306. That port was verified free;
a **second newly initialized datadir/process** was created for this suite, with
its own creation attestation. No preexisting 13306 instance/datadir was reused,
modified, reset, restored or stopped. **Port 3306 was never connected to or
modified.** Both new datadirs live under an ignored publication evidence folder.

## Verified schema

| Object | Count |
| --- | ---: |
| Tables | 42 |
| Triggers | 94 |
| Foreign keys | 122 |
| Indexes | 170 |

Every table definition, column, index and composite FK matches the schema
contract. All trigger bodies, execution order, definers, character set and SQL
mode match. All 122 FK orphan checks are zero. Exam presentation, class audit,
institution administration and historical versioned validators pass. Bootstrap,
schema contract, additive migration sources and setup tools are covered by the
138-entry manifest; historical migrations were not replayed after bootstrap.

## Build and regression

- Full solution build, `--no-restore --no-incremental`: **success**, zero errors,
  11 existing nullable warnings.
- Unit tests: **103/103 passed**; integration tests: **1027/1027 passed**.
- Total: **1130/1130 passed**, zero failures, skips or unexecuted outcomes.
- Enumerated test identities exactly match the initial 1130 baseline: none added,
  deleted, renamed or disabled. All C# source bytes match the start of this task.
- Real Program/JWT/MySQL integration and strict session mode were used. Raw TRX,
  generated test artifacts and credentials remain private and are not published.
- Generated tracked TestResults were restored to their exact initial bytes,
  preserving preexisting local state.

## Publication inventory and constraints

All **116** files below are explicit reviewed project candidates. Required SQL
and tooling are not ignored. `git diff --check` passes. Scans found no private
keys, access tokens or occurrence of the private database password. No datadir,
backup, raw TRX, dump, operational log or private connection string is included.

A single coherent commit was planned because the final bootstrap, manifest, EF
mappings and DI registrations depend on all three implementations. Separating
those shared final artifacts would produce intermediate inconsistent commits.

The workspace's root `.git` is read-only and shell Git HTTPS is unavailable.
The authorized publication plan used the GitHub connector to create the reviewed
Git tree/commit, then fast-forward the branch with `force=false` and an exact
expected-head lease. The remote branch was independently checked again and had
not advanced. Each of the 116 canonical candidate blobs matched Git's own
clean-filter hash; no required candidate is ignored.

**Publication blocked by tool permissions.** The first write operation,
`github_create_tree`, was rejected before execution with:
`MCP tool call requires approval, but approval policy is never`.
No tree, commit or branch update was created by this publication attempt. No
commit SHA or successful push is claimed. Local HEAD/index/tracking refs were
not mutated. All 116 reviewed project files remain local pending publication.
The final independent remote-reference check is supplied in the delivery response.

The final public-only package contains **638 files**. Its validator passed against
both fresh instances after regression, including all schema and orphan checks.
Both owned processes were then cleanly shut down after exact executable, PID,
UUID and datadir attestation. Their private datadirs and raw evidence are retained
and ignored. Existing user instances and datadirs were preserved.

Project-file inventory (modified or newly added):

- `.gitattributes`
- `.gitignore`
- `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionAcademicHttp.cs`
- `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionBienniumController.cs`
- `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionBienniumStudyAreaController.cs`
- `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionBienniumStudyPathController.cs`
- `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionCampusController.cs`
- `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionClassController.cs`
- `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionClassSubjectController.cs`
- `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionStudyAreaController.cs`
- `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionStudyPathController.cs`
- `Backend/ElectronicRegisterAPI/src/Application/ElectronicRegisterAPI.Application/DependencyInjection/AddApplicationManagers.cs`
- `Backend/ElectronicRegisterAPI/src/Application/ElectronicRegisterAPI.Application/Managers/ExamManager.cs`
- `Backend/ElectronicRegisterAPI/src/Application/ElectronicRegisterAPI.Application/Managers/InstitutionAcademicCatalogManager.cs`
- `Backend/ElectronicRegisterAPI/src/Application/ElectronicRegisterAPI.Application/Managers/InstitutionCampusManager.cs`
- `Backend/ElectronicRegisterAPI/src/Application/ElectronicRegisterAPI.Application/Managers/InstitutionClassManager.cs`
- `Backend/ElectronicRegisterAPI/src/Application/ElectronicRegisterAPI.Application/Managers/InstitutionClassSubjectManager.cs`
- `Backend/ElectronicRegisterAPI/src/Application/ElectronicRegisterAPI.Application/Managers/InstitutionSubjectManager.cs`
- `Backend/ElectronicRegisterAPI/src/Application/ElectronicRegisterAPI.Application/Mappers/ExamCreationMapper.cs`
- `Backend/ElectronicRegisterAPI/src/Application/ElectronicRegisterAPI.Application/Mappers/ExamStudentMapper.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/DTOs/CreateExamDto.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/DTOs/InstitutionAcademicCatalogDtos.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/DTOs/InstitutionCampusDtos.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/DTOs/InstitutionClassDtos.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/DTOs/InstitutionClassSubjectDtos.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/DTOs/UpdateExamDto.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/Interfaces/Managers/IInstitutionAcademicCatalogManager.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/Interfaces/Managers/IInstitutionCampusManager.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/Interfaces/Managers/IInstitutionClassManager.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/Interfaces/Managers/IInstitutionClassSubjectManager.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/Interfaces/Repositories/IInstitutionAcademicCatalogRepository.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/Interfaces/Repositories/IInstitutionCampusRepository.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/Interfaces/Repositories/IInstitutionClassRepository.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/Interfaces/Repositories/IInstitutionClassSubjectRepository.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/Models/ClassSubject.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/Models/ExamSubmission.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/Models/InstitutionAcademicRules.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/Models/InstitutionCampusRules.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/Models/InstitutionClassRules.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/Models/StudentExamPresentation.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/ElectronicRegisterContext.InstitutionAdministration.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/ElectronicRegisterContext.InstitutionClasses.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/ElectronicRegisterContext.Multitenancy.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/ElectronicRegisterContext.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/Entities/ExamSubmission.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/Entities/InstitutionAdministrationEvent.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/Entities/InstitutionClassEvent.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/Migrations/InstitutionAdministrationIntegrity.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/Migrations/InstitutionClassIntegrity.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/Migrations/Mt3aOwnershipMigration.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Repositories/ClassSubjectRepository.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Repositories/ExamRepository.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Repositories/ExamSubmissionRepository.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Repositories/InstitutionAcademicCatalogRepository.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Repositories/InstitutionAdministrationPersistence.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Repositories/InstitutionCampusRepository.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Repositories/InstitutionClassRepository.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Repositories/InstitutionClassSubjectRepository.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Repositories/InstitutionExamDataScope.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Repositories/InstitutionExamRepositoryFactory.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Repositories/InstitutionSubjectRepository.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Repositories/SubjectRepository.cs`
- `docs/api/INSTITUTION_ACADEMIC_MANAGEMENT.md`
- `docs/api/INSTITUTION_CLASSES.md`
- `docs/development/GITHUB_PUBLICATION_VALIDATION.md`
- `docs/development/INSTITUTION_ADMIN_COVERAGE_REPORT.md`
- `docs/development/LOCAL_DATABASE_SETUP.md`
- `migrations/bootstrap/001_schema.sql`
- `migrations/bootstrap/README.md`
- `migrations/bootstrap/manifest.json`
- `migrations/bootstrap/schema-contract.json`
- `migrations/exam-presentation/001_expand.sql`
- `migrations/exam-presentation/002_validate.sql`
- `migrations/exam-presentation/README.md`
- `migrations/institution-administration/001_expand.sql`
- `migrations/institution-administration/002_validate.sql`
- `migrations/institution-administration/README.md`
- `migrations/institution-class/001_expand.sql`
- `migrations/institution-class/002_validate.sql`
- `migrations/institution-class/README.md`
- `tests/Integration/AuditBootstrapTests.cs`
- `tests/Integration/EXAM_NAME_AND_RANDOMIZATION_REPORT.md`
- `tests/Integration/EXAM_NAME_AND_RANDOMIZATION_VERIFICATION.json`
- `tests/Integration/ExamPresentationTests.cs`
- `tests/Integration/INSTITUTION_ACADEMIC_CATALOG_CRUD_REPORT.md`
- `tests/Integration/INSTITUTION_ADMIN_ACADEMIC_VERIFICATION.json`
- `tests/Integration/INSTITUTION_CAMPUS_CRUD_REPORT.md`
- `tests/Integration/INSTITUTION_CLASS_CRUD_REPORT.md`
- `tests/Integration/INSTITUTION_CLASS_CRUD_VERIFICATION.json`
- `tests/Integration/INSTITUTION_CLASS_SUBJECT_CRUD_REPORT.md`
- `tests/Integration/InstitutionAcademicAuditFaultTests.cs`
- `tests/Integration/InstitutionAcademicCatalogTests.cs`
- `tests/Integration/InstitutionAcademicManagementSecurityTests.cs`
- `tests/Integration/InstitutionAdministrationDependencyRaceTests.cs`
- `tests/Integration/InstitutionCampusAuditFaultTests.cs`
- `tests/Integration/InstitutionCampusCrudTests.cs`
- `tests/Integration/InstitutionClassCrudFixture.cs`
- `tests/Integration/InstitutionClassCrudSecurityTests.cs`
- `tests/Integration/InstitutionClassCrudTests.cs`
- `tests/Integration/InstitutionClassSubjectAuditFaultTests.cs`
- `tests/Integration/InstitutionClassSubjectCrudTests.cs`
- `tests/Integration/MembershipHttpSurface.cs`
- `tests/Integration/Mt2IdentityTests.cs`
- `tests/Integration/Mt3cHttpTests.cs`
- `tests/Integration/Mt3dHttpTests.cs`
- `tests/Integration/Mt4bHttpTests.cs`
- `tests/Integration/Phase4_ExamValidationAndAtomicityTests.cs`
- `tests/Unit/InstitutionAcademicRulesTests.cs`
- `tests/Unit/InstitutionCampusRulesTests.cs`
- `tests/Unit/InstitutionClassRulesTests.cs`
- `tests/Unit/StudentExamPresentationTests.cs`
- `tools/Invoke-ExamPresentationMigration.ps1`
- `tools/Invoke-InstitutionAdministrationMigration.ps1`
- `tools/Invoke-InstitutionClassMigration.ps1`
- `tools/local_test_db.py`
