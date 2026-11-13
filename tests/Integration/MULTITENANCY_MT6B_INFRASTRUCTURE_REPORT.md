# MT-6B Infrastructure and reproducibility

22 October 2026. Portable implementation and local build/bootstrap/publish/recovery verified; no provider deployment performed.

## Implemented

- RuntimePolicy startup validation; explicit LocalTest/Staging/Production profiles. Operational mode must match host environment. LocalTest is loopback-only with exact13306 test DB; operational mode requires non-root TLS verification, explicit hosts/identity/HTTPS and reviewed rollout configuration. Flags remain false by default.
- External ER_RUNTIME_CONFIG, environment variables and mounted /run/secrets KeyPerFile configuration. No application key/password/default operator authority is shipped. appsettings never enters publish output.
- GET /health/live and /health/ready with generic responses, stopping-host refusal and bounded SQL identity probes. Full schema validation remains a separate release preflight.
- Structured JSON completion/error-type logs, correlation header/scope, privacy-bounded request counter/duration histogram through ElectronicRegister.Http Meter. No public metrics endpoint.30-second host shutdown budget and explicit worker rollout guards.
- Restricted forwarding, HTTPS/HSTS and security headers. Exact proxy/certificate/firewall topology is an owner decision; real TLS termination is not inferred from forwarded-header tests.
- Deterministic build properties, SDK selector, eight NuGet lock files, locked restore and release publication. prepare-release.ps1 refuses existing output/config/keys and writes a complete SHA256 artifact manifest.
- Provider-neutral non-root OCI recipe and restrictive Docker context; GitHub CI with full SHA-pinned Node24-compatible actions, read-only token and no persisted checkout credentials. Public source/hash/unit/audit job; protected manual full-MySQL job on a disposable Windows runner. No privileged PR execution or production secrets are needed.
- Public-only source packager and guarded disposable cold-backup/recovery drill. Existing hash/journal/identity migration runner retained for NEW empty test instances; no old migration replay or adoption of13306.

Primary files: Program/Operations, Directory.Build.props, global.json, eight packages.lock.json files, infra/Dockerfile/runtime.example.json, .dockerignore, .github/workflows/verify.yml, tools/prepare-release.ps1, package-versioning-candidates.py, verify-disposable-recovery.py, check-dependency-audit.py and test_release_tools.py; RELEASE_OPERATIONS.md and LOCAL_DATABASE_SETUP.md. Exact complete Git candidates are listed separately.

## Actual reproduction

The existing bootstrap was extended rather than replaced. Schema contract and consolidated SQL remain unchanged:40/88/118/164. Manifest closure increased104->124 artifacts, including locks and infrastructure. Narrow .gitignore SQL exceptions remain; broader appsettings privacy exclusion and disposable proof directories were added. .gitattributes fixes critical raw-byte hashes, historical CRLF, infrastructure and lock-file LF.

New source package selected only Git-visible/manifest candidates, without private dump/config/code inputs. Final package contains566 public files before final documentation/fixture-only refresh. Actual public PowerShell setup created MySQL23312 from zero; it verified full table/index/FK definitions and all88 trigger bodies, SQL_MODE, charset/definers/ACTION_ORDER,118 FK without orphans and36zero validator rules. Locked restore, full solution build and Release publish succeeded. Final source-only cleanup/assertion and CI/doc changes are hash-checked and rebuilt; no SQL definition changed.

An earlier independent23310 rehearsal also succeeded. Both rehearsals used different new datadirs and freshly generated private credentials, not a copy of3306/13306. Application source closure is compared by SHA256 with the public package. The critical124 artifact Git clean/smudge roundtrip is verified in the disposable package only; main repository index/objects are not staged/committed.

## Backup/restore drill

Public tool refuses3306/13306, occupied ports, unknown creation attestations, nonempty destinations, overlaps and replay. Fresh23312 had identifiable synthetic institution/User/membership/admin-role data with generation1. It verified schema/data, stopped that process gracefully, waited for PID and listener disappearance and copied the full cold datadir plus my.ini to a new ACL-private backup. Complete478 file SHA256 manifest,133129735 data bytes, binary/config identity, snapshots and free-space results were retained privately.

Recovery used a SECOND copy into a new datadir on23313, never the backup itself. File hashes matched before startup. Real SQL then proved every40table row fingerprint, nonzero generation,88 trigger creation contexts,118 FK and36 validators. Both disposable servers were stopped gracefully and original backup hashes remained identical. No SQL constraints/triggers were disabled and no dump overlay was executed. This certifies same-binary Windows cold recovery on small synthetic data, not cross-version recovery, representative production volume, PITR, encryption/off-site retention or SLA.

## Commands and operational decisions

Fresh-PC setup commands remain in LOCAL_DATABASE_SETUP.md; never rerun CreateInstance on the current R2 instance. Typical public checks:

```powershell
python tools/verify-project-artifacts.py
python -m unittest discover -s tools -p 'test_*.py'
dotnet restore Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx --locked-mode
python tools/package-versioning-candidates.py NEW_PUBLIC_PACKAGE
./tools/prepare-release.ps1 -OutputDirectory NEW_PRIVATE_RELEASE
```

Full recovery command/guards are in RELEASE_OPERATIONS.md. No MT-6 DDL is required. Staging/production have received no migrations: the owner must select/certify a supported DB engine, deployment identity/definers and migration authority before an operational schema installation. The current administrative CLI deliberately retains its isolated Windows test-target guard; operational target enrollment requires an explicit reviewed operational configuration rather than a port substitution.

Owner configuration still required: deployment platform/process supervisor, patched runtime/container image digests, supported SQL compatibility environment, least-privilege DB accounts/TLS, domain/proxy/certificates, secret storage, operator registry/Windows key custody/two real approvers, trusted notification adapter, telemetry collector/alerts, RPO/RTO/retention/encryption/PITR and protected CI runner/environment. Registry changes must be applied to every host before claiming revocation is effective.

No Docker executable or local YAML parser was available. OCI build/image scan and remote CI execution are NOT reported passed. PowerShell scripts parsed successfully; actual setup/publish scripts ran. NuGet advisory feed was blocked: online security clearance remains required. Eleven offline tool tests passed. Full application regression is966/966 with940retained identities.

## Git/database boundaries

Inventory: docs/development/VERSIONING_CANDIDATES.json, including prior MT-5B-E/bootstrap/C/D changes. git status/ls-files/check-ignore/diff --check, private-path/key/known-secret scans and closure checks were performed. SQL needed by bootstrap is versionable; no private dependency remains. Commit/push still await owner review.

3306 unchanged/read-only.13306 schema and GLOBAL modes unchanged, no restore/reset/reinitialization/old migration replay. All temporary backup/recovery databases are distinct and stopped. No XAMPP or deployment.

Final Git byte roundtrip caught a missing LF attribute for .dockerignore. Added the exact attribute and regenerated the bootstrap/inventory hashes before rerunning the proof. No SQL was involved.
