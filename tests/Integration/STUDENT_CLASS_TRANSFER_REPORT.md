# Student class transfer verification

Baseline: `8167a7d7128e14f520c3f7fe80733c99891cd3b9`, branch `refactor-database-entities`.
Initial 1130 identities: 103 unit and 1027 integration. Existing four status-only
artifacts were preserved; no checkout, reset, clean, commit or push was performed.

## Implementation

Dedicated manager/repository contracts, strict DTOs, pure calendar policy and
validated Options are added across Domain → Application → Infrastructure → API.
POST student/{id}/transfer-class and GET student/{id}/class-history are active
InstitutionAdmin-only. School marker/policy and LegacyEndpointGate are retained;
POST additionally requires CourseApi. Rollout defaults remain disabled. See
[HTTP contract](../../docs/api/STUDENT_CLASS_TRANSFER.md).

Current Student.ClassId was inventoried across repositories, school visibility,
exam assignment validation, exam START, grade mapping and legacy CRUD. Institutional
PUT stays name-only and now explicitly rejects unmapped fields. Initial class
assignment at creation remains valid. No User, membership or profile mutation occurs.

## Policy and transaction

Default 3 calendar months, globally configurable (1–120), explicit Europe/Rome
zone, TimeProvider, inclusive DateOnly.AddMonths deadline. NULL official StartDate
or missing class-edition attribution conflicts. Different editions require both
deadlines; client backdating/future scheduling cannot bypass them. Configuration
is validated at host startup. This is administrative policy, not an assertion of
a statutory national limit. Future tenant overrides require a separate design.

One READ COMMITTED transaction uses tenant-first IAM locks and revalidates current
privileges, then locks Student before classes/editions/enrollments. Current reads
avoid stale REPEATABLE READ snapshots after waits. START acquires the same Student
lock before Exam. Same-edition movement changes only current class, student revision
and audit. Cross-edition movement withdraws the source (original enrollment date
retained), inserts a new enrolled relation and real enrollment events linked by
TransferEventId. Existing destination relations in every status conflict.

Persistent revision prevents two intentions from consuming the same state. The
receipt replay check precedes date, current class and revision validation, while
authorization is always renewed. Replay survives later events and policy changes;
a different actor/content conflicts. No memory lock, Redis or application cache
stores concurrency state.

## Career and exam preservation

Grade/exam/submission/question/answer/assignment identity and contents remain
unchanged. Whole-row educational digests prove no transfer-side rewrite, including
randomization JSON. Historical associations remain readable only through authorized
grades/submissions. Old unstarted assignments remain persisted but cannot START or
appear as current assigned exams. No new exams are assigned automatically.
PendingReview Open answers remain reviewable/finalizable by the original teacher.
Unsubmitted in_progress attempts before their deadline block movement; expired
attempts remain handled by the existing worker without transfer-side mutation.
The full dossier retains old grades; after a recorded cross-course movement,
statistics are limited to the current edition, without automatic credit recognition.
Untransferred legacy statistics retain their previous semantics.

## Schema and operational safety

| Object | Before | After |
| --- | ---: | ---: |
| Tables | 42 | 43 |
| Triggers | 94 | 98 |
| FK | 122 | 131 |
| Indexes | 170 | 180 |

Migration 20261022.4 adds student.transfer_revision (default 0) and
student_class_transfer_event, with nine tenant-aware RESTRICT FK and four triggers.
No historical event is fabricated. All 42 previous tables remain; 41 definitions
and all 94 previous triggers are byte-equivalent in the contract. Student changes
only by the additive column. The original bootstrap prefix is intact.
The ledger validates ownership, actor, classes/courses, enrollment decision, policy
and revisions, and rejects every UPDATE/DELETE. Historical parent deletion is
blocked by real FK. Class's fail-closed incoming-FK inventory is updated; MT3A
classification explicitly validates the new ledger. EF partial mappings include
the real FK/index names.

The additive runner completed once on a newly initialized 23326 process after
exact-target preflight and verified full backup. A candidate-only fresh bootstrap
completed on a second new datadir/process on vacant 13306. No existing 13306
instance/datadir was reused. Port 3306 was never connected to or modified. No
reset/restore, FK/trigger disabling or GLOBAL sql_mode change occurred. Partial DDL
is never retried automatically; no partial DDL occurred in this run.

## Tests

Full build succeeds with zero errors and 11 preexisting nullable warnings.
**1229/1229 passed**: 133 unit, 1096 integration; zero failures/skips/unexecuted.
All **1130** prior identities preserved; **99** new outcomes (30 unit, 69 integration).
The initial diagnostic regression found ten obsolete route-count expectations;
only exact additive counts were updated (School 52→54, MVC 199→201 and derived
subsets). No security assertion was removed or weakened. The final regression
executes every previous identity, including Class, Enrollment, Grade, Exam and
expiry-worker tests.

