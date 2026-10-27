# Institutional email — additive migration 20261027.2

Prerequisite: the locally developed Staff schema (43 tables, 99 triggers, 131 FK, 180 indexes). `predecessor-contract.json` preserves its actual definitions; it is not reconstructed from the older Git HEAD, whose Staff changes are absent. Target: 48 tables, 117 triggers, 145 FK, 199 indexes, MySQL 5.7.24, utf8mb4_unicode_ci. No legacy role ordinals or User email values are changed.

`001_expand.sql` adds five tables, eighteen triggers and a technical singleton, then registers existing User addresses in the global namespace. This current-state backfill creates no historical events. All User email writers subsequently participate through INSERT/UPDATE triggers and a real unique key. Existing institutional addresses are never automatically released. `002_validate.sql` is read-only; all ten violation counts must be zero.

Do not apply raw DDL to an unidentified instance. Stop writers and ensure maintenance remains enforced throughout preflight, backup, DDL and postflight. MySQL DDL implicitly commits: neither transaction rollback nor automatic replay/recovery is promised. Preserve a failed datadir, logs and dump for review. Never disable FK or trigger enforcement to complete a migration.

The controlled PowerShell runner supports **only new disposable local instances**, rooted beneath `.staff-private`, on ports other than 3306/13306. It requires explicit port/datadir, checks version/UUID/quietness and every predecessor table/trigger definition, creates a new evidence directory and SHA256 dump, applies once, then compares the full final contract and ten integrity checks. A second execution fails predecessor validation. It does not install software, start/stop an existing server, reset data or modify execution policy.

```powershell
# Use the password belonging to the new, attested instance; never a production credential.
$env:MYSQL_PWD = [IO.File]::ReadAllText('PATH-TO-NEW-PRIVATE-INSTANCE/password.txt')
.\tools\Invoke-InstitutionEmailMigration.ps1 -MySqlBin C:\MAMP\bin\mysql\bin `
  -Port NEW-TEST-PORT -ExpectedDataDirectory 'PATH-TO-NEW-PRIVATE-INSTANCE/data' -WritersStopped
Remove-Item Env:MYSQL_PWD
```

If Windows execution policy or sandbox permissions reject a command, preserve the exact error and do not weaken the restriction. The report distinguishes native SQL operations actually executed by the agent from this runner's execution status.

The consolidated bootstrap contains the same additive SQL after the previous schema. The singleton id=1 is schema coordination data, not a tenant/account. Bootstrap contains no institution, membership or User seed. The migration requires the existing `er_test@localhost` trigger definer and is run with the attested isolated root account. New triggers inherit the migration actor definer; the recorded contract is for this controlled root execution. A different production execution identity requires a separately reviewed deployment/definer contract and is outside this local-only runner.

Offline validation, from the repository root:

```powershell
$py = 'C:\Users\Lorenzo\AppData\Local\Python\pythoncore-3.14-64\python.exe'
& $py -m unittest discover -s tools -p 'test_*.py'
& $py tools/verify-project-artifacts.py
& $py tools/verify-institution-staff.py
& $py tools/verify-institution-email.py
```

These offline commands do not replace real MySQL bootstrap/migration tests. Previous manually reported Python successes apply only to their previous inputs; the new validator and manifest require a new execution.
