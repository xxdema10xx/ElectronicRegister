# Exam presentation ï¿½ 20261022.1

Fresh databases: use `tools/setup-local-test-db.ps1` and the consolidated bootstrap.
The new column is already present: never run the expansion afterward.

Existing isolated MySQL 5.7.24 databases: stop API/worker writers and use
`tools/Invoke-ExamPresentationMigration.ps1 -MySqlBin <bin> -PrivateEvidenceDirectory <repo>/.exam-private/<new-directory> -Apply`.
The script verifies port 13306, database, datadir suffix, empty connection/transaction
inventory, absence of the new column; exports mismatched names; takes a full logical
backup with SHA256; applies DDL once; validates it. It refuses replay. No reset,
restore, trigger/FK disabling or GLOBAL sql_mode changes. After any DDL failure,
retain evidence and inspect manually. Never point this script at the principal DB.

`display_order_json LONGTEXT NULL` contains question IDs in display order and a
question-ID dictionary of option-ID arrays. New attempts use independent Fisherï¿½Yates
draws via RandomNumberGenerator.GetInt32. The JSON is inserted with the attempt in
the existing Start transaction and protected by its exam row lock and unique
(exam_id, student_id) constraint. Reads neither generate nor persist a new order.
NULL explicitly preserves the old canonical order, including its exposed Positions.
New Positions are 1-based display ranks. Canonical entities are never mutated.
Invalid or mismatched JSON fails closed rather than silently regenerating it.

Historical names are deliberately NOT updated by expansion. The validation query
exports historical/current names and attempt counts per owner. Keep the inventory
and full backup private, review mismatches (including submitted/finalized attempts),
and approve any historical reconciliation explicitly. Then perform owner-and-ID
specific updates from that reviewed inventory in a separate transaction, checking
old values, affected counts and final mismatch query; preserve the original-name
export indefinitely according to the operator's retention policy. There is no bulk
UPDATE or automatic reconciliation in setup. API reads immediately use Subject.Name;
new writes and explicit subject renames persist the authoritative name. Subject
renames run in a transaction with updates to the same owner's associated exams.

Input `Name` remains an ignored, optional CLR compatibility member so existing
compiled test/client helpers still build. JSON cannot set it and it is omitted from
serialization/OpenAPI input contracts. Output DTOs retain Name. Description remains
teacher editable. The existing frontends contain no exam-creation contract to change.
