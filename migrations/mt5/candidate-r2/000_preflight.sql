-- MT-5B candidate R2: preflight correction only; NOT the lost certified SQL.
-- Review only. Never run against the MT4C baseline on port 3306.
-- MySQL 5.7.24, quiescent writers, verified backup and reviewed driver required.
-- Seven explicit rules. The reviewed driver must require seven zero values.
-- Valid before fresh installation or after all eleven triggers, not mid-EXPAND.
SELECT 'target_session' AS rule,
 IF(COALESCE((DATABASE()='electronic_register_test'
 AND VERSION()='5.7.24' AND @@port=13306
 AND SUBSTRING_INDEX(USER(),'@',1)='root'
 AND RIGHT(LOWER(REPLACE(@@datadir,CHAR(92),'/')),45)='/migrations/mt4/private/mt5a-mamp-13306/data/'
 AND @@foreign_key_checks=1 AND @@unique_checks=1
 AND FIND_IN_SET('STRICT_TRANS_TABLES',@@SESSION.sql_mode)>0
 AND EXISTS(SELECT 1 FROM information_schema.SCHEMATA WHERE SCHEMA_NAME=DATABASE()
   AND DEFAULT_CHARACTER_SET_NAME='utf8mb4' AND DEFAULT_COLLATION_NAME='utf8mb4_unicode_ci')),0),0,1) AS violations
UNION ALL
SELECT 'baseline_objects',
 ABS(32-(SELECT COUNT(*) FROM information_schema.TABLES
  WHERE TABLE_SCHEMA=DATABASE() AND TABLE_TYPE='BASE TABLE'
    AND TABLE_NAME<>'institution_membership_security_state'))
 + (SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE()
    AND (TABLE_TYPE<>'BASE TABLE' OR ENGINE<>'InnoDB'))
 + ABS(98-(SELECT COUNT(*) FROM information_schema.REFERENTIAL_CONSTRAINTS
    WHERE CONSTRAINT_SCHEMA=DATABASE() AND CONSTRAINT_NAME<>'fk_membership_security_owner'))
 + ABS(139-(SELECT COUNT(*) FROM
   (SELECT TABLE_SCHEMA,TABLE_NAME,INDEX_NAME FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME<>'institution_membership_security_state'
    GROUP BY TABLE_SCHEMA,TABLE_NAME,INDEX_NAME) original_indexes))
UNION ALL
SELECT 'principal_contract',
 IF(EXISTS(SELECT 1 FROM information_schema.STATISTICS
   WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='institution_membership'
     AND INDEX_NAME='PRIMARY' AND NON_UNIQUE=0
   GROUP BY TABLE_NAME,INDEX_NAME
   HAVING GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX)='id'),0,1)
 + IF(EXISTS(SELECT 1 FROM information_schema.STATISTICS
   WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='institution_membership_role'
     AND INDEX_NAME='PRIMARY' AND NON_UNIQUE=0
   GROUP BY TABLE_NAME,INDEX_NAME
   HAVING GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX)='membership_id,role'),0,1)
 +
 IF(EXISTS(SELECT 1 FROM information_schema.STATISTICS
   WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='institution_membership'
     AND NON_UNIQUE=0
   GROUP BY TABLE_NAME,INDEX_NAME
   HAVING GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX)='institution_id,id'),0,1)
 + ABS(3-(SELECT COUNT(*) FROM information_schema.COLUMNS
   WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='institution_membership'
    AND COLUMN_NAME IN ('id','institution_id','user_id') AND DATA_TYPE='char'
    AND CHARACTER_MAXIMUM_LENGTH=36 AND CHARACTER_SET_NAME='ascii'
    AND COLLATION_NAME='ascii_general_ci' AND IS_NULLABLE='NO'))
 + ABS(3-(SELECT COUNT(*) FROM information_schema.REFERENTIAL_CONSTRAINTS
   WHERE CONSTRAINT_SCHEMA=DATABASE()
    AND CONSTRAINT_NAME IN ('fk_membership_institution','fk_membership_user','fk_membership_role_membership')
    AND DELETE_RULE='RESTRICT' AND UPDATE_RULE='RESTRICT'))
UNION ALL
SELECT 'retained_trigger_safety',
 ABS(53-(SELECT COUNT(*) FROM information_schema.TRIGGERS
   WHERE TRIGGER_SCHEMA=DATABASE() AND LEFT(TRIGGER_NAME,5)<>'mt5b_'))
 + (SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE()
    AND LEFT(TRIGGER_NAME,5)<>'mt5b_'
    AND EVENT_OBJECT_TABLE IN ('institution_membership','institution_membership_role','institution_membership_security_state'))
UNION ALL
SELECT 'membership_integrity',
 (SELECT COUNT(*) FROM institution_membership m
   LEFT JOIN institution i ON i.id=m.institution_id LEFT JOIN user u ON u.id=m.user_id
   WHERE i.id IS NULL OR u.id IS NULL)
 + (SELECT COUNT(*) FROM institution_membership_role r
   LEFT JOIN institution_membership m ON m.id=r.membership_id WHERE m.id IS NULL)
UNION ALL
SELECT 'holding_provisioning',
 IF((SELECT COUNT(*) FROM institution WHERE code='MT3A_LEGACY_TEST' AND status='provisioning')=1,0,1)
UNION ALL
SELECT 'installation_boundary',
 IF(
 ((SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE()
    AND TABLE_NAME='institution_membership_security_state')=0
  AND (SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE()
    AND LEFT(TRIGGER_NAME,5)='mt5b_')=0)
 OR
 ((SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE()
    AND TABLE_NAME='institution_membership_security_state')=1
  AND (SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE()
    AND LEFT(TRIGGER_NAME,5)='mt5b_')=11),0,1);
