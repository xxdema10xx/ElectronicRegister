# Institution administration 20261022.3

Additive MySQL 5.7.24 ledger for academic catalog, ClassSubject and Campus writes.
No business tables are replaced. No historical events or actors are backfilled.

Fresh installations use the consolidated bootstrap ONLY. Never replay expansion
scripts after bootstrap. Existing installations require an explicitly attested
isolated test instance, quiescence, full verified backup and absent target objects.
Use `tools/Invoke-InstitutionAdministrationMigration.ps1`; it refuses replay and
stops on any SQL error. Evidence and backups belong under ignored `.exam-private`.
Do not target a preexisting user instance, including ports 3306 and 13306.

The ledger has restrictive owner and composite actor FK, a unique resource revision
index, actor/shape/owner validation and unconditional append-only UPDATE/DELETE
triggers. It intentionally has no FK to the live resource: deletion of an unused
resource preserves its history. All new mutations and their audit share one
transaction. Existing resources begin at revision zero; their first mutation creates
revision one. Snapshots describe the actual mutation, without inventing prior events.

Deletion discovers every incoming SQL FK under the locked parent and checks current
child rows with locking reads before issuing DELETE. Thus CASCADE/SET NULL cannot
remove linked business data. A resource with dependencies needs a dedicated future
administrative workflow, not automatic unlinking.
