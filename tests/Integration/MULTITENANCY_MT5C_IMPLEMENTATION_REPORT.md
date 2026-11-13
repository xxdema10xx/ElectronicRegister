# MT-5C implementation and verification

Certified 22 October 2026. MT-5C completed before starting MT-5D. No commit or push.

## Implemented

- Domain onboarding contracts; InstitutionOnboardingManager; transactional InstitutionOnboardingRepository; DI.
- Explicit PlatformOperator authority: owner-installed public-key registry, RSA-PSS/SHA256 signed typed intents, current key permissions/revocation/expiry, short operation validity, persistent nonces, replay rejection and attributed technical audit. No authority from User.Role, root MySQL or ordinary JWTs.
- Protected administrative CLI in tools/PlatformAdmin. Signing uses an explicitly installed Windows certificate private key; no automatic authority generation or registration.
- Attested legal review, Provisioning institution, personally verified first-administrator invitation, membership/role creation and activation in one transaction after acceptance. Legal verification remains a human administrative attestation with evidence reference.
- Tenant-admin invitations, revocation/reissue, single-use random 256-bit proof stored only as SHA256, recipient User identity and channel binding, expiry, attempt limits, rate limiting, idempotency and isolation. No implicit rejoin or email-only association.
- Real HTTP invitation endpoints and selection-ticket acceptance, with existing InstitutionAccess authorization and rollout gates retained. No HTTP PlatformOperator endpoint.
- Technical provisioning ledger separate from IAM; verified provisioning excludes the legacy technical writer. Default channel and rollout remain closed.

Primary code: Domain/Models/Onboarding.cs, Application/Managers/InstitutionOnboardingManager.cs, Infrastructure/Repositories/InstitutionOnboardingRepository.cs, Infrastructure/Security/PlatformAuthority.cs and InvitationChannelRegistration.cs, API/Controllers/InstitutionInvitationController.cs, tools/PlatformAdmin.

## Persistence and target

Only 127.0.0.1:13306, MySQL 5.7.24, PID 2156, C:/MAMP/bin/mysql/bin/mysqld.exe, private migrations/mt4/private/mt5a-mamp-13306/data, electronic_register_test.

Applied once: migrations/mt5/onboarding/001_expand.sql. Four additive tables and twelve triggers; no existing object dropped or recreated. Versioned manifest and 002_validate.sql accompany it. Installed MT-5C inventory: **38 tables, 80 triggers, 112 FK, 159 indexes**. All previous 68 triggers preserved. New tables are accessed through parameterized SQL on the EF-owned connection/transaction; no full EF scaffolding of the new tables was performed.

Final verified private backup SHA256: **D7CAD3988384941E76961652E9C507A71160067E79D6AAFC41BC6AACB9054F33**, 216893 bytes, 38 tables/80 triggers, exit 0, footer and metadata/SQL_MODE overlay preserved. Pre-onboarding and pre-certification backups remain private. Full evidence: migrations/mt4/private/mt5a-mamp-13306/mt5c-implementation-20261021/.

## Actual tests

| Run | Result |
|---|---|
| Corrected targeted backfill compatibility | 62/62 Passed |
| Final unit regression | 69/69 Passed |
| Final real MySQL/Program/JWT integration regression | 842/842 Passed |
| Combined final regression | **911/911 Passed** |

Zero Failed, Skipped and NotExecuted in the final regression. All **880 historical test identities** preserved; **31 new MT-5C identities**. Targeted runs overlap and are not added to the total. TRX: unit-final-certified-20261022 and integration-final-certified-20261022 in the private evidence directory. The final integration run took about 29.5 minutes.

Tests cover operator signature/time/key/replay rejection, first-admin verification, no early role assignment, channel/user/tenant binding, wrong/expired/revoked proofs, reinvitation, concurrency, rollback fault injection, shared User A/B, rollout, rate limiting, legacy-writer denial and the actual CLI plus HTTP acceptance.

## Corrections during certification

The first resumed full run had 837/842 integration tests passed and five failures. Corrected IConfiguration registration for standalone DI builders, added explicit read-only MT-5C graph classification to historical tooling, and classified textual operator/key identifiers precisely rather than treating them as GUIDs. Canonical MT1/MT3/MT4 SQL remained unchanged; unknown or malformed schema still fails closed.

A fixture left by the user-requested interruption was proven to belong to two MT3C synthetic institutions and four synthetic users. After a fresh verified backup and dependency checks, only that fixture was removed transactionally using the existing scoped teardown guards. No database reset/restore or trigger bypass. The corrected targeted and full runs then passed.

Test DataProtection operations use an ephemeral provider; framework key-management warm-up can still emit Windows DPAPI/EventLog diagnostics. These are distinct from JWT signing and did not prevent the final successful run. Production authentication was not weakened.

## Final SQL and isolation

All 112 FK checked with composite-column joins: zero orphans. Validators: MT4A 7/7, MT4C 5/5, MT5B 6/6 plus 2/2, IAM 4/4 plus 2/2, onboarding 5/5, all zero. All 80 trigger definitions/contexts unchanged during the regression. No residual IAM/onboarding fixtures.

The historical SeedData.ResetScenarioAsync scenarios intentionally change rows in seven grading/submission/audit tables. Changes were checked against deterministic 1e57 scenario identifiers; all other data and all schema metadata were preserved. No preservation claim is made for deliberately mutated scenario rows.

3306 remained read-only and its full fingerprint unchanged: FC6DF04E520A46B4084D9007A825D40DC354CE0C231CB80A0E20133886D92F22. GLOBAL sql_mode unchanged on both servers. No XAMPP actions, repeated migrations, FK/trigger disabling or automatic restores.

## Portability and Git

Extended consolidated empty schema, schema contract, bootstrap manifest, validators, safe PowerShell/Python setup, examples, gitignore exceptions and newline rules. Canonical SQL is visible to Git; private datadir, backups, credentials and evidence remain ignored. Independent 23308 bootstrap from public candidate files verified complete schema, SQL_MODE/ACTION_ORDER, validators, all FK, replay refusal and rejection of 3306. Updated public-only package builds successfully. The temporary 23308 proof server was subsequently stopped gracefully; its data/evidence remain private. Evidence: migrations/mt4/private/mt5c-portability-C-20261021/certified-20261022.json. No private source inputs or data dumps were used for bootstrap.

Guide: docs/development/LOCAL_DATABASE_SETUP.md. Authority procedures: docs/architecture/PLATFORM_OPERATOR_AND_ONBOARDING.md. Source hashes and full test identities are retained privately. Git inventory for the overall delivery, including preceding MT-5B-E/bootstrap changes, is maintained in docs/development/VERSIONING_CANDIDATES.json; untracked candidates are not yet committed or published.

## Operational limits

No production notification provider or real operator authority was installed. These remain explicit owner actions; the operational path stays closed until correctly configured. No automatic legal verification. MT-5D is a separate subsequent implementation and is reported in its own report.

**MT-5C IMPLEMENTED AND VERIFIED**
