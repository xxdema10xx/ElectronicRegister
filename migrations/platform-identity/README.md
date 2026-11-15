# Platform identity — additive successor er-platform-identity-1

Target: **MySQL 8.4.13** only. Predecessor: release `er-quality-2-mysql84` (71 tables, 180 triggers,
200 FK, 303 indexes). Target: 72 tables, 186 triggers, 201 FK, 308 indexes. Design:
[PLATFORM_IDENTITY_AND_LIFECYCLE.md](../../docs/architecture/PLATFORM_IDENTITY_AND_LIFECYCLE.md).

## What it adds

- `user`: `fiscal_code_hmac` (UNIQUE), `fiscal_code_hmac_key_version`, `fiscal_code_enc`, `fiscal_code_enc_key_version`,
  `lifecycle_state` (`Active` default / `Closed`), `closed_at_utc`, `reactivated_at_utc`. All nullable except the
  state; no column is removed or rewritten and no fiscal code, event or actor is backfilled.
- `identity_lifecycle_event`: append-only ledger (IDs, transition, rule code/version, security generation, UTC).
- Triggers: `platform_user_insert` (shape), `platform_user_update` (email and fiscal code immutable, allowed
  state transitions), `platform_user_delete` (no physical deletion), `lifecycle_event_insert/update/delete`.
- Marker `er-platform-identity-1` in `er_schema_release`, written **last**.

Existing triggers and FKs are unchanged. With `user.email` immutable and `user` rows undeletable,
`email_user_update` can no longer release an address and `fk_email_reservation_user ON DELETE CASCADE`
(which does not fire triggers) can no longer run: every issued address stays reserved forever.

The user guards contain the same attested-instance predicate as the `mt5c_*` purge guards. In ordinary
bundles it requires `VERSION()='5.7.24'` and is never true on 8.4. Only `tools/mysql84-fixture.cjs` binds it,
for test containers created and attested by the test process, to `@er_mt5b_fixture_capability`; the user must
be listed in `@er_mt5b_fixture_owners` and have an `…example.test` address.

## Rules

- Apply once, after er-quality-2-mysql84, with the migration identity. Never replay: a second run fails the
  predecessor check. DDL implicitly commits; there is no rollback, automatic restore or retry. Preserve a failed
  instance, its logs and the backup for review.
- Never `SET GLOBAL`, never disable FK or trigger checks, never touch 3306, 3307, 13306 or `mysql84-local`.
- `002_validate.sql` is read-only: all 13 violation counts must be zero.
- `predecessor-contract.json` records the predecessor (markers, inventory, `user` columns, hash of
  `migrations/mysql84-remediation/contract.json`) and the target (`SHOW CREATE TABLE` of `user` and
  `identity_lifecycle_event` captured on 8.4.13, trigger metadata and unbound statement hashes).

## Release closure

The files are part of the 8.4 release closure (`tools/mysql84-release.cjs`, roots include
`migrations/platform-identity`). The er-quality-2 contract is not modified. After reviewing a change:

```powershell
node tools/mysql84-release.cjs              # must fail with "New release closure drift" only for reviewed files
node tools/mysql84-release.cjs --write      # deliberate regeneration (BACKEND_REVIEW.md §8)
node tools/mysql84-release.cjs
node tools/test-mysql84-release.cjs
```

`node tools/mysql84-release.cjs successor <binding.json> <NEW-dir>` writes the DEFINER-bound SQL
(`01-platform-identity.sql` and `binding.json`) without executing anything. The integration fixture
(`tests/Integration/Mysql84FixtureTarget.cs`) asks the bundle for the successor (`successor: true`, fixture-only).

## Applying to a disposable container

Only a NEW container created for this purpose, labelled `er.platform-identity.disposable=1`, from
`mysql:8.4.13`, bound to a loopback port other than 3306/3307/13306, already carrying er-quality-2-mysql84:

```powershell
# Binding: {"database":"electronic_register_test","definerUser":"<definer>","definerHost":"<host>"}
$env:MYSQL_PWD = '<disposable root password>'   # never on the command line
.\tools\Invoke-PlatformIdentityMigration.ps1 -ContainerName <name> -Port <port> -BindingConfig <binding.json> -WritersStopped
Remove-Item Env:MYSQL_PWD
```

The runner checks label, image, port binding, version, quietness, markers, inventory and `user` columns;
takes a SHA-256 hashed `mysqldump` into a new `.staff-private/platform-identity-migration-*` directory; binds and
applies the SQL once; then compares markers, inventory, columns, both table definitions, trigger metadata and
statement hashes, and runs `002_validate.sql`.

## Database First scaffold

`Scaffold-PlatformIdentity.ps1` scaffolds a **candidate** of `user` and `identity_lifecycle_event` from a
disposable container into a new directory. Reviewed result: `Entities/IdentityLifecycleEvent.cs` is committed
unchanged and listed in `tools/CodeOrganization/generated-sources.json` (41 files); `User.cs` is intentionally
kept without the new columns (see decision A3), and the sidecar `ElectronicRegisterContext.PlatformIdentity.cs`
configures the new entity.

## Limits

Not applied to existing staging/production deployments by any automated path: an existing er-quality-2 deployment needs a
reviewed execution of this successor with its own migration identity. The only automated application is on the NEW,
disposable database of a staging prepared with `host.py prepare --enable-platform-identity` (EC2 release rehearsal, CI
flows): `host.py successor` runs after the er-quality-2 certification, never on a database that already carries the
marker, and checks `002_validate.sql` (13/13 at zero). `tools/release-preflight.cjs` accepts the successor only through the
reviewed delta of `predecessor-contract.json` (user DDL, ledger table, six triggers). Key rotation, fiscal code backfill for
existing users and any change to the immutable columns require a new reviewed migration.
