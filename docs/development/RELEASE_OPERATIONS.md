# MT-6 release operations

No production deployment is authorized by this document. Local certification is
not approval to expose the service. Current database certification remains MySQL
5.7.24 on Windows; no compatibility claim is made for an untested engine.

## Configuration and transport

Development/test uses explicit `Deployment__Mode=LocalTest`, loopback HTTP binding,
127.0.0.1:13306 and electronic_register_test. The profile refuses external listeners,
other databases/ports and proxy trust. Production/Staging requires its explicit
mode matching ASPNETCORE_ENVIRONMENT (Staging or Production), concrete AllowedHosts, JWT key >=48 UTF-8 bytes, issuer/audience, non-root DB
identity with password and SslMode=VerifyFull, expected SQL hostname/datadir/version,
and Transport:HttpsPort. Unsafe/incomplete configuration fails before DB DI.

Use `infra/runtime.example.json` as a non-secret template; install the actual file
outside Git and select it with ER_RUNTIME_CONFIG. Configuration is read once.
External secret stores can inject environment variables (`Jwt__Key`,
`ConnectionStrings__DefaultConnection`) or mounted KeyPerFile names using `__` below
/run/secrets. Do not pass secrets in shell arguments. Apply registry revocations/rotations to every CLI and API host, drain/restart hosts
that use the startup configuration snapshot, and verify old-key rejection before
returning traffic. Editing an unapplied file or revoking only a local certificate
is not proof that all hosts have adopted a revoked public-key registry.
Protect secret files/readers
and service identities with OS ACLs. No default signing key, DB password, operator
authority or invitation provider is shipped.

CORS defaults closed; list exact approved HTTPS origins. Credentials are not enabled.
TLS must terminate at an owner-selected proxy or Kestrel certificate. Configure only
the proxy's exact IP addresses in Transport:KnownProxies, one forwarding hop.
Restrict network access so clients cannot bypass that proxy. No trust-all forwarded
headers; ASPNETCORE_FORWARDEDHEADERS_ENABLED is refused in favor of the explicit
proxy allowlist. TLS policy, certificate issuance/renewal, DNS, firewall and exact proxy
topology remain operational decisions. Use TLS1.2+ and disable obsolete protocols
at the chosen terminator; that terminator has not been deployed/tested locally.

Production emits HSTS (180 days, no subdomains), HTTPS redirects, restrictive CSP,
no-sniff, DENY framing, no-referrer, no-store and permissions restrictions. Development
OpenAPI/Scalar stays development-only. JSON request size is capped at1MiB unless
an endpoint has a stricter limit. Rollout flags default false. Legacy identity is
forbidden in Staging/Production. Any intentional rollout additionally requires
Deployment:RolloutApproved; that setting is not a substitute for release approval.

## Runtime visibility and shutdown

GET /health/live checks only the process; GET /health/ready checks current SQL
version/host/port/datadir/database with bounded3-second connect/command timeouts
and4-second cancellation. Failure returns503 without database/error details.
Readiness is a connection/identity probe, not a schema migration authority; run
full contract/FK/validator verification before promoting an artifact.

Framework request-start/SQL-command/exception middleware logging is filtered to
prevent query-string or exception/SQL disclosure; error logs retain error type only.
Logs are structured JSON with a generated correlation ID returned in
X-Correlation-ID. Completion logs include status only, never request bodies,
Authorization, query strings, invitation proof, signatures or SQL. Retain the enforced framework/EF filters on operational hosts;
keep log access/retention restricted. Request buffering/body logging is forbidden.
Meter `ElectronicRegister.Http` exposes a request counter by bounded numeric status
and a duration histogram. Attach an owner-selected .NET MeterListener/OTLP collector;
no public unauthenticated metrics dump or provider is installed. Dashboard, alert
thresholds and external collection are deployment responsibilities.

Readiness rejects stopping hosts. ASP.NET graceful shutdown budget is30seconds;
the worker uses cancellation and a bounded cycle. Default-closed legacy/institution
lanes do not resolve repositories. Drain proxy traffic first, then SIGTERM/service
stop, wait for exit, investigate timeout. Never force-kill DB as automatic rollback.

## Artifacts and CI

`python tools/verify-project-artifacts.py` checks the bootstrap's complete SHA256
closure and Git visibility. `python tools/package-versioning-candidates.py NEW_PATH`
creates a fresh public-only source package and SHA256 inventory, refusing ignored
dependencies. Build/setup inputs come from that package, not private/.

`./tools/prepare-release.ps1 -OutputDirectory NEW_PRIVATE_OUTPUT` publishes an
application artifact with a per-file SHA256 manifest; it refuses existing output
and local appsettings/keys. `infra/Dockerfile` is a provider-neutral, non-root OCI
recipe. No database, operator CLI private keys or local config is copied into it.
Resolve/pin reviewed patched image digests before release; image build/scanning
requires an available container engine and has not been inferred from dotnet publish.

