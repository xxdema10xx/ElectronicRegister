# Institutional bulk import — 20261027.3

Additive MySQL 5.7.24 migration from the exact Phase2 predecessor (48 tables,117 triggers,145 FK,199 indexes) to 52 tables,129 triggers,158 FK,218 indexes. `predecessor-contract.json` preserves Phase2 definitions; all existing tables and triggers must remain identical. No old role ordinal, User.Email, academic entity or historical record is rewritten.

`001_expand.sql` adds four structures:

- `institution_import_batch`: tenant/actor ownership, immutable content digest/type/expiry, optimistic revision, bounded private JSON staging, metadata and durable result. Staging is cleared on completion or expiry.
- `institution_import_item`: immutable created target IDs, typed composite Student/Teacher ownership FKs, batch/category FK, optional email assignment and external reference FKs.
- `institution_import_external_reference`: case-sensitive stable keys scoped by tenant and category, backed by typed person FKs; no generic EntityType/EntityId. References cannot be reused by deleting history.
- `institution_import_event`: per-tenant EventId, request digest, actor User/Membership or explicit Retention service identity, revision and metadata/result without source rows. Append-only.

Twelve triggers enforce valid states, immutable fields, target shapes, current authorized batch actors for interactive transitions and retained history. Thirteen new FKs and nineteen new indexes are part of SHOW CREATE contracts. SQL CHECK is not used. `002_validate.sql` returns ten violation counts, all required zero. The consolidated bootstrap contains the exact migration; never replay after bootstrap.

DDL implicitly commits. Stop writers and keep maintenance enforced throughout preflight, dump, DDL and postflight. Do not promise rollback through SQL transactions; preserve partial datadir/logs/dump on failure and never automatically repeat expansion. The application does not migrate on startup.

The controlled runner only accepts new isolated local test targets beneath `.staff-private`, explicit port/datadir, MySQL5.7.24 and no active writers. Ports3306/13306 are refused. It verifies every predecessor table/trigger, writes a new private dump with SHA256, applies once and compares the complete final contract plus ten SQL checks. Root is the attested migration account; new trigger definers inherit it. Production execution/definer review is separate and no production migration was performed here.

```powershell
$env:MYSQL_PWD = [IO.File]::ReadAllText('NEW-PRIVATE-INSTANCE/password.txt')
.\tools\Invoke-InstitutionImportMigration.ps1 -MySqlBin C:\MAMP\bin\mysql\bin `
  -Port NEW-TEST-PORT -ExpectedDataDirectory 'NEW-PRIVATE-INSTANCE/data' -WritersStopped
Remove-Item Env:MYSQL_PWD
```

Do not change execution policy to run this script. Actual native SQL migration and bootstrap execution are recorded separately in the report; syntax inspection is not execution of the runner.

Transaction order is institution first, current membership/roles/security generation, then existing email namespace mutex/person writes when requested. One transaction spans all created people, external references, email assignments, audit, result and completion. Failure leaves the previously committed preview intact and creates no person/reservation/import item. No FK or audit trigger is disabled for the tests.

The retention service only clears expired private preview payloads and appends explicit Retention events with null user actors; it does not impersonate the preview creator or delete metadata/history. Access expires immediately; physical cleanup runs every minute in bounded passes of20. The per-actor pending-payload quota prevents unbounded accumulation while cleanup catches up. Cleanup detects whether the schema exists and also operates when ingestion is disabled. See the API guide for configuration and privacy behavior.

Offline host commands after every artifact update:

```powershell
$py='C:\Users\Lorenzo\AppData\Local\Python\pythoncore-3.14-64\python.exe'
& $py -m unittest discover -s tools -p 'test_*.py'
& $py tools/verify-project-artifacts.py
& $py tools/verify-institution-staff.py
& $py tools/verify-institution-email.py
& $py tools/verify-institution-import.py
```

Previous host successes on159 Phase2 hashes are historical, not results for this updated manifest. The report distinguishes host restore of Open XML SDK from actual compilation, real database tests and Python execution.
