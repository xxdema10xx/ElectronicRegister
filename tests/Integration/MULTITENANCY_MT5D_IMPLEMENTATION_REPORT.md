# MT-5D implementation and verification

Completed 22 October 2026, after MT-5C was certified at 911/911 Passed.
No commit, push, reset or destructive checkout. Production rollout remains closed.

## Implemented code and interfaces

- Domain/Models/Governance.cs: IInstitutionGovernanceManager/Repository, IMembershipAuditManager/Repository and the typed audit projection. PlatformAction retains its previous numeric values and appends recovery revoke/reissue.
- Application/Managers/GovernanceManagers.cs: dedicated governance manager and current-tenant admin audit manager. Infrastructure/Repositories/InstitutionGovernanceRepository.cs is the authorized governance part of the shared transactional onboarding repository, separate from the legacy technical writer contract.
- Infrastructure/Security/PlatformAuthority.cs: ordinary signed-operation validation plus VerifyCommitted for already recorded recovery attestations; current key permissions, revocation, expiry and distinct identities remain mandatory.
- Infrastructure/Repositories/InstitutionOnboardingRepository.cs: verified recovery acceptance, replacement-before-revocation, activation and attributed technical audit in one transaction. No C# security generation setter.
- Infrastructure/Repositories/MembershipAuditRepository.cs: no-tracking, owner-filtered SQL with the actor's current active/admin status and persisted generation in the same query; limited projection excludes RequestHash and technical ledger content.
- API/Controllers/InstitutionMembershipAuditController.cs: exactly two new GET routes. DI and tools/PlatformAdmin were extended; no operator HTTP endpoint or frontend.

## Governance and exceptional recovery contract

Signed PlatformOperator commands support Active->Suspended, Suspended->Active with a valid persisted admin, and conservative Active/Suspended->Archived. Archived is terminal. Reason, evidence reference and EventId are signed; nonce reuse fails. Freshly signed retries with the same EventId and intent return historical results, with current authorization rechecked.

The institution row is the per-tenant mutex shared with IAM/onboarding. SQL advances all owner-scoped security states exactly once per effective institutional transition from Active/Suspended, rejecting missing state and Int64 overflow. No-op status updates do not advance. First-admin Provisioning activation does not add another increment to role assignment. Suspend/reactivate permanently revokes old JWTs; another institution of the same User is unaffected.

Recovery requires a Suspended institution and a signed attestation of renewed legal review and loss of usable administration. The application records the human decision and evidence; it does not claim to establish legal facts automatically. A fresh recipient must have no membership in that institution in any state. Existing membership in another institution is allowed; no implicit Left/Pending rejoin.

A case lasts at most 24 hours. Requesting it assigns no role and delivers no usable proof. Two distinct authorized operators sign the exact case intent; the request is not itself an approval. The second approval delivers a 256-bit personal challenge, stored only as hash, valid for 15 minutes and bounded by case expiry. Acceptance requires real authenticated global identity, current channel proof, both persisted signatures and current authority. Authorization is checked again after awaited work and on retries. One public key under two operator aliases is rejected.

Acceptance creates the new active admin before suspending previous active admin memberships, checks the replacement, activates the institution and writes RecoveryAccepted with the former admin IDs, all in one transaction. Historical roles, Users and audit are retained. No fictional IAM actor is created; recipient/operator actions use the separate technical ledger. Existing Last Admin and self-mutation rules remain enforced in ordinary IAM.

RevokeRecovery reduces pending access even if a second approver is no longer available. ReissueRecovery requires both original approvals still valid and rotates the challenge. Neither can change the approved recipient or roles. Without two real configured operators, granting recovery/reissue stays disabled. No real operators or notification provider were configured by this work.

Current contract and owner procedures: docs/architecture/INSTITUTION_GOVERNANCE_AND_RECOVERY.md and PLATFORM_OPERATOR_AND_ONBOARDING.md.

## HTTP audit

- GET /api/institution/admin/audit?page=1&size=20&target=<membership>&action=AddRole
- GET /api/institution/admin/audit/{eventId}

InstitutionAccess and current InstitutionAdmin are required. MembershipAdminApi defaults false. Every query filters the verified owner in SQL before materialization, and binds current actor privileges/generation in that same SQL. Ordering is UTC event time descending, then EventId descending; size is 1..100 and page is bounded. Responses are no-store.

