-- All counts must be zero. Metadata inventory must match the versioned schema contract.
SELECT 'invalid_class_events',COUNT(*) FROM institution_class_event
WHERE revision<1 OR action NOT IN ('class.created','class.renamed','class.deleted')
 OR (before_json IS NOT NULL AND NOT JSON_VALID(before_json)) OR (after_json IS NOT NULL AND NOT JSON_VALID(after_json));
SELECT 'foreign_class_actors',COUNT(*) FROM institution_class_event e LEFT JOIN institution_membership m
 ON m.institution_id=e.institution_id AND m.id=e.actor_membership_id AND m.user_id=e.actor_user_id WHERE m.id IS NULL;
SELECT 'class_event_trigger_count',ABS(3-COUNT(*)) FROM information_schema.TRIGGERS
 WHERE TRIGGER_SCHEMA=DATABASE() AND TRIGGER_NAME IN ('class_event_insert','class_event_update','class_event_delete');
-- Complete incoming-FK inventory for safe deletion. No new CASCADE/SET NULL may be ignored.
SELECT k.TABLE_NAME,k.CONSTRAINT_NAME,k.COLUMN_NAME,r.DELETE_RULE FROM information_schema.KEY_COLUMN_USAGE k
 JOIN information_schema.REFERENTIAL_CONSTRAINTS r ON r.CONSTRAINT_SCHEMA=k.CONSTRAINT_SCHEMA AND r.CONSTRAINT_NAME=k.CONSTRAINT_NAME AND r.TABLE_NAME=k.TABLE_NAME
 WHERE k.REFERENCED_TABLE_SCHEMA=DATABASE() AND k.REFERENCED_TABLE_NAME='class'
 ORDER BY k.TABLE_NAME,k.CONSTRAINT_NAME,k.ORDINAL_POSITION;
