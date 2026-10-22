# MT-6C End-to-end validation and release readiness

22 October 2026. **Local implementation verified; operational release is NOT approved. Dependency-advisory clearance and external deployment validation are incomplete.**

## Executed evidence

| Check | Actual result |
|---|---|
| Final complete unit regression |69/69 Passed |
| Final complete real MySQL/Program integration regression |897/897 Passed |
| Distinct total |966/966 Passed;0 Failed/Skipped/NotExecuted |
| Historical identity comparison |All 940 retained; 26 new |
| Latest security/surface/worker/CLI targeted regression |33/33 Passed; overlapping, not added to total |
| Offline bootstrap/release safety tools |11/11 Passed |
| Public-only locked restore/build/Release publish |Passed |
| New-instance bootstrap and cold recovery |Passed,23312->23313 |
| Complete SQL checks on 13306 |40 tables,88 triggers,118 FK,164 indexes;36 validators zero;118 FK zero orphans |
| Original3306 |Exact data/metadata fingerprint unchanged |
| NuGet online advisory scan |BLOCKED: network denial/NU1900; unknown advisory status |
| Container build/image scan, actual cloud/proxy TLS, remote CI |NOT EXECUTED/unconfigured |

Final integration took about14.64minutes locally; this is regression timing, not a production benchmark. The first attempt had six failures, corrected without weakening assertions/security. See MT-6A. After the complete run, only test ACL cleanup and explicit minimal-route assertions were strengthened and the33affected/new tests rerun; application source/SQL stayed unchanged.

The suite covers real JWT issuance/current-generation validation, monotone revocation, IAM Last Admin/revision/EventId/concurrency/audit/rollback, first-admin/invitations, signed operator replay/revocation, dual-approval recovery, lifecycle and A/B isolation. Existing fault injection, closed-rollout, legacy-route and worker recovery tests all passed. New tests exercise malformed config, trusted/untrusted forwarding, HTTPS-scheme/HSTS/redirect handling, CORS, throttling, health/readiness, bounded metrics, sanitized errors/headers/logs, duplicate JWT headers and actual CLI file confidentiality. There are still163MVC actions; only two explicitly anonymous GET health minimal routes were added. No operator HTTP endpoint.

Private evidence: migrations/mt4/private/mt5a-mamp-13306/mt6-20261022/ (TRX, exact identity delta, initial/final SQL fingerprints, validators, all trigger contexts and verified logical backup bundle); migrations/mt4/private/mt6-portability-final-20261022/ (public inputs, build/publish,478 file physical manifest and verified recovery). None are Git/deployment inputs.

Final logical test backup SHA256: **DD86F7FEA12FBE60A9042A2DC9D4B8F26E8DEC2F858DBEF67DDB131DF0394622**; exit0/footer/inventory/context overlay verified. It is archival evidence only; no logical restore or trigger overlay was executed. The portable cold recovery preserves exact SQL_MODE through physical files, without the MySQL 5.7bare-dump limitation.

3306 fingerprint: **FC6DF04E520A46B4084D9007A825D40DC354CE0C231CB80A0E20133886D92F22**, unchanged. GLOBAL sql_mode unchanged on both existing instances. No IAM/onboarding/recovery fixtures remain. Historical seed scenarios intentionally mutate seven known seed tables during regression; all other table data and every definition/context remain identical. No claim is made that test scenario data never changed.

## Go-live checklist

- [x] Security hardening, external configuration boundary, liveness/readiness, structured privacy-bounded logs/metrics and graceful host shutdown implemented/tested locally.
- [x]940 historical tests retained;26 added; complete regression green; route/gate and tenant isolation checks passed.
- [x] Hash-checked public schema/bootstrap, lock files, clean public source package, full build/publish and same-engine cold recovery demonstrated without private source dependencies.
- [x] No MT-6 schema migration needed; existing13306definition inventory preserved.3306strictly read-only. No deployment/commit/push.
- [ ] Run successful online direct/transitive NuGet audit, resolve/retest advisories, install current approved .NET/OS patches and retain evidence. Blocked scan is not a waiver.
- [ ] Select/certify a supported MySQL LTS on an independent environment. Follow rehearsed supported upgrade steps; never overwrite5.7test/baseline datadirs or claim8.4compatibility from this suite.
- [ ] Choose staging/production host/provider and process supervisor; build/scan/pin OCI digest if containers are selected. Remote CI and disposable protected runner/environment need owner configuration and execution.
- [ ] Configure DNS, certificates, TLS policy/renewal, exact trusted proxy/firewall rules and host/origin allowlists; perform real encrypted staging E2E, readiness and drain/restart verification.
- [ ] Register real operator keys/identities through owner procedure, protect Windows private keys, install and test current registry on every host; configure two real recovery approvers and a trusted operational channel adapter. No fake authority/provider was installed.
- [ ] Configure external JWT/DB secrets, least-privilege DB/definer/migration identities, audited secret rotation, operational CLI target enrollment and telemetry collection/alerts/retention. PlatformOperator receives no tenant teaching privilege.
- [ ] Agree RPO/RTO, encrypted off-site retention and recovery authority; rehearse representative-volume/PITR recovery on sacrificial staging copies. Current drill does not prove production SLA.
- [ ] Review exact Git candidates and sensitive-output classification, authorize commit/publication and approve the operational release explicitly.

## Rollout and rollback

Keep all flags false initially. In independently configured staging: verify artifact hashes, SQL contract/FK/validators and backup bundle, health/readiness/TLS, current registry/channel and negative cross-tenant/revocation tests. After explicit owner approval, enable the smallest reviewed cohort/surface, monitor auth failures/latency/audit/idempotency/DB locks and stop on security or integrity anomalies. Do not enable broad legacy identity or grant didactic rights to platform operators.

Rollback is application-only by default: drain, disable the reviewed rollout surface, stop gracefully and deploy the previous verified compatible artifact/config. Preserve additive schema, data, audit, nonces and monotone generation. Never reset generation or automatically restore a live DB. Any database incident recovery needs separate authority, verified backups and agreed data-loss window.

## Git and verdict

The124 artifact bootstrap/infrastructure manifest, eight lock files, examples, SQL and source are Git-visible. VERSIONING_CANDIDATES.json contains the exact combined review inventory, including prior uncommitted work. Private data, configs, keys, datadirs, dumps and diagnostics remain excluded. No commit/push or destructive Git operation occurred.

**MT-6 LOCAL IMPLEMENTATION VERIFIED ? OPERATIONAL RELEASE BLOCKED**

The stronger unconditional completion claim is withheld because online dependency security clearance, container/remote infrastructure checks and owner-provided production components have not been verified. This is implemented code with green local regression, not a plan-only handoff or a production-readiness declaration.
