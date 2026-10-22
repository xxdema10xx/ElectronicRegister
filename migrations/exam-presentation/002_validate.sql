-- Must return one nullable LONGTEXT column.
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'exam_submission' AND COLUMN_NAME = 'display_order_json';
-- Must return zero. NULL marks a historical canonical-order attempt.
SELECT COUNT(*) AS invalid_orders FROM exam_submission
WHERE display_order_json IS NOT NULL AND NOT JSON_VALID(display_order_json);
-- Export this inventory before any reconciliation. Names are not rewritten by migration.
SELECT e.institution_id, e.id, e.name AS historical_name, s.name AS subject_name,
       (SELECT COUNT(*) FROM exam_submission es WHERE es.exam_id = e.id) AS attempts
FROM exam e JOIN class_subject cs ON cs.id = e.class_subject_id AND cs.institution_id = e.institution_id
JOIN subject s ON s.id = cs.subject_id AND s.institution_id = cs.institution_id
WHERE BINARY e.name <> BINARY s.name;
