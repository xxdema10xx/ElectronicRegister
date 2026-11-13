-- Read-only MySQL 8.4 validation of er-platform-identity-1. Every violations column must equal zero.
SELECT 'platform_marker' AS check_name,ABS(1-COUNT(*)) AS violations FROM er_schema_release WHERE version='er-platform-identity-1';
SELECT 'platform_predecessor_marker' AS check_name,ABS(1-COUNT(*)) AS violations FROM er_schema_release WHERE version='er-quality-2-mysql84';
SELECT 'platform_user_columns' AS check_name,ABS(7-COUNT(*)) AS violations FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='user' AND (
 (COLUMN_NAME='fiscal_code_hmac' AND COLUMN_TYPE='char(64)' AND IS_NULLABLE='YES' AND COLLATION_NAME='ascii_bin') OR
 (COLUMN_NAME='fiscal_code_hmac_key_version' AND COLUMN_TYPE='varchar(32)' AND IS_NULLABLE='YES' AND COLLATION_NAME='ascii_bin') OR
 (COLUMN_NAME='fiscal_code_enc' AND COLUMN_TYPE='varbinary(64)' AND IS_NULLABLE='YES') OR
 (COLUMN_NAME='fiscal_code_enc_key_version' AND COLUMN_TYPE='varchar(32)' AND IS_NULLABLE='YES' AND COLLATION_NAME='ascii_bin') OR
 (COLUMN_NAME='lifecycle_state' AND COLUMN_TYPE='enum(''Active'',''Closed'')' AND IS_NULLABLE='NO' AND COLUMN_DEFAULT='Active' AND COLLATION_NAME='ascii_bin') OR
 (COLUMN_NAME='closed_at_utc' AND COLUMN_TYPE='datetime(6)' AND IS_NULLABLE='YES') OR
 (COLUMN_NAME='reactivated_at_utc' AND COLUMN_TYPE='datetime(6)' AND IS_NULLABLE='YES'));
SELECT 'platform_fiscal_unique' AS check_name,ABS(1-COUNT(*)) AS violations FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='user' AND INDEX_NAME='uq_user_fiscal_code_hmac' AND NON_UNIQUE=0 AND COLUMN_NAME='fiscal_code_hmac' AND SEQ_IN_INDEX=1;
SELECT 'platform_lifecycle_table' AS check_name,ABS(1-COUNT(*)) AS violations FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='identity_lifecycle_event' AND ENGINE='InnoDB' AND TABLE_COLLATION='utf8mb4_unicode_ci';
SELECT 'platform_lifecycle_fk' AS check_name,ABS(1-COUNT(*)) AS violations FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='identity_lifecycle_event' AND CONSTRAINT_NAME='fk_lifecycle_user' AND REFERENCED_TABLE_NAME='user' AND DELETE_RULE='RESTRICT' AND UPDATE_RULE='RESTRICT';
SELECT 'platform_triggers' AS check_name,ABS(6-COUNT(*)) AS violations FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() AND ACTION_TIMING='BEFORE' AND (
 (TRIGGER_NAME='platform_user_insert' AND EVENT_OBJECT_TABLE='user' AND EVENT_MANIPULATION='INSERT') OR
 (TRIGGER_NAME='platform_user_update' AND EVENT_OBJECT_TABLE='user' AND EVENT_MANIPULATION='UPDATE') OR
 (TRIGGER_NAME='platform_user_delete' AND EVENT_OBJECT_TABLE='user' AND EVENT_MANIPULATION='DELETE') OR
 (TRIGGER_NAME='lifecycle_event_insert' AND EVENT_OBJECT_TABLE='identity_lifecycle_event' AND EVENT_MANIPULATION='INSERT') OR
 (TRIGGER_NAME='lifecycle_event_update' AND EVENT_OBJECT_TABLE='identity_lifecycle_event' AND EVENT_MANIPULATION='UPDATE') OR
 (TRIGGER_NAME='lifecycle_event_delete' AND EVENT_OBJECT_TABLE='identity_lifecycle_event' AND EVENT_MANIPULATION='DELETE'));
SELECT 'platform_preserved_email_triggers' AS check_name,ABS(3-COUNT(*)) AS violations FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() AND TRIGGER_NAME IN ('email_user_insert','email_user_update','email_reservation_delete');
SELECT 'platform_fiscal_shape' AS check_name,COUNT(*) AS violations FROM `user` WHERE NOT(
 (fiscal_code_hmac IS NULL AND fiscal_code_hmac_key_version IS NULL AND fiscal_code_enc IS NULL AND fiscal_code_enc_key_version IS NULL) OR
 (fiscal_code_hmac IS NOT NULL AND fiscal_code_hmac_key_version IS NOT NULL AND fiscal_code_enc IS NOT NULL AND fiscal_code_enc_key_version IS NOT NULL AND REGEXP_LIKE(fiscal_code_hmac,'^[0-9A-F]{64}$','c')));
SELECT 'platform_lifecycle_shape' AS check_name,COUNT(*) AS violations FROM `user` WHERE (lifecycle_state='Closed' AND (closed_at_utc IS NULL OR (reactivated_at_utc IS NOT NULL AND reactivated_at_utc>=closed_at_utc))) OR
 (lifecycle_state='Active' AND closed_at_utc IS NOT NULL AND (reactivated_at_utc IS NULL OR reactivated_at_utc<closed_at_utc));
SELECT 'platform_closed_without_event' AS check_name,COUNT(*) AS violations FROM `user` u WHERE u.lifecycle_state='Closed' AND NOT EXISTS(SELECT 1 FROM identity_lifecycle_event e WHERE e.user_id=u.id AND e.transition='Closed');
SELECT 'platform_event_values' AS check_name,COUNT(*) AS violations FROM identity_lifecycle_event e LEFT JOIN `user` u ON u.id=e.user_id WHERE u.id IS NULL OR e.transition NOT IN ('Closed','Reactivated') OR e.rule_version<1 OR e.security_generation<1;
SELECT 'platform_email_namespace' AS check_name,COUNT(*) AS violations FROM `user` u LEFT JOIN institution_email_reservation r ON r.address=LOWER(u.email) AND r.user_id=u.id WHERE r.address IS NULL;
