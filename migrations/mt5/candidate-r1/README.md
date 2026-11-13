# MT-5B-R1 — reconstructed SQL candidates

These files are new candidates, not recovered originals. The historical SHA256
manifest is unchanged. `manifest-r1.json` identifies this review package.

Do not copy these files next to the existing runner or execute them yet.
The current MT4C MAMP database on port 3306 must remain untouched.

The intended next checkpoint is an explicitly authorized, private MySQL 5.7.24
instance at 127.0.0.1:13306, with datadir ending in
`/migrations/mt4/private/mt5a-mamp-13306/data/`. The reviewed driver must verify
the full absolute datadir, process, listener, hostname, database and backups.
SQL installation gates and fixture guards only check the suffix, as does the
existing test helper; they do not replace the driver's absolute-path check.

Future order: 000 (seven zero rows), 001 once, 002 initial backfill, 003 (six zero
rows), 004 once, 005 (two zero rows), plus unchanged MT4 validators and independent
definition/data/FK verification. 000 supports only a clean predecessor or a fully
installed state, not an intermediate/partial deployment. Writers must stay quiet
throughout installation. 003 must pass before 004 is allowed.

All installation commands require root, the isolated target, strict session mode,
FK/unique checks enabled, abort-on-error and no `--force`. Prepared DO/invalid-column
gates reject an incorrect target in 001/002/004 without creating helper objects.
They rely on the client stopping on the error; they are not a defence against a DBA.
EXPAND and additive 004 deliberately fail on replay. DDL commits implicitly:
preserve partial state for diagnosis; no automatic restore, retry or destructive DOWN.

Fixture deletion requires the whole conjunction in three triggers: root login,
version/port/database/datadir, explicit session owner list, and an observed finite
synthetic-code allowlist with GUID suffix. The real holding code never qualifies.
This authorizes neither an application capability nor operational reset/recreate.
Roles still advance generation during fixture teardown and missing-state still
blocks role deletion. Only the parent fixture deletion removes its sidecar.

New review choices, not proven historical details: exact error texts, finite
fixture-code prefix list, stronger missing-state checks on no-op status/role UPDATE,
installation gates, and validator rule contents. Preservation of the real holding
assumes trusted maintenance does not relabel it as a synthetic fixture; root with
DDL privileges cannot be constrained by these triggers.

`verify_static.py` reads local files only. It checks package/hash integrity,
lexical framing, expected trigger names/shapes/order, dependencies and forbidden
statements. It is not a MySQL parser, a SQL execution or a .NET test suite.
Run `python migrations/mt5/candidate-r1/verify_static.py` after review edits; regenerate
the new manifest intentionally if the SQL changes. Never change the old manifest.

See `tests/Integration/MULTITENANCY_MT5B_SQL_RECONSTRUCTION.md` for source links,
traceability, uncertainties and the separate authorization required for real SQL.
