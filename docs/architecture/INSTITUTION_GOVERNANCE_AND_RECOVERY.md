# Institution governance and exceptional administrative recovery - MT-5D

The current executable contract extends the verified MT-5C onboarding and MT-5B IAM.
Institution means the ITS Foundation. PlatformOperator authority and recipient proof
are defined in PLATFORM_OPERATOR_AND_ONBOARDING.md. This document supersedes older
MT5A planning sections; it does not claim a production provider or authority exists.

## Administrative surface and authority

Governance uses IInstitutionGovernanceManager and the protected PlatformAdmin CLI.
There is no public HTTP lifecycle/operator/recovery-management endpoint. Every
operation has an explicitly registered operator/key, permitted action, RSA-PSS
signature, bound typed intent, institution, EventId, short issue/expiry window and
unique persistent nonce. A new signed nonce is required for an idempotent retry;
exact nonce replay is rejected. Reusing EventId with a different intent conflicts.
Authorization is repeated under the tenant row mutex and immediately before commit.
Root MySQL is a test transport credential, not an authority decision. Ordinary
JWTs, User.Role, emails and tenant administrators cannot authorize platform actions.
PlatformOperator never gains an InstitutionAccess token or didactic membership.

Owner-installed public-key configuration is authoritative. Keep private signing
keys in the Windows certificate store, protect the administrative configuration
and deploy the same trust registry to CLI and acceptance host. Revoke/remove keys
or permissions and rotate with distinct KeyId values. Never auto-register a key
from a request. Two different keys belonging to the same operator count once;
the same public key registered under two operator identities is rejected.
The owner must attest that distinct operator IDs identify distinct responsible
people; software cannot independently prove their legal identity. Keep clocks synchronized.

Multitenancy:Governance:Enabled defaults false. Notification-dependent paths also
require an operational owner-installed channel; acceptance retains the onboarding
flag. Suspension/archive can operate without notification delivery. No actual
production keys/operators/provider are installed by the migration or bootstrap.

## Lifecycle policy

- Provisioning -> Active: only verified MT-5C first-administrator acceptance.
- Active -> Suspended: signed operator decision with reason and evidence reference.
- Suspended -> Active: new signed review, current persisted valid administrator required.
- Active/Suspended -> Archived: signed conservative archival; all data and audit retained.
- Archived is terminal. No reactivation or implicit return to Provisioning.

The institution row is the tenant mutex shared with IAM/onboarding. New SQL triggers
advance all security sidecars of that institution once per effective lifecycle change
from Active/Suspended, fail on missing state/Int64 overflow, and reject leaving
Archived. No-op updates do not increment. Reactivation requires a current valid
admin. Provisioning activation does not add a second increment to first-admin role
assignment. No generation reset or C# generation setter is used. Old JWTs remain
revoked after suspend/reactivate; other institutions of the same User are unaffected.
Technical events and state updates commit together; failures roll back DML, not DDL.

## Exceptional recovery policy

Recovery is a separate high-impact signed workflow, not a password reset. The
institution must already be Suspended by an explicit governance decision. Operators
attest loss of usable administrative authority and renewed legal verification with
an evidence/dossier reference. The application does not infer that legal fact from
an email, missing password or database row; the signed human review is mandatory.

1. RequestRecovery binds institution, new recipient UserId, RecoveryId, declared legal
   identity, evidence and reason. The recipient must have no membership in this
   institution in any state; an existing membership in another institution is fine.
   No Left/Pending rejoin or automatic account association is implemented.
2. An immutable case lasts at most 24 hours. The initial invitation proof is random,
   hashed and undelivered: a request alone cannot grant access.
3. Two distinct currently authorized operators each sign ApproveRecovery for the
   exact case intent. The requester may be one approver, but the request itself is
   not an approval. The second approval delivers a fresh 256-bit personal challenge
   with 15-minute validity, bounded also by case expiry. No roles are assigned yet.
4. The recipient authenticates globally and proves channel control through the same
   real selection-ticket acceptance endpoint. Current case, both signatures, key
   revocation/expiry/permissions, distinct operators and owner are verified again,
   including on retries. Committed signatures attest an already accepted decision;
   their original short submission window is not extended for new operations. Case
   expiry and current authority remain mandatory for acceptance.
5. In one transaction, create a fresh active admin membership, consume the invitation,
   suspend prior active admin memberships, verify the replacement, activate the
   institution and append an attributed technical RecoveryAccepted event including
   IDs of the former admins suspended. Create the replacement before reducing old
   admins. Keep roles, users and all historical audit. Do not impersonate an IAM actor.

Revocation/reissue are signed platform actions, not tenant-admin capabilities.
RevokeRecovery can reduce pending access even when a second approver is no longer
available; it cannot activate or create membership. ReissueRecovery requires both
original approvals still valid and rotates the challenge, invalidating the old one.
The target/case stay immutable; a new reason/evidence may explain withdrawal/redelivery.
A recipient cannot change the approved nomination or roles. Expired/revoked/consumed
invitations cannot be newly accepted. A consumed matching EventId returns the stored
receipt only after current authentication/authority checks; no duplicate mutation/audit.

Cases, independent approvals, technical events and nonces are persistent and
append-only. Only tightly guarded synthetic test teardown can delete them on the
attested 13306/private datadir, with explicit MT5BP fixture owners, excluding the
real/technical holding. No FK/trigger disabling. If fewer than two authorized operators
are configured, granting recovery and challenge reissue are disabled. Do not create
fictional approvers to open production.

## Read-only IAM audit HTTP

GET api/institution/admin/audit: page (>=1), size (1..100), optional target membership
and exact IAM action. GET api/institution/admin/audit/{eventId}: one visible event.
Both use the existing InstitutionAccess/InstitutionAdmin policy and closed-by-default
MembershipAdminApi flag. The manager rereads current membership privileges; every
repository query applies InstitutionId in SQL before materialization, with no-tracking
projection. The same SQL also requires the actor still active/admin in the active
institution with the current persisted generation, closing the read-authorization
race after the manager check. Ordering is OccurredAtUtc descending, then EventId descending. Detail
outside the tenant is indistinguishable from absent (404). Invalid filters give 400.
Missing/invalid auth and revoked JWTs retain existing 401/403 contracts.

Responses contain actor/target membership IDs, operation, role/status snapshots,
reason, revision and UTC time. They exclude request hashes, signatures, challenges,
credentials and the technical provisioning/recovery ledger. Responses are no-store.
There is no audit POST/UPDATE/DELETE surface. Existing directory GETs and LegacyEndpointGate
remain in place; the only additional HTTP routes are these two GETs.

## Persistence, trust boundary and rollout

migrations/mt5/governance contains the one-shot additive migration, hash manifest
and read-only validator. Two new tables and eight triggers extend, not replace,
existing objects. Explicit backups retain trigger creation contexts; never rely
on a raw MySQL 5.7 dump alone for all historical SQL_MODE values. A partial DDL
failure requires manual review, with no replay or automatic restore.

Application authorization assumes trusted deployed code, owner configuration and
a secure database host. A database/server administrator with unrestricted ability
to replace schemas/code is outside the ordinary-user threat boundary. The application
never uses that administrator credential as legal/platform authorization. Persisted
recovery signatures additionally prevent treating arbitrary approval-shaped rows as
verified consent. Do not publish database credentials, private keys, proofs or dumps.

The portable bootstrap includes the complete schema and validators; see
../development/LOCAL_DATABASE_SETUP.md. Production legal evidence retention, delivery
operations and real personnel/key registration remain owner responsibilities, not
synthetic test claims or automatically configured services.
