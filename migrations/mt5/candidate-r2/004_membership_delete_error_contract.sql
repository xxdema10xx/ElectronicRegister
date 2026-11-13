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

-- Additive guard, no replacement/drop of the retained retention trigger.
DELIMITER $$
CREATE TRIGGER mt5b_membership_delete_contract BEFORE DELETE ON institution_membership
FOR EACH ROW PRECEDES mt5b_membership_security_delete
BEGIN
 IF NOT(COALESCE((
 DATABASE()='electronic_register_test'
 AND VERSION()='5.7.24' AND @@port=13306
 AND SUBSTRING_INDEX(USER(),'@',1)='root'
 AND RIGHT(LOWER(REPLACE(@@datadir,CHAR(92),'/')),45)='/migrations/mt4/private/mt5a-mamp-13306/data/'
 AND FIND_IN_SET(OLD.institution_id,COALESCE(@er_mt5b_fixture_owners,''))>0
 AND EXISTS(
   SELECT 1 FROM institution i WHERE i.id=OLD.institution_id
    AND BINARY i.code<>'MT3A_LEGACY_TEST'
    AND BINARY i.code REGEXP '^MT(1|1_REHEARSAL|2|3|3_REHEARSAL|3A|3B|3C|3D|4|4B|5A|5BP)_[0-9a-fA-F]{32}$'
 )
),0)) THEN
  SIGNAL SQLSTATE '23000' SET MYSQL_ERRNO=1451,
   MESSAGE_TEXT='MT5B membership is retained by its security state';
 END IF;
END$$
DELIMITER ;
