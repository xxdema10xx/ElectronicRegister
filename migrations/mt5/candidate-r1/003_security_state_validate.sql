-- MT-5B-R1 reconstructed candidate; NOT the lost certified SQL.
-- Review only. Never run against the MT4C baseline on port 3306.
-- MySQL 5.7.24, quiescent writers, verified backup and reviewed driver required.
-- Six zero rules; valid after 002 both before and after additive 004.
-- Presence/shape checks do NOT certify executable trigger bodies: compare bodies
-- with these candidates and execute negative tests on the isolated server later.
SELECT 'missing_state' AS rule,COUNT(*) AS violations
 FROM institution_membership m LEFT JOIN institution_membership_security_state s
 ON s.institution_id=m.institution_id AND s.membership_id=m.id WHERE s.membership_id IS NULL
UNION ALL
SELECT 'orphan_state',COUNT(*)
 FROM institution_membership_security_state s LEFT JOIN institution_membership m
 ON m.institution_id=s.institution_id AND m.id=s.membership_id WHERE m.id IS NULL
UNION ALL
SELECT 'invalid_generation',COUNT(*) FROM institution_membership_security_state
 WHERE security_generation IS NULL OR security_generation<0
UNION ALL
SELECT 'sidecar_contract',
 IF(EXISTS(SELECT 1 FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE()
   AND TABLE_NAME='institution_membership_security_state' AND ENGINE='InnoDB'
   AND TABLE_COLLATION='utf8mb4_unicode_ci'),0,1)
 + ABS(3-(SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE()
   AND TABLE_NAME='institution_membership_security_state'))
 + ABS(2-(SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE()
   AND TABLE_NAME='institution_membership_security_state' AND COLUMN_NAME IN ('institution_id','membership_id')
   AND DATA_TYPE='char' AND CHARACTER_MAXIMUM_LENGTH=36 AND IS_NULLABLE='NO'
   AND CHARACTER_SET_NAME='ascii' AND COLLATION_NAME='ascii_general_ci'))
 + IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE()
   AND TABLE_NAME='institution_membership_security_state' AND COLUMN_NAME='security_generation'
   AND COLUMN_TYPE='bigint(20)' AND IS_NULLABLE='NO' AND COLUMN_DEFAULT='0' AND EXTRA=''),0,1)
 + ABS(2-(SELECT COUNT(DISTINCT INDEX_NAME) FROM information_schema.STATISTICS
   WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='institution_membership_security_state'))
 + ABS(1-(SELECT COUNT(*) FROM information_schema.REFERENTIAL_CONSTRAINTS
   WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='institution_membership_security_state'))
 + IF(EXISTS(SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE()
   AND TABLE_NAME='institution_membership_security_state' AND INDEX_NAME='PRIMARY' AND NON_UNIQUE=0
   GROUP BY TABLE_NAME,INDEX_NAME HAVING GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX)='institution_id,membership_id'),0,1)
 + IF(EXISTS(SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE()
   AND TABLE_NAME='institution_membership_security_state' AND INDEX_NAME='uq_membership_security_identity' AND NON_UNIQUE=0
   GROUP BY TABLE_NAME,INDEX_NAME HAVING GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX)='membership_id'),0,1)
 + IF(EXISTS(SELECT 1 FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE()
   AND TABLE_NAME='institution_membership_security_state' AND CONSTRAINT_NAME='fk_membership_security_owner'
   AND REFERENCED_TABLE_NAME='institution_membership' AND UPDATE_RULE='RESTRICT' AND DELETE_RULE='RESTRICT'),0,1)
 + IF(EXISTS(SELECT 1 FROM information_schema.KEY_COLUMN_USAGE WHERE CONSTRAINT_SCHEMA=DATABASE()
   AND TABLE_NAME='institution_membership_security_state' AND CONSTRAINT_NAME='fk_membership_security_owner'
   AND REFERENCED_TABLE_SCHEMA=DATABASE() AND REFERENCED_TABLE_NAME='institution_membership'
   GROUP BY CONSTRAINT_NAME HAVING GROUP_CONCAT(CONCAT(COLUMN_NAME,':',REFERENCED_COLUMN_NAME)
     ORDER BY ORDINAL_POSITION)='institution_id:institution_id,membership_id:id'),0,1)
UNION ALL
SELECT 'core_trigger_contract',
 (SELECT COUNT(*) FROM (
  SELECT 'mt5b_security_insert' AS name,'institution_membership_security_state' AS tbl,'BEFORE' AS timing,'INSERT' AS evt
  UNION ALL
  SELECT 'mt5b_security_update','institution_membership_security_state','BEFORE','UPDATE'
  UNION ALL
  SELECT 'mt5b_security_delete','institution_membership_security_state','BEFORE','DELETE'
  UNION ALL
  SELECT 'mt5b_membership_security_insert','institution_membership','AFTER','INSERT'
  UNION ALL
  SELECT 'mt5b_membership_identity_update','institution_membership','BEFORE','UPDATE'
  UNION ALL
  SELECT 'mt5b_membership_security_update','institution_membership','AFTER','UPDATE'
  UNION ALL
  SELECT 'mt5b_membership_security_delete','institution_membership','BEFORE','DELETE'
  UNION ALL
  SELECT 'mt5b_role_security_insert','institution_membership_role','AFTER','INSERT'
  UNION ALL
  SELECT 'mt5b_role_security_delete','institution_membership_role','AFTER','DELETE'
  UNION ALL
  SELECT 'mt5b_role_identity_update','institution_membership_role','BEFORE','UPDATE'
 ) expected LEFT JOIN information_schema.TRIGGERS t
  ON t.TRIGGER_SCHEMA=DATABASE() AND t.TRIGGER_NAME=expected.name
  WHERE t.TRIGGER_NAME IS NULL OR t.EVENT_OBJECT_TABLE<>expected.tbl
    OR t.ACTION_TIMING<>expected.timing OR t.EVENT_MANIPULATION<>expected.evt)
 + (SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE()
   AND LEFT(TRIGGER_NAME,5)='mt5b_' AND TRIGGER_NAME NOT IN (
    'mt5b_security_insert',
    'mt5b_security_update',
    'mt5b_security_delete',
    'mt5b_membership_security_insert',
    'mt5b_membership_identity_update',
    'mt5b_membership_security_update',
    'mt5b_membership_security_delete',
    'mt5b_role_security_insert',
    'mt5b_role_security_delete',
    'mt5b_role_identity_update','mt5b_membership_delete_contract'))
UNION ALL
SELECT 'membership_dependencies',
 (SELECT COUNT(*) FROM institution_membership m LEFT JOIN institution i ON i.id=m.institution_id
   LEFT JOIN user u ON u.id=m.user_id WHERE i.id IS NULL OR u.id IS NULL)
 + (SELECT COUNT(*) FROM institution_membership_role r LEFT JOIN institution_membership m
   ON m.id=r.membership_id WHERE m.id IS NULL);