The response contains EventId, actor User/membership, target membership, action/role, before/after role/status snapshots, reason, revisions and UTC time. No signatures, credential hashes, challenges, SQL details or provisioning/recovery ledger is exposed. Missing/invalid auth and revoked tokens retain the established 401/403 behavior; absent/cross-tenant detail is 404, malformed filters 400. There is no audit mutation route. The two old directory GETs and all previous routes remain; actual route inventories verify 163 MVC routes and eight admin-marked routes, of which the only MT-5D additions are these two GETs. LegacyEndpointGate remains fail-closed.

## SQL actually applied

Target: 127.0.0.1:13306, MySQL 5.7.24, PID 2156, C:/MAMP/bin/mysql/bin/mysqld.exe, electronic_register_test, private migrations/mt4/private/mt5a-mamp-13306/data.

Applied once, after full process/listener/SQL identity, writer checks, predecessor comparison and verified private backup:

| Source | SHA256 |
|---|---|
| migrations/mt5/governance/001_expand.sql | A55A819CB2B883FC902EFAFC47CCBBDCFCF986CFBDB98D800C133DDC342064C9 |
| migrations/mt5/governance/002_validate.sql (read-only) | 84006E850C7F3A571E63127E9594A155F3E92957776FD42CFFC3525A0236041C |

Two new tables: institution_recovery and institution_recovery_approval; six RESTRICT FK and eight triggers: institutional transition/generation plus request/approval insert/update/delete guards. All 80 preceding triggers, previous data and table definitions were preserved at installation. No old object dropped/recreated; no previous migration replayed and no partial DDL failure.

Final real inventory: **40 tables, 88 triggers, 118 FK, 164 indexes**. Full bodies, timing, DEFINER, SQL_MODE, charset/collation and ACTION_ORDER were checked; all 88 creation contexts remained unchanged during regression. All 118 FK, including composite keys, have zero orphans. Validators MT4A 7/7, MT4C 5/5, MT5B 6/6+2/2, IAM 4/4+2/2, onboarding 5/5 and governance 5/5 are zero. No residual IAM/onboarding/recovery fixtures.

Only deterministic SeedData 1e57 scenario rows changed in the seven grading/submission/audit tables intentionally exercised by tests. All other data and all schema definitions were preserved; no false claim that intentionally mutated test scenarios stayed byte-identical.

Private verified logical backups (footer, exit 0, inventory, SHA256 and full metadata/SQL_MODE overlay bundle):

| Stage | SHA256 |
|---|---|
| pre-governance.sql | C341F028CA3D6F51D8A20A584EADA478486E8B7BB5E0A5CD69939C48CAAC62FA |
| post-governance.sql | BD9F5AFC9E6CCDA2CCAFFA391132F4FB78F5C615704D9E2E9422021D5049EC2E |
| final.sql | E68C2249927DCB05198D0ABBEF09C84AAD8D894CCD8E3CEE17B6C2F192EBF65E |

The final backup is 232028 bytes, 40 tables/88 triggers. Evidence and backups remain under migrations/mt4/private/mt5a-mamp-13306/mt5d-implementation-20261022/, excluded from Git. No raw dump alone is claimed to preserve every historical SQL_MODE.

## Tests and corrections

Added **29 MT-5D test identities** in Mt5dGovernanceTests.cs and Mt5dAuditHttpTests.cs (partial shared synthetic fixture; historical class/method identities retained). Coverage includes:

- Lifecycle suspension/reactivation/archive, monotone JWT revocation and shared User A/B isolation; no-admin reactivation and overflow rejection.
- Operator/gate refusal, current key revocation, nonce/EventId replay/conflict, concurrent idempotency, rollback of status/generation/audit/nonce and the real CLI manager path without delivery dependency.
- Two independent recovery approvals, single-operator and alias refusal, duplicate/concurrent requests/approvals, authenticated personal acceptance, cross-tenant intent, revoked authorities, append-only records, and root knowing a proof not bypassing approvals.
- Recovery acceptance fault rollback and concurrent single consumption, per-case withdrawal, challenge reissue and old-proof rejection. Authority revoked during awaited approval/acceptance cannot commit partial effects.
- Actual Program/JWT/MySQL audit HTTP projection, current-role checks, revoked JWTs, A/B list/detail isolation, paging/filter validation, rollout refusal and no audit mutation route. Actual EF SQL capture verifies EXISTS/current generation and exclusion of request_hash; a manager-to-repository revocation race returns no audit rows.

