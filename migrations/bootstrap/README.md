# Empty consolidated schema - MT-5D

DDL-only source, extracted from verified SHOW CREATE metadata. No dump or row data.
The contract preserves 40 tables, 88 triggers, 118 FK, 164 indexes and every trigger
SQL_MODE/charset/collation/DEFINER/ACTION_ORDER context. No historical migration hash
is rewritten. AUTO_INCREMENT watermarks are omitted; column attributes remain.

Use tools/setup-local-test-db.ps1 on a new attested isolated test instance. Never
replay historical expand scripts afterward. The driver creates tables, adds FK with
checks enabled and creates triggers in their original order/context, then validates.
Only a technical Provisioning holding is inserted at runtime, with no User/admin.
See [local setup](../../docs/development/LOCAL_DATABASE_SETUP.md).
