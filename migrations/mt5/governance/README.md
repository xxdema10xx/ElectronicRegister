# MT-5D additive governance/recovery

Apply 001_expand.sql once only to the certified MT-5C predecessor on the attested
isolated test server, after verified backup, source hashes and writer checks.
It adds institution_recovery, institution_recovery_approval and eight triggers.
It does not modify or replace existing objects. MySQL DDL commits implicitly;
stop on any error, retain partial state, never replay or restore automatically.
002_validate.sql is read-only: five rules must return zero. manifest.json hashes
freeze the applied SQL. Runtime authorization, signatures and human attestations
remain mandatory application contracts; SQL counts alone cannot certify them.

A new clone uses migrations/bootstrap instead: this migration is already consolidated.
See docs/architecture/INSTITUTION_GOVERNANCE_AND_RECOVERY.md and the implementation report.
