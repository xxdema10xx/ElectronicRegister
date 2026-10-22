# Institution ClassSubject CRUD — 2026-10-22

Five routes under `/api/institution/class-subjects`: GET list/detail, POST, PUT
and DELETE. Dedicated DTOs reject unknown/server/structural fields. Create accepts
ClassId, SubjectId and TeacherId; update accepts TeacherId alone with mandatory
If-Match. Server IDs and tenant ownership are authoritative. Controller, manager,
repository and interfaces follow the existing layer direction and scoped DI.

The existing teacher list remains available, with the previous array and choice
fields (ClassSubjectId included), plus Id/Revision, SQL paging and total header.
Class/subject/teacher filters execute in SQL. Teacher visibility uses verified
membership/profile; detail and all writes require current InstitutionAdmin.
The existing SchoolApi + ExamApi rollout policy and LegacyEndpointGate apply.

ExamRepository and InstitutionExamDataScope additionally perform a current scoped
ClassSubject locking read at the existing exam write boundary. This closes the
race between a teacher's initial authorization and an administrative reassignment
or deletion. It does not change the exam engine, template, assignment or grading.

Schema before/after this phase: 42 tables, 94 triggers, 122 FK, 170 indexes.
No additional DDL in this phase. The shared institution_administration_event
ledger introduced in phase A records mutations atomically and retains history.
Tenant-first IAM authorization locks and resource row locks serialize writes and
revocation. Unique class/subject and composite owner FK remain the final SQL
protection. Stale revisions and duplicates return 409; missing If-Match returns
428; foreign/missing rows return identical 404.

Actual incoming FK are Exam and Grade (legacy and composite owner forms).
Current locking reads of all FK dependencies block reassignment and deletion;
Exam also protects all reachable submissions/results. No cascade removal or
implicit author change is introduced. Existing exam engine, subject-derived names,
persisted student permutations, IDs, grading and deadlines remain unchanged.

New files: InstitutionClassSubjectDtos, IInstitutionClassSubjectManager,
IInstitutionClassSubjectRepository, InstitutionClassSubjectManager,
InstitutionClassSubjectRepository, InstitutionClassSubjectCrudTests. The existing
controller, scoped DI, frontend contract and route inventory expectations change.
Inventory assertions still validate exact counts and manager/policy bindings.

Targeted real Program/JWT/MySQL run: **14 passed, zero failures/skips**. Tests cover
CRUD/revisions/audit, all three foreign references, unknown/immutable fields,
concurrent duplicate creates and updates, role denial, SQL paging, owner isolation,
grade protection and teacher->exam->student->automatic-grade HTTP flow. The E2E
also verifies derived Exam.Name, persistent display JSON and no IsCorrect exposure.
Full phase and final regression results are in the overall coverage report.

All prior test identities remain. Only existing inventory expectations incompatible
with added endpoints/manager were updated; security assertions were preserved.
No user database, 3306 connection, commit or push. Rollout remains disabled by default.

## Final global verification

**1130/1130 passed** (103 unit, 1027 integration), zero failures and zero skips.
All 1034 prior identities preserved; 96 new outcomes. All three phases complete.
Both isolated migration and candidate-only fresh bootstrap match 42 tables, 94
triggers, 122 FK and 170 indexes; all versioned validator checks pass. 138 manifest
file hashes and Git/EOL portability verified. See the coverage report and sanitized
INSTITUTION_ADMIN_ACADEMIC_VERIFICATION.json for exact phase counts and files.
