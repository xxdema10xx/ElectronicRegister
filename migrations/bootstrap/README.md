# Empty consolidated schema - MT-5D

Schema source, extracted from verified SHOW CREATE metadata. No dump or tenant row data.
The contract preserves 52 tables, 129 triggers, 158 FK, 218 indexes and every trigger
SQL_MODE/charset/collation/DEFINER/ACTION_ORDER context. No historical migration hash
is rewritten. AUTO_INCREMENT watermarks are omitted; column attributes remain.

Use tools/setup-local-test-db.ps1 on a new attested isolated test instance. Never
replay historical expand scripts afterward. The driver creates tables, adds FK with
checks enabled and creates triggers in their original order/context, then validates.
Only a technical Provisioning holding is inserted at runtime, with no User/admin.
The email module additionally initializes its required database mutex singleton id=1;
this is coordination data, not a tenant, account or historical event.
See [local setup](../../docs/development/LOCAL_DATABASE_SETUP.md).

Institutional email 20261027.2 adds five tables and eighteen triggers, including
global User/address namespace coordination. See ../institution-email/README.md.
Never replay the additive migration after this bootstrap. Generating or reserving
an address does not create a mailbox or change User.Email or authentication.

Institutional bulk import 20261027.3 adds four tables, twelve triggers, thirteen FK
and nineteen indexes for private staged previews, immutable created target links,
stable external references and append-only import events. See
../institution-import/README.md. Never replay its expansion after this bootstrap.
The retention service clears only expired preview payloads; import history remains.

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

Institution Staff 20261027.1 appends Staff to the institutional role ENUM without
changing existing SQL ordinals or User.role. Invitation/audit role catalogs support
four roles; targeted Staff grade/exam audit protection adds one trigger (98 → 99).
See ../institution-staff/README.md for new disposable-instance verification and the
guarded additive migration. Fresh bootstrap already includes it: never replay its DDL.
