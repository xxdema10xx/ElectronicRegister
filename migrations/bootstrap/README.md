# Empty consolidated schema - MT-5D

DDL-only source, extracted from verified SHOW CREATE metadata. No dump or row data.
The contract preserves 42 tables, 94 triggers, 122 FK, 170 indexes and every trigger
SQL_MODE/charset/collation/DEFINER/ACTION_ORDER context. No historical migration hash
is rewritten. AUTO_INCREMENT watermarks are omitted; column attributes remain.

Use tools/setup-local-test-db.ps1 on a new attested isolated test instance. Never
replay historical expand scripts afterward. The driver creates tables, adds FK with
checks enabled and creates triggers in their original order/context, then validates.
Only a technical Provisioning holding is inserted at runtime, with no User/admin.
See [local setup](../../docs/development/LOCAL_DATABASE_SETUP.md).

Exam presentation 20261022.1 adds nullable exam_submission.display_order_json.
New bootstrap includes it; do not replay migrations/exam-presentation/001_expand.sql.
See ../exam-presentation/README.md for existing-instance preflight and reconciliation.

Institution Class CRUD 20261022.2 adds the append-only class ledger and durable
revisions. Existing Class/edition semantics remain unchanged. See
../institution-class/README.md; never replay its expansion after fresh bootstrap.

Institution administration 20261022.3 adds the shared append-only resource ledger.
Business catalog, ClassSubject and Campus tables/FK retain their original semantics.
See ../institution-administration/README.md. Fresh bootstrap includes this ledger;
do not replay its expansion. Preserve all existing user databases, including 13306.
