-- Additive hardening: JSON_CONTAINS is recursive; snapshots must be flat strings.
-- Requires the complete 001 installation and a fresh verified private backup.
SET @mt5d_shape_gate=IF(COALESCE((DATABASE()='electronic_register_test' AND VERSION()='5.7.24'
 AND @@hostname='CES01622' AND @@port=13306 AND SUBSTRING_INDEX(USER(),'@',1)='root'
 AND RIGHT(LOWER(REPLACE(@@datadir,CHAR(92),'/')),45)='/migrations/mt4/private/mt5a-mamp-13306/data/'
 AND @@foreign_key_checks=1 AND @@unique_checks=1
 AND FIND_IN_SET('STRICT_TRANS_TABLES',@@SESSION.sql_mode)>0
 AND (SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_TYPE='BASE TABLE')=34
 AND (SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE())=67
 AND (SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() AND TRIGGER_NAME IN ('mt5d_event_insert','mt5d_event_update','mt5d_event_delete'))=3
 AND NOT EXISTS(SELECT 1 FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() AND TRIGGER_NAME='mt5d_event_snapshot_shape')),0),
 'DO 0','SELECT MT5D_SNAPSHOT_TARGET_MISMATCH');
PREPARE mt5d_shape_check FROM @mt5d_shape_gate;
EXECUTE mt5d_shape_check;
DEALLOCATE PREPARE mt5d_shape_check;
SET @mt5d_shape_gate=NULL;
DELIMITER $$
CREATE TRIGGER mt5d_event_snapshot_shape BEFORE INSERT ON institution_membership_event
FOR EACH ROW FOLLOWS mt5d_event_insert
BEGIN
 DECLARE position_index INT DEFAULT 0;
 IF JSON_LENGTH(NEW.previous_roles)>3 OR JSON_LENGTH(NEW.new_roles)>3 THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D role snapshot exceeds catalog';
 END IF;
 WHILE position_index<3 DO
  IF position_index<JSON_LENGTH(NEW.previous_roles) AND NOT(COALESCE((
    JSON_TYPE(JSON_EXTRACT(NEW.previous_roles,CONCAT('$[',position_index,']')))='STRING'
    AND BINARY JSON_UNQUOTE(JSON_EXTRACT(NEW.previous_roles,CONCAT('$[',position_index,']'))) IN ('InstitutionAdmin','Teacher','Student')),0)) THEN
   SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D previous roles must be flat catalog strings';
  END IF;
  IF position_index<JSON_LENGTH(NEW.new_roles) AND NOT(COALESCE((
    JSON_TYPE(JSON_EXTRACT(NEW.new_roles,CONCAT('$[',position_index,']')))='STRING'
    AND BINARY JSON_UNQUOTE(JSON_EXTRACT(NEW.new_roles,CONCAT('$[',position_index,']'))) IN ('InstitutionAdmin','Teacher','Student')),0)) THEN
   SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D new roles must be flat catalog strings';
  END IF;
  SET position_index=position_index+1;
 END WHILE;
END$$
DELIMITER ;
