# Institution Class CRUD — 20261022.2

The existing Class table is unchanged. A dedicated `institution_class_event` ledger
is necessary because course_administration_event requires a real CourseEdition and
audit_event's classification trigger supports only exam/exam_submission/platform.
No fictitious edition is introduced.

The ledger records owner, actor user and membership, class snapshot ID, action,
revision, before/after JSON and UTC timestamp. Owner and actor have restrictive FKs.
ClassId is deliberately a historical snapshot ID without a Class FK, so deletion of
an empty class cannot erase or invalidate its audit. UPDATE and DELETE are rejected
by unconditional append-only triggers, including for root. The insertion trigger
checks a current active InstitutionAdmin, owner and event shape.

The same ledger provides persistent optimistic revisions. The existing Class row
is locked before reading MAX(revision) or mutating it. The unique
(institution_id,class_id,revision) key and If-Match prevent lost updates and ABA.
Existing classes require no backfill and begin at revision zero. CourseEditionId
is neither reused as a revision nor mutated by this CRUD.

## SQL inventory for deletion

The real MySQL inventory contains five incoming constraints:

| Child | Constraint | ON DELETE |
| --- | --- | --- |
| student | fk_student_class | SET NULL |
| student | fk_mt3a_student_class | SET NULL |
| class_subject | fk_cs_class | CASCADE |
| class_subject | fk_mt3a_cs_class | CASCADE |
| course_administration_event | fk_mt4c_class | RESTRICT |

Class.CourseEditionId is also explicitly blocking. Existence of ClassSubject blocks
all its reachable exams, submissions and grades. Class lock precedes dependency
reads; FK parent locks serialize concurrent child insertion. Before deletion, a
closed allowlist is checked against information_schema: any missing or newly added
incoming FK causes a conflict until the code inventory is reviewed. No cascade or
SET NULL is used as an application removal strategy.

## Installation

New clone/new empty isolated instance: use the consolidated public bootstrap and
setup tools. It already includes exam presentation 20261022.1 and this ledger.
Never replay either expansion on that schema.

Existing isolated MySQL 5.7.24 instance: stop writers and run
`tools/Invoke-InstitutionClassMigration.ps1` with `-MySqlBin`, a NEW directory under
the workspace `.exam-private`, and `-Apply`. Password may be supplied as SecureString.
The script attests 127.0.0.1:13306, database and canonical isolated datadir suffix,
checks table absence and quiet connections/transactions, exports incoming FKs,
takes a full logical backup with SHA256, then applies the additive SQL once.
An existing table, including a partially completed expansion, is never replayed.
After any SQL failure stop and inspect private evidence; there is no automatic
reset, restore, trigger/FK disabling or GLOBAL sql_mode modification.

`002_validate.sql` must report zero for its first three checks and emits the full
incoming-FK inventory. Schema contract: 41 tables, 91 triggers, 120 FKs, 167 indexes.
The original 40 tables and 88 triggers retain their definitions and metadata.

Test fixtures with new immutable audit records remain in their disposable isolated
datadir, including their referenced users/memberships. No production append-only
exception or teardown bypass was added for these tests.
