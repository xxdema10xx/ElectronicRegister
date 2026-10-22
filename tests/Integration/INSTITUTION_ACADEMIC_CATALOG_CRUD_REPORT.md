# Institution academic catalog CRUD — 2026-10-22

## Scope and contracts

Five dedicated institutional controllers provide 23 routes: StudyArea, StudyPath
and Biennium have list/detail/create/update/delete; BienniumStudyArea and
BienniumStudyPath have list/detail/create/delete only. See
`docs/api/INSTITUTION_ACADEMIC_MANAGEMENT.md` for request/response contracts.

DTOs are independent of EF entities, reject unmapped JSON, and never accept owner,
audit or server identity. Names are trimmed, MySQL collation is authoritative for
duplicates, TEXT is validated by UTF-8 byte length, dates retain DateOnly and MySQL
1000-9999 bounds. List sorting/paging and ownership execute in SQL. Descriptive
association data can be obtained through the tenant catalog detail endpoints.

All operations require current InstitutionAdmin and SchoolApi rollout. Writes
revalidate membership, roles and security generation inside the transaction under
the existing IAM tenant-first lock order. Missing and foreign resources return
indistinguishable 404. PUT requires If-Match and rejects stale revisions with 409.

## Persistence and safety

Existing five business tables, FK and UNIQUE constraints are reused. The additive
`migrations/institution-administration/001_expand.sql` creates one shared ledger,
with restrictive owner/composite actor FK, per-resource revision uniqueness and
three append-only/validation triggers. No fake CourseEdition or exam events.
New FK: `fk_administration_event_owner`, `fk_administration_event_actor`.
New triggers: `administration_event_insert`, `administration_event_update`,
`administration_event_delete`. Existing Class ledger triggers are unchanged.
Snapshots, actor, tenant and UTC server timestamp commit atomically with mutations.
No historical events are invented; existing resources start at revision zero.

Deletion locks the parent, inventories actual incoming FK, uses current locking
child reads, and rejects dependencies before DELETE. CASCADE/SET NULL are never
used as an application removal strategy. Each discovered-child SQL query joins
the explicitly tenant-owned parent rather than relying on an unscoped GUID lookup.
No implicit class/path/edition changes.
Future non-id references fail closed until reviewed. The immutable audit survives
physical deletion because it does not reference the live resource through a FK.

Schema inventory: 41 -> 42 tables; 91 -> 94 triggers; 120 -> 122 FK;
167 -> 170 indexes. Original table and trigger definitions remain preserved.
Bootstrap includes the ledger and must not be followed by historical migrations.

## Files

Controllers: InstitutionStudyArea, InstitutionStudyPath, InstitutionBiennium,
InstitutionBienniumStudyArea, InstitutionBienniumStudyPath and InstitutionAcademicHttp.
Domain: InstitutionAcademicCatalogDtos, InstitutionAcademicRules and dedicated
IInstitutionAcademicCatalogManager/Repository. Application: InstitutionAcademicCatalogManager.
Infrastructure: InstitutionAcademicCatalogRepository, InstitutionAdministrationPersistence,
InstitutionAdministrationEvent, context mapping and InstitutionAdministrationIntegrity.
Shared DI, offline classifier, setup validator, bootstrap/contract/manifest and
migration runner are updated without replacing the previous Class/Exam artifacts.

Tests: InstitutionAcademicCatalogTests, InstitutionAcademicAuditFaultTests,
InstitutionAcademicRulesTests. The HTTP chain creates all five catalog resources
and a Class without SQL insertion of those business resources. Tests cover real
MySQL collation, concurrent creates/updates, ownership, FK dependencies, forbidden
roles, unknown fields, input boundaries and rollback after the audit was inserted.

## Verification status

Initial baseline enumerated: 1034 identities (90 unit / 944 integration).
First targeted HTTP run after query correction: 22 passed. Initial full run found
four offline-validator failures caused by a copied trigger-message predicate; the
predicate was corrected. Final phase result is recorded in the overall coverage
report after the complete regression. No test identities were removed or disabled.

Phase A completed regression: **1073/1073 passed** (97 unit, 976 integration), zero
failures and zero skips. Added 7 unit outcomes and 32 integration outcomes.

Tests use a newly provisioned MySQL 5.7.24 datadir under ignored `.exam-private`,
with verified identity and pre-bootstrap backup. The new process bound 13306 and
its exact new datadir was attested before DDL. Port 3306 was never connected to or modified. No existing
datadir was reused. Private evidence, backups and logs are not Git candidates.

Rollout remains disabled by default; frontend contracts do not enable deployment.

## Final global verification

**1130/1130 passed** (103 unit, 1027 integration), zero failures and zero skips.
All 1034 prior identities preserved; 96 new outcomes. All three phases complete.
Both isolated migration and candidate-only fresh bootstrap match 42 tables, 94
triggers, 122 FK and 170 indexes; all versioned validator checks pass. 138 manifest
file hashes and Git/EOL portability verified. See the coverage report and sanitized
INSTITUTION_ADMIN_ACADEMIC_VERIFICATION.json for exact phase counts and files.