New tests cover calendar month ends/leap years/DST, both movement types, missing
configuration/links, both course deadlines, enrollment conflicts, A/B ownership,
role/token/revocation/rollout checks, forbidden fields and payload size, history,
exact actor/content idempotence after later events, concurrent intentions/replays,
START competition, immutable educational data, old-start denial, historical
Open grading, course statistics and append-only audit. Faults after source update,
destination insert, enrollment audit, Student update and transfer audit each
roll back every enrollment/class/event mutation.

Python 3.13.0 executes the actual setup validator; 11 tooling tests pass. All 142
manifest entries verify, including new additive SQL/tooling. No package dependency
changed. Final live SQL/candidate verification and Git checks are recorded in the
paired machine-readable report; raw TRX, passwords, backup and datadirs stay ignored.

## Deliberate exclusions and limits

No cross-institution movement, historical rewriting, future/backdated operation,
credit recognition, enrollment reactivation/second episode, structural Class
course move, override or new frontend screen is implemented. Legacy classes without
real course attribution/dates require a separate reviewed administrative workflow.
Retention and lawful rectification/archiving need an approved policy; no legal
retention period is invented. The existing School/Course rollout flags still gate
operation. Deadlock/uniqueness conflicts return sanitized 409 and must be reviewed
with fresh state; the workflow never retries a different intention implicitly.

## Changed/new project files

- `.gitignore`
- `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Controllers/InstitutionStudentTransferController.cs`
- `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Program.cs`
- `Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/appsettings.example.json`
- `Backend/ElectronicRegisterAPI/src/Application/ElectronicRegisterAPI.Application/DependencyInjection/AddApplicationManagers.cs`
- `Backend/ElectronicRegisterAPI/src/Application/ElectronicRegisterAPI.Application/Managers/InstitutionStudentTransferManager.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/DTOs/StudentClassTransferDtos.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/DTOs/UpdateStudentDto.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/Interfaces/Managers/IInstitutionStudentTransferManager.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/Interfaces/Repositories/IInstitutionStudentTransferRepository.cs`
- `Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/Models/StudentClassTransferPolicy.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/ElectronicRegisterContext.Multitenancy.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/ElectronicRegisterContext.StudentTransfers.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/Entities/StudentClassTransferEvent.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/Migrations/Mt3aOwnershipMigration.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/Migrations/StudentClassTransferIntegrity.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Repositories/ExamRepository.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Repositories/GradeRepository.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Repositories/InstitutionClassRepository.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Repositories/InstitutionExamDataScope.cs`
- `Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Repositories/InstitutionStudentTransferRepository.cs`
- `docs/api/STUDENT_CLASS_TRANSFER.md`
- `docs/development/INSTITUTION_ADMIN_COVERAGE_REPORT.md`
- `docs/development/LOCAL_DATABASE_SETUP.md`
- `migrations/bootstrap/001_schema.sql`
- `migrations/bootstrap/README.md`
- `migrations/bootstrap/manifest.json`
- `migrations/bootstrap/schema-contract.json`
- `migrations/student-class-transfer/001_expand.sql`
- `migrations/student-class-transfer/002_validate.sql`
- `migrations/student-class-transfer/README.md`
- `tests/Integration/AuditBootstrapTests.cs`
- `tests/Integration/MembershipHttpSurface.cs`
- `tests/Integration/Mt3cHttpTests.cs`
- `tests/Integration/Mt3dHttpTests.cs`
- `tests/Integration/Mt4bHttpTests.cs`
- `tests/Integration/STUDENT_CLASS_TRANSFER_REPORT.md`
- `tests/Integration/STUDENT_CLASS_TRANSFER_VERIFICATION.json`
- `tests/Integration/StudentClassTransferAtomicityTests.cs`
- `tests/Integration/StudentClassTransferExamTests.cs`
- `tests/Integration/StudentClassTransferTests.cs`
- `tests/Unit/StudentClassTransferPolicyTests.cs`
- `tools/Invoke-StudentClassTransferMigration.ps1`
- `tools/local_test_db.py`

## Final SQL and portability evidence

The final export contains **657 public source candidates**. Its actual Python
bootstrap completed from an empty new datadir on 23327 and matched all 43 tables,
98 trigger definitions/contexts, 131 FK and 180 indexes. Both the additive-migration
target (23326) and full-regression target (13306) passed complete post-regression
verification, all 131 orphan checks and every versioned validator with zero
violations. All 142 SHA256 entries verify. The applied migration hash equals the
versioned SQL; its pre-DDL backup hash verifies and contains all 42 prior table DDL.

All three owned processes were cleanly shut down after exact executable/PID/UUID/
datadir verification. Private datadirs, backup and raw evidence are retained and
ignored. No existing user process was stopped. All 817 generated TestResults files
were restored to their exact initial bytes, including the initially modified
status-only artifact. No initial file outside the 45-file delivery inventory
changed. Git whitespace checks pass; no required file is ignored, no private key,
token or private database password was found in the candidates, and the index is
empty. HEAD remains 8167a7d7128e14f520c3f7fe80733c99891cd3b9. No commit or push.
