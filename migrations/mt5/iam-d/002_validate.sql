-- Read-only; each count must be zero.
SELECT 'iam_missing_objects', ABS(1-(SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='institution_membership_event'))+ABS(3-(SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() AND TRIGGER_NAME IN ('mt5d_event_insert','mt5d_event_update','mt5d_event_delete')));
SELECT 'iam_revision_invalid',COUNT(*) FROM institution_membership_event WHERE revision_before<0 OR revision_after<=revision_before OR revision_after-revision_before<>1;
SELECT 'iam_event_actor_owner',COUNT(*) FROM institution_membership_event e LEFT JOIN institution_membership m ON m.institution_id=e.institution_id AND m.id=e.actor_membership_id AND m.user_id=e.actor_user_id WHERE m.id IS NULL;
SELECT 'iam_event_target_owner',COUNT(*) FROM institution_membership_event e LEFT JOIN institution_membership m ON m.institution_id=e.institution_id AND m.id=e.target_membership_id WHERE m.id IS NULL;
