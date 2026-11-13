# MT5B — persistence only

MySQL 5.7.24 MAMP isolated on 127.0.0.1:13306, electronic_register_test.
No JWT cutover, IAM ledger, HTTP mutation, invitation or onboarding is included.
No existing MT1/MT3/MT4 canonical SQL is changed. No automatic restore or DROP.

## Installation order

Quiesce all membership/role writers for the complete maintenance window. DDL commits
implicitly; a failed step leaves its partial state for diagnosis. Do not rerun EXPAND
or initialize/reset the database. The isolated server is already provisioned; these
scripts never create a datadir, database, account or service.

1. Verify process/listener, VERSION/hostname/port/datadir/DATABASE/GLOBAL mode.
2. Run 000_preflight.sql and the MT4A/MT4C validators, requiring zero violations.
3. Acquire a full private backup, verify table/trigger definitions, footer and SHA256.
4. 001_security_state_expand.sql creates the state table and ten core triggers.
5. 002_security_state_backfill.sql initializes only missing rows with generation0.
6. 003_security_state_validate.sql requires six zero results.
7. 004_membership_delete_error_contract.sql adds the FK-style1451 error guard,
   preserving existing callers without replacing/dropping the retained core guard.
8. 005_delete_contract_validate.sql requires two zero results (presence and order).
9. Verify33tables/64triggers/99FK/141indexes and all previous definitions/orphans.

EXPAND before backfill fails closed if a writer nevertheless encounters an old
membership without a state row. Newly inserted memberships initialize their state
in the same transaction. A missing state is never interpreted as generation0 by
the repository. Backfill is initial installation, **not** automatic repair after
future generation-aware JWT activation. Stop and investigate a missing state then.
Existing nonzero generations are never overwritten; no historical IAM events are made.

`Run-Mt5bPersistence.ps1` is a guarded runner; default mode is read-only Validate:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File migrations/mt5/Run-Mt5bPersistence.ps1 `
 -ClientConfig migrations/mt4/private/mt5a-mamp-13306/client.ini -Mode Validate
```

For a separately authorized fresh MT4C installation, use Mode Apply with a new
BackupPath under the existing private, ACL-protected isolated-instance directory.
Apply refuses an already installed state table. Host/port/database are explicit on
every client invocation; credentials come from ClientConfig, never a CLI password.
The current workspace has already been migrated: **do not run Apply again here**.
The canonical SQL is data-independent and initializes existing memberships too;
the runner deliberately allows only the currently authorized isolated server.
Operational deployment elsewhere requires its own reviewed preflight/authorization.

## Security contract

One state row per immutable membership, composite owner FK RESTRICT, globally unique
membership_id in the state table. Creation=0; each effective role INSERT/DELETE and
status change increments1 in the same InnoDB transaction. No-op/duplicate/rollback
does not advance the persisted counter. Role rekey UPDATE is rejected; use explicit
DELETE/INSERT (transactional if replacing a set). Identity changes are rejected.

Generation cannot decrease. Privileged direct SQL may advance it, which only revokes
additional access; there is no application setter/API. Normal trigger increments
are guarded at Int64.MaxValue and fail the original mutation rather than wrap/reset.
Sidecar UPDATE never writes institution_membership, including with INSERT SELECT and
DELETE JOIN that read that parent. Direct statements reading the state table while
also triggering its update may fail closed under MySQL restrictions; no admitted
writer uses that shape. They must not bypass the triggers to work around an error.

Ordinary membership DELETE and state DELETE are denied. Therefore the same ID cannot
be deleted/recreated to restore an old generation; this does not rely on GUID odds.
There is no operational delete API. Fixture teardown is an explicit **DBA test-only**
exception: root, DB/13306/exact isolated datadir suffix, enumerated owner IDs in a
session variable and GUID-suffixed synthetic Institution codes must all match.
The actual holding does not match. No HTTP/production code sets this variable.
This is not a defence against a DBA. Temporary rehearsal holdings are relabelled
only at teardown using their exact owned ID/name, never the real configured holding.

## Technical versus interactive writers

IInstitutionMembershipRepository is technical provisioning persistence, not an IAM
admin manager. All three writers lock Institution first and require Provisioning;
Active/Suspended/Archived are denied, independent of User.Role. No HTTP consumer
exists. The offline MT1/backfill tools remain manifest-driven test/migration tooling,
not registered in Program, with their existing explicit test-DB/holding checks and
maintenance requirement. Fixture SQL is privileged setup/fault injection/teardown,
not a human-admin mutation and not a fabricated ActorUserId.

Interactive writers are still absent. Their future protocol must use Institution
FOR UPDATE first, revalidate caller/version, lock memberships deterministically,
enforce self/LastAdmin rules, mutate, append attributed IAM event and commit. The
database owns generation increments: do not also increment in EF/the manager.

## Database-first reproduction

After migration and a successful build, with TEST_MYSQL_CONNECTION privately set for
the child process, `Scaffold-Mt5b.ps1 -Candidate <new-private-directory>` scaffolds
only institution_membership_security_state via Name=ConnectionStrings:Mt5bScaffold.
It never overwrites a candidate or copies generated legacy User/context. The checked
entity was copied from this scaffold. The context sidecar adds the composite FK
explicitly because single-table scaffolding excludes the unselected principal;
it introduces no navigations. Compare candidate/hash before any future copy.

See the persistence report for real SQL proof, regression history and remaining JWT,
audit, idempotency and LastAdmin work. This phase does not complete MT5B.
