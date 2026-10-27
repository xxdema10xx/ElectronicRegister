-- MySQL 5.7.24. Each returned mismatch count must be zero.
SELECT 'role_column_mismatch', COUNT(*)<>1 FROM information_schema.COLUMNS
 WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='institution_membership_role' AND COLUMN_NAME='role'
 AND BINARY COLUMN_TYPE=BINARY "enum('InstitutionAdmin','Teacher','Student','Staff')" AND IS_NULLABLE='NO';
SELECT 'role_trigger_mismatch', 3-COUNT(*) FROM information_schema.TRIGGERS
 WHERE TRIGGER_SCHEMA=DATABASE() AND TRIGGER_NAME IN ('mt5c_invitation_insert','mt5d_event_insert','mt5d_event_snapshot_shape')
 AND ACTION_STATEMENT LIKE '%Staff%';
SELECT 'unknown_roles', COUNT(*) FROM institution_membership_role
 WHERE BINARY role NOT IN ('InstitutionAdmin','Teacher','Student','Staff');
SELECT 'missing_security_state', COUNT(*) FROM institution_membership m
 LEFT JOIN institution_membership_security_state s ON s.institution_id=m.institution_id AND s.membership_id=m.id WHERE s.membership_id IS NULL;
SELECT 'staff_audit_guard_mismatch', 2-COUNT(*) FROM information_schema.TRIGGERS
 WHERE TRIGGER_SCHEMA=DATABASE() AND TRIGGER_NAME IN ('mt3a_audit_update','staff_audit_delete')
 AND ACTION_STATEMENT LIKE '%Staff audit append only%';
SELECT 'staff_audit_actor_mismatch', COUNT(*) FROM audit_event a
 LEFT JOIN institution_membership m ON m.institution_id=a.institution_id
 AND BINARY m.id=BINARY JSON_UNQUOTE(JSON_EXTRACT(a.after_json,'$.ActorMembershipId')) AND m.user_id=a.actor_user_id
 WHERE a.action LIKE 'staff.%' AND (a.actor_user_id IS NULL OR m.id IS NULL);