Targeted runs: d-functional-a 20/20; d-functional-b 92/92 including historical backfills and route inventories; d-functional-c 4/4; d-authority-final 4/4; d-corrected-security 16/16. These overlap and are not summed.

The first JWT/IAM focused run had 138/144 passed and six historical assertions expecting no institutional lifecycle generation advance. Updated those same test identities to assert the exact governance increment and no additional advance from a denied technical/IAM writer. Non-active IAM denial is now tested with a fresh matching generation, avoiding a stale-token-only false positive. No trigger or authorization assertion was weakened. Final full regression includes all these tests passed.

The test-only DataProtection warm-up service was isolated from the Windows profile key ring while retaining the real application worker and authentication. Six offline bootstrap safety tests also pass. Existing unrelated nullable warnings remain; full builds succeed.

| Final regression | Result |
|---|---|
| Unit | 69/69 Passed |
| Real MySQL/Program/JWT integration | 871/871 Passed |
| Total | **940/940 Passed** |

Zero Failed, Skipped and NotExecuted. All **880 historical identities**, and all **911 MT-5C identities**, are preserved; the difference is exactly the 29 new MT-5D identities. Integration duration about 14.1 minutes. TRX SHA256, full identity comparison, source freeze and SQL evidence are in final-certification.json privately. No unexecuted test is presented as Passed.

## Portability and Git

Extended the existing public-only bootstrap to the complete MT-5D schema. Manifest closure: 104 SHA256-verified artifacts. Updated SQL consolidation/schema contract, setup validator, examples, documentation and ignore rules. New SQL is narrowly allowlisted; actual CLI configs and signed operation JSON are ignored while admin.example.json stays visible. New schema/artifacts use LF; immutable historical SQL/manifests retain certified byte conventions.

Fresh independent MySQL on 23309 bootstrapped from Git candidates only, with no private dump/datadir/source inputs. Verified all definitions/contexts, validators, FK, replay refusal and rejection of 3306, then built the public-only package. All 415 frozen C# source files are present in that package with identical hashes. Source-only final updates were rebuilt and the unchanged schema reverified. A Git clean/smudge roundtrip in the disposable package preserved all 104 manifest SHA256 values, without main-repository object/index writes or commits. Evidence: migrations/mt4/private/mt5d-portability-20261022/.

Temporary proof servers 23308 and 23309 were stopped gracefully after identity verification. No force kill; data/evidence retained privately. Main 3306 and authorized 13306 remain running.

New-computer commands are in docs/development/LOCAL_DATABASE_SETUP.md. The public runner deliberately requires its own creation attestation and will not adopt/reinitialize the current external R2 instance. This is a safety boundary, not a requirement for old private files on a new computer. All required fresh-setup inputs are public candidates; local credentials and diagnostic orchestration are private by design.

The overall exact review inventory is docs/development/VERSIONING_CANDIDATES.json, including prior uncommitted MT-5B-E and bootstrap changes, required-artifact tracking state and generated test evidence. Existence on disk is not claimed to mean committed/on GitHub. git status, git ls-files, check-ignore, diff --check, hashes, source closure and known-secret/private-key scans were checked. No staging, commit or push. Final content review remains the owner's decision before publishing.

## Protected baseline and operational limits

3306 was used only for read-only identity/fingerprint verification. It remains MT-4C: 32 tables, 53 triggers, 98 FK, 139 indexes; fingerprint FC6DF04E520A46B4084D9007A825D40DC354CE0C231CB80A0E20133886D92F22 unchanged. GLOBAL sql_mode unchanged on both servers. No XAMPP, restore/reset, FK/trigger disabling, old migration replay, service/config changes to the principal server, or frontend work.

No production notification provider, real signing authority or two real recovery approvers were installed. All rollout flags remain disabled by default. Owner registration, legal evidence review, production delivery and key custody/rotation remain explicit operational responsibilities. PlatformOperator has no automatic didactic privileges. No production readiness claim is made from synthetic legal attestations.

**MT-5D IMPLEMENTED AND VERIFIED**
