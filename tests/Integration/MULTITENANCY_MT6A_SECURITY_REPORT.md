# MT-6A Security hardening

22 October 2026. Implementation and local regression verified. **Online dependency-advisory clearance remains blocked; no production security approval is claimed.** No commit, push or deployment.

## Review and fixes

Consulted current MT-5C/MT-5D implementation reports, PlatformOperator/onboarding and governance/recovery contracts, IAM mutation/audit contract, bootstrap guide, Program, institutional JWT authentication/token emission, Microsoft token validator, operator authority/CLI, onboarding/governance/IAM/audit repositories, worker and exception handlers. Historical migrations were not reconstructed or replayed.

| Finding | Implemented correction | Actual negative evidence |
|---|---|---|
| CORS allowed every origin | Exact configured origins; closed default; no credential wildcard | Real Program preflight accepts only approved origin |
| Sensitive HTTP requests lacked centralized throttling | Per-client fixed window for POST /api, bounded queue zero,429/Retry-After; existing tenant5/min and invitation5-attempt limits retained | Changing forwarded IP cannot evade limiter; health remains available |
| No strict operational startup boundary | RuntimePolicy validates mode/environment, concrete hosts, key strength/issuer/audience, non-root TLS-verified DB identity, expected SQL identity, HTTPS/proxy config; local profile confines listener and DB to loopback13306 | Unsafe host/port/key/origin/proxy/flags refused before connection; real incomplete Production startup refused |
| Proxy/transport/header defaults incomplete | Explicit proxy IP allowlist, one hop, trust-all environment switch refused, HTTPS redirect/HSTS, headers and1MiB limit; Kestrel Server header removed | Real trusted-proxy Staging branch and spoof refusal; security headers on200 and500 |
| JWT header ambiguity | Duplicate signed header properties rejected for both institutional token types; global legacy bearer restricted to HS256/zero skew | Real correctly signed duplicate-header selection/access JWTs return401; no MAC-bypass claim |
| Legacy worker scanned despite closed rollout | Explicit legacy enablement required; institution lane retains independent default-deny flag | Disabled lanes do not resolve repositories; original recovery/concurrency/worker delivery tests still pass with explicit test opt-in |
| Administrative config confidentiality | CLI allows config data/permission access only to current operator/System/Administrators; trusted owner required; fixed configuration snapshot | Actual CLI refuses Everyone-readable config without SQL mutation; positive signed CLI paths pass with private ACLs |
| Exception/query disclosure risk | Type-only worker/global500 errors; framework request-start, EF-command and exception-middleware logging filters; generated correlation; headers registered OnStarting | Fault-injected real login500 leaks neither exception secret, password nor query, and retains headers |
| Web publish could include local configuration | appsettings excluded from output/publish; source-only Docker context; publisher rejects config/keys and existing output | Public-only Release publish succeeded without local configuration/keys |

Files: API Program, Operations/RuntimePolicy.cs, Operations/OperationalPipeline.cs, GlobalExceptionHandler.cs, InstitutionAuthentication.cs, ExpiredExamSubmissionWorker.cs, API csproj/example/launch settings; tools/PlatformAdmin/Program.cs; MT-6 tests and private test-file ACL helper. Full combined file list is VERSIONING_CANDIDATES.json.

## Preserved controls

InstitutionAccess remains owner/membership/user/status/role/generation checked against current SQL on every protected request. No User.Role tenant authority, zero-generation fallback, operator HTTP surface or IAM bypass was added. Current authorization is repeated under tenant mutex on retries; Last Admin/self-mutation, EventId/revision conflict and transactional ledger/generation behavior remain covered by the full regression.

Operator RSA-PSS/3072-bit minimum, signed intent/action/nonce/time binding, current registry permissions and key alias rejection remain. Recovery retains two distinct operators and personal single-use challenge acceptance. SQL identifiers come from fixed catalogs; request values remain EF/ADO parameters. Audit projections remain owner-filtered before materialization and exclude credential/request hashes. DTO unknown-member/enum/reason validation and centralized generic ProblemDetails remain. LegacyEndpointGate is unchanged; new anonymous minimal surfaces are only two GET health routes.

## Tests and corrections

Final full regression:69 unit +897 integration = **966/966 Passed**, zero Failed/Skipped/NotExecuted. Exact TRX set comparison proves all940 historical identities retained and26 new identities added. Targeted runs16,36,23 and corrected33 overlap and are not summed. Final targeted security/surface33 also verifies the latest test-only ACL cleanup and explicit minimal-route inventory after the full run; no application behavior changed after that full run.

Initial full attempt:888/894 integration passed; six failures were four historical worker tests relying on implicit enablement and two CLI fixtures inheriting insufficiently private ACLs. Tests now explicitly enable their authorized legacy lane, retain delivery/concurrency/rollback assertions, and create private credential files. Error assertions check the retained failure type and absence of exception objects. No authorization was weakened and no test identity removed. Negative ACL fixtures restore their private permissions in finally; temporary test grants from exploratory runs were removed from those files only.

## Dependencies and residual risk

Installed SDK10.0.401 / runtime 10.0.12 were verified. Eight NuGet lock files now freeze resolution, with locked restore/build/publish tested. No package/runtime upgrade is claimed. The official [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy) listed10.0.13 when checked; latest approved runtime/OS patch rollout and regression remain required.

`dotnet list ... package --vulnerable --include-transitive --format json` failed because api.nuget.org:443 is blocked by this execution environment. Restore reports NU1900. **Advisory status is UNKNOWN, not zero vulnerabilities.** CI fails closed on unavailable/malformed/advisory-bearing audits;11 offline tool tests include negative audit cases. Repeat the online audit, review all direct/transitive advisories and verify any updates before release approval. Existing nullable warnings remain; builds succeed.

[Oracle EOL notice](https://www.mysql.com/support/eol-notice.html): MySQL 5.7 ended active development25October2023 and is under sustaining support. RELEASE_OPERATIONS.md documents an independently rehearsed supported-version path. No8.4binary, engine change or compatibility certification was performed. TLS negotiation, real certificates, delivery providers, deployed distributed quotas and an external penetration test are not certified by local HTTP/header tests. Rate limiting is per process/IP; scaled deployment needs edge/shared quotas.

## Git and database

124 hash-verified public bootstrap/infrastructure artifacts, eight lock files, narrow SQL exceptions and consistent critical-artifact EOL. Private files/keys/dumps/datadirs remain excluded. Exact Git inventory and final checks are shared with MT-6C; nothing is claimed already on GitHub.

13306 remains40 tables/88 triggers/118 FK/164 indexes, all36validator rules zero, no IAM/onboarding/recovery fixtures. GLOBAL modes unchanged.3306 remains exact original fingerprint FC6DF04E520A46B4084D9007A825D40DC354CE0C231CB80A0E20133886D92F22, read-only. No schema migration, restore/reset, FK/trigger disabling, SET GLOBAL, XAMPP or production operation.
