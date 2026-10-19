-- Intentionally no DML: no verified Class/edition/enrollment manifest exists.
-- Re-running this phase never invents dates, editions, membership or attendance.
SELECT COUNT(*) AS legacy_classes_unattributed FROM class WHERE course_edition_id IS NULL;