Eight NuGet lock files preserve dependency resolution; CI restores in locked mode.
The .NET10 SDK feature line is selected by global.json with newer stable10.0feature
bands permitted. The local package cache is not treated as a vulnerability audit.
GitHub Actions are pinned to full commit SHA values verified against their official
release pages (checkout7.0.1, setup-dotnet6.0.0, setup-python7.0.0). Self-hosted
runners must support Node24 (runner>=2.327.1).
GitHub workflow verifies hashes, offline tooling, restore/build/unit tests and
NuGet advisories. Real full MySQL regression requires a separately provisioned
disposable Windows runner/environment, approved manual dispatch and ER_MYSQL_BIN.
Do not expose production secrets or permanent DBs to pull-request code. A completed
instance's journal refuses replay; use a new disposable runner rather than cleanup
or reset. CI definitions are candidates until the owner commits/configures/runs them.

## Schema and upgrade

No MT-6 schema change is required. The existing hash-checked, journaled empty-test
bootstrap is the migration runner for new test instances. It checks executable,
listener, PID, version, hostname, server UUID, datadir, schema, all88trigger bodies,
SQL_MODE/ACTION_ORDER,118FK and36validator results. It refuses3306, nonempty targets,
unknown instances and replay. It does not automatically adopt existing servers or
apply historical migrations on top of consolidated DDL. No staging/production
migration has been executed or approved; provision a reviewed deployment-specific
runner/identity/definer plan for the chosen supported target before any operational DDL.

MySQL5.7 ended active development25October2023; Oracle lists sustaining support.
Target a supported LTS, initially8.4, in a new independent compatibility environment.
Inventory reserved names/collations/auth plugins, remove obsolete NO_AUTO_CREATE_USER
only in a separately versioned conversion, review CHECK behavior/trigger modes,
provider compatibility, definer least privilege and upgrade checker output. For the supported major-version upgrade path use a separately rehearsed5.7latest
->8.0latest ->8.4LTS sequence; direct5.7->8.4 upgrade is unsupported. A logical
export/import alternative also needs independent compatibility proof. Never
transplant5.7 files into8.4. Preserve
the original5.7 backup. Run full schema/functional/concurrency/JWT/recovery regression,
clone bootstrap and backup/restore before certification. No8.4binary was installed
or compatibility demonstrated in this checkpoint.

Official sources checked22October2026:
[MySQL EOL](https://www.mysql.com/support/eol-notice.html),
[upgrade restrictions](https://dev.mysql.com/doc/relnotes/mysql/8.4/en/news-8-4-0.html),
[.NET policy](https://dotnet.microsoft.com/en-us/platform/support/policy).
Installed .NET10.0.12 must be compared with the current patched10.0release (the policy
lists10.0.13). NuGet audit must succeed online and have no unresolved advisories;
an offline/blocked scan is not evidence of no vulnerabilities.

## Backup, recovery and rollback

Operational strategy: encrypted physical backups compatible with the exact engine,
plus logical dumps/complete trigger metadata contexts, manifest hashes and verified
binary logs for the owner's required point-in-time recovery. Agree RPO/RTO,
retention, off-host private storage, keys, restore authority and regular rehearsals.
The local MySQL5.7 dump omits some per-trigger SQL_MODE. Never call a bare dump an
exact restore; preserve full contexts/overlay separately. Never automatically
replay a destructive overlay against an existing application schema.

The public drill `tools/verify-disposable-recovery.py` is deliberately narrow:
two NEW attested sacrificial loopback instances with ports>=23000, same MySQL5.7.24
binary and Windows. It verifies current public bootstrap, creates identifiable
synthetic rows with nonzero generation, fingerprints all40tables, stops only that
source gracefully and waits for both PID/listener disappearance. Then it copies
the complete cold datadir plus my.ini into a NEW ACL-private backup with complete
SHA256/file-count/size manifest. A separate new working copy is verified before
startup, configured on a distinct private path/port, and validated against every
table/trigger context/FK/validator and original row fingerprint. Both disposable
servers are left stopped; original backup is never used as a working directory.
Source credentials are newly generated for this rehearsal, never copied from3306.

Example after a fresh public-only bootstrap on23310 (password in secure child env):

```powershell
python tools/verify-disposable-recovery.py --mysql-bin C:/MAMP/bin/mysql/bin `
  --source-data C:/NEW-PACKAGE/migrations/mt4/private/mt5a-mamp-13306/data --source-port 23310 `
  --target-data C:/NEW-RECOVERY/migrations/mt4/private/mt5a-mamp-13306/data --target-port 23311 `
  --backup C:/NEW-PRIVATE-BACKUP --confirm-sacrificial
```

The drill refuses13306/3306 and preexisting destinations. It does not certify
cross-version recovery, production PITR, off-site encryption or a recovery SLA.
No FK/trigger is disabled and no existing DB is restored/reset. On any failure stop,
preserve files/journal and diagnose; no automatic retry/restore/deletion.

Application rollback: drain traffic, disable rollout using reviewed configuration,
stop the candidate gracefully, deploy the previous signed/hash-verified compatible
application artifact and restore its non-secret config references. Keep additive
schema/data/audit/generation intact. Never reset generation or delete ledger rows.
A DB restore requires separately authorized incident recovery and verified RPO/RTO,
not an automatic response to a failed app rollout.
