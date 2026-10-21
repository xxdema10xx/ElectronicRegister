-- MT-5B-R1 reconstructed candidate; NOT the lost certified SQL.
-- Review only. Never run against the MT4C baseline on port 3306.
-- MySQL 5.7.24, quiescent writers, verified backup and reviewed driver required.
-- R1 installation gate: no routine/object is created to check the target.
-- PREPARE fails on an unresolved identifier on any non-authorized target.
SET @mt5b_r1_gate_sql=IF(COALESCE((DATABASE()='electronic_register_test'
 AND VERSION()='5.7.24' AND @@port=13306
 AND SUBSTRING_INDEX(USER(),'@',1)='root'
 AND @@foreign_key_checks=1 AND @@unique_checks=1
 AND FIND_IN_SET('STRICT_TRANS_TABLES',@@SESSION.sql_mode)>0
 AND RIGHT(LOWER(REPLACE(@@datadir,CHAR(92),'/')),45)='/migrations/mt4/private/mt5a-mamp-13306/data/'),0),
 'DO 0','SELECT MT5B_R1_TARGET_MISMATCH');
PREPARE mt5b_r1_gate FROM @mt5b_r1_gate_sql;
EXECUTE mt5b_r1_gate;
DEALLOCATE PREPARE mt5b_r1_gate;
SET @mt5b_r1_gate_sql=NULL;

-- INITIAL INSTALLATION ONLY. Not repair after generation-aware JWT activation.
-- Anti-join initializes missing rows; existing/nonzero generations are untouched.
-- One atomic InnoDB statement, writers quiescent; no INSERT IGNORE or upsert.
INSERT INTO institution_membership_security_state
 (institution_id,membership_id,security_generation)
SELECT m.institution_id,m.id,0
FROM institution_membership m
LEFT JOIN institution_membership_security_state s ON s.membership_id=m.id
WHERE s.membership_id IS NULL;
