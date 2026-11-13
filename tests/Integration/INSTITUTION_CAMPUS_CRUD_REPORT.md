# Institution Campus CRUD — 2026-10-22

Five endpoints under `/api/institution/campuses`: GET list/detail, POST, PUT and
DELETE. Existing CourseEdition link endpoints remain unchanged. Dedicated
InstitutionCampusDto/CreateInstitutionCampusDto/UpdateInstitutionCampusDto and
IInstitutionCampusManager/Repository keep EF out of the HTTP contract. Manager,
repository, validation rules and scoped DI are implemented in the existing layers.

All endpoints require current InstitutionAdmin, InstitutionAccess and verified
TenantContext. SchoolApi + CourseApi flags remain disabled by default. The explicit
InstitutionCourseEndpoint marker and policy continue through LegacyEndpointGate;
no prefix-based authorization. Foreign/missing IDs return indistinguishable 404.

Name is required, trimmed and max 200. Nullable Address/City/Province use actual
300/100/100 limits; blank optional values become null. Create defaults IsActive
true; PUT requires explicit IsActive and If-Match. Stale revisions return 409,
missing precondition 428, unknown JSON/query owner fields 400. No invented name
uniqueness constraint. SQL list paging/order/filtering preserves the previous
array/CampusId contract, with additional Id/Revision and X-Total-Count header.

Disactivation changes IsActive only. Reactivation uses the same PUT. Links and
editions are never modified implicitly. Physical deletion is allowed only without
actual incoming FK references, including CourseEditionCampus and restrictive
course_administration_event history. The new generic resource mutation ledger
does not reference the live campus and survives deletion. A campus with historical
administrative references needs a separate future archival workflow.

Schema before/after phase C: 42 tables, 94 triggers, 122 FK, 170 indexes.
No extra DDL in phase C. Phase A shared ledger supplies atomic actor/tenant/action/
snapshot/UTC history and durable revisions. Authorization read locks are acquired
tenant-first and block IAM revocation; they remain compatible with owner/actor FK
checks in existing workflows. Resource mutations hold exclusive row locks and a
unique resource revision prevents stale-snapshot lost updates. All SQL constraint
errors are translated without database details. FK and triggers remain enabled.

Files: InstitutionCampus controller/manager/repository and interfaces; DTOs and
InstitutionCampusRules; DI and precise route-inventory expectations; new Campus
CRUD, fault injection, rules and shared security/dependency-race tests. See
`docs/api/INSTITUTION_ACADEMIC_MANAGEMENT.md` for React Native contracts.

Initial Campus targeted run: 15 passed. Campus plus shared security targeted run:
30 passed, zero failures/skips after aligning the new rollout expectation with the
existing policy's 403 behavior. Checks include activation, links, input limits,
unknown fields, isolation, concurrent updates, rollback after audit, and revocation
between manager authorization and the transactional write. Shared tests verify
every new route's JWT purpose/generation, rollout policy and exact marker/roles;
root cannot UPDATE/DELETE audit rows. Final suite counts are in the coverage report.

MySQL 5.7.24 uses the newly created ignored private datadir. Existing databases were
preserved; no connection to port 3306, no resets/restores, no FK/trigger disabling,
no GLOBAL sql_mode changes, commit or push. Frontend-ready contracts do not enable
operational rollout.

## Final global verification

**1130/1130 passed** (103 unit, 1027 integration), zero failures and zero skips.
All 1034 prior identities preserved; 96 new outcomes. All three phases complete.
Both isolated migration and candidate-only fresh bootstrap match 42 tables, 94
triggers, 122 FK and 170 indexes; all versioned validator checks pass. 138 manifest
file hashes and Git/EOL portability verified. See the coverage report and sanitized
INSTITUTION_ADMIN_ACADEMIC_VERIFICATION.json for exact phase counts and files.
